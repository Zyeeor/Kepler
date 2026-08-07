// AssetPipelineHub v3 - Unity 2022.3+ Editor adapter
// State stays in ProjectSettings/AssetPipelineHub. No manifest is written into Assets.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class AssetPipelineHub_Unity : AssetPostprocessor
{
    private const string StateRelativePath = "ProjectSettings/AssetPipelineHub/latest_import.json";

    private void OnPreprocessTexture()
    {
        string name = Path.GetFileNameWithoutExtension(assetPath).ToUpperInvariant();
        TextureImporter importer = assetImporter as TextureImporter;
        if (importer == null) return;
        if (name.EndsWith("_N"))
        {
            importer.textureType = TextureImporterType.NormalMap;
            return;
        }
        if (name.EndsWith("_MRA") || name.EndsWith("_M") || name.EndsWith("_G") ||
            name.EndsWith("_R") || name.EndsWith("_AO"))
        {
            importer.sRGBTexture = false;
        }
    }

    [MenuItem("Tools/Asset Pipeline Hub/应用最近一次导入", false, 100)]
    [MenuItem("Assets/资产管线/应用最近一次导入", false, 1000)]
    public static void ApplyLatestImport()
    {
        string statePath = Path.Combine(ProjectRoot(), StateRelativePath);
        if (!File.Exists(statePath))
        {
            EditorUtility.DisplayDialog("Asset Pipeline Hub", "未找到交付清单。请先在桌面端执行阶段 04。\n\n" + statePath, "确定");
            return;
        }

        DeliveryState state = JsonUtility.FromJson<DeliveryState>(File.ReadAllText(statePath));
        if (state == null || state.assets == null)
        {
            EditorUtility.DisplayDialog("Asset Pipeline Hub", "交付清单无效。", "确定");
            return;
        }

        AssetDatabase.Refresh();
        Dictionary<string, string> shaderSlots = ParseShaderSlots(state.shader_slots);
        int materialCount = 0;
        int meshCount = 0;
        int textureCount = 0;

        foreach (DeliveryAsset asset in state.assets)
        {
            if (asset.materials == null) continue;
            foreach (DeliveryMaterial entry in asset.materials)
            {
                Material material = GetOrCreateMaterial(entry.asset_path, state);
                if (material == null) continue;
                if (entry.textures != null)
                {
                    foreach (DeliveryTexture textureEntry in entry.textures)
                    {
                        ApplyTextureImportSetting(textureEntry);
                        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureEntry.asset_path);
                        if (texture == null)
                        {
                            Debug.LogWarning("[AssetPipelineHub] 未找到贴图：" + textureEntry.asset_path);
                            continue;
                        }
                        string requestedSlot;
                        shaderSlots.TryGetValue(textureEntry.type, out requestedSlot);
                        string slot = ResolveShaderSlot(material, textureEntry.type, requestedSlot);
                        if (string.IsNullOrEmpty(slot))
                        {
                            Debug.LogWarning("[AssetPipelineHub] Shader 无可用槽位：" + textureEntry.type);
                            continue;
                        }
                        material.SetTexture(slot, texture);
                        textureCount++;
                    }
                }
                EditorUtility.SetDirty(material);
                RemapMaterial(state, asset, entry, material);
                materialCount++;
            }
            if (asset.meshes != null) meshCount += asset.meshes.Length;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Asset Pipeline Hub",
            "导入完成。\n\n模型：" + meshCount + "\n材质：" + materialCount + "\n贴图绑定：" + textureCount +
            "\n\n状态清单保存在 ProjectSettings，不会污染美术资源目录。",
            "确定"
        );
    }

    private static Material GetOrCreateMaterial(string targetPath, DeliveryState state)
    {
        targetPath = NormalizeAssetPath(targetPath);
        EnsureAssetFolder(NormalizeAssetPath(Path.GetDirectoryName(targetPath)));
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
        string sourcePath = ResolveProjectAssetPath(state.material_source_path);
        Material template = null;
        Shader requestedShader = null;

        if (state.material_source_mode == "material")
        {
            template = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (template != null) requestedShader = template.shader;
        }
        else
        {
            requestedShader = AssetDatabase.LoadAssetAtPath<Shader>(sourcePath);
        }

        if (requestedShader == null)
        {
            Debug.LogError("[AssetPipelineHub] 无法加载材质来源：" + state.material_source_path);
            return null;
        }

        if (existing == null)
        {
            if (template != null && AssetDatabase.CopyAsset(sourcePath, targetPath))
            {
                existing = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
                existing.name = Path.GetFileNameWithoutExtension(targetPath);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            existing = new Material(requestedShader);
            existing.name = Path.GetFileNameWithoutExtension(targetPath);
            AssetDatabase.CreateAsset(existing, targetPath);
            return existing;
        }

        if (template != null)
        {
            existing.shader = template.shader;
            if (state.material_update_policy == "reset_from_template")
            {
                existing.CopyPropertiesFromMaterial(template);
            }
        }
        else if (existing.shader != requestedShader)
        {
            existing.shader = requestedShader;
        }
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static void ApplyTextureImportSetting(DeliveryTexture entry)
    {
        TextureImporter importer = AssetImporter.GetAtPath(entry.asset_path) as TextureImporter;
        if (importer == null) return;
        bool changed = false;
        if (entry.unity_type == "NormalMap" && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            changed = true;
        }
        if (importer.sRGBTexture != entry.srgb)
        {
            importer.sRGBTexture = entry.srgb;
            changed = true;
        }
        if (changed) importer.SaveAndReimport();
    }

    private static void RemapMaterial(DeliveryState state, DeliveryAsset asset, DeliveryMaterial entry, Material material)
    {
        if (entry.meshes == null || asset.meshes == null) return;
        foreach (string meshName in entry.meshes)
        {
            foreach (DeliveryMesh mesh in asset.meshes)
            {
                if (!mesh.name.Equals(meshName, StringComparison.OrdinalIgnoreCase)) continue;
                ModelImporter importer = AssetImporter.GetAtPath(mesh.asset_path) as ModelImporter;
                if (importer == null) continue;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                AssetImporter.SourceAssetIdentifier identifier =
                    new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.name);
                importer.AddRemap(identifier, material);
                importer.SaveAndReimport();
                Debug.Log("[AssetPipelineHub] 材质映射：" + mesh.asset_path + " -> " + material.name);
            }
        }
    }

    private static Dictionary<string, string> ParseShaderSlots(string[] values)
    {
        Dictionary<string, string> result = new Dictionary<string, string>();
        if (values == null) return result;
        foreach (string value in values)
        {
            if (string.IsNullOrEmpty(value)) continue;
            string[] parts = value.Split(new[] { '=' }, 2);
            if (parts.Length == 2) result[parts[0].Trim()] = parts[1].Trim();
        }
        return result;
    }

    private static string ResolveShaderSlot(Material material, string textureType, string requestedSlot)
    {
        if (!string.IsNullOrEmpty(requestedSlot) && material.HasProperty(requestedSlot)) return requestedSlot;
        string[] candidates;
        switch (textureType)
        {
            case "_BC": candidates = new[] { "_BaseMap", "_MainTex" }; break;
            case "_N": candidates = new[] { "_NormalMap", "_BumpMap" }; break;
            case "_MRA": candidates = new[] { "_MetallicGlossMap", "_MaskMap" }; break;
            case "_AO": candidates = new[] { "_OcclusionMap", "_MetallicGlossMap", "_MaskMap" }; break;
            default: candidates = new[] { requestedSlot }; break;
        }
        foreach (string candidate in candidates)
        {
            if (!string.IsNullOrEmpty(candidate) && material.HasProperty(candidate)) return candidate;
        }
        return null;
    }

    private static string ResolveProjectAssetPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string path = NormalizeAssetPath(value.Trim());
        if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return "Assets/" + path.Substring(7);
        string projectRoot = NormalizeAssetPath(ProjectRoot());
        if (Path.IsPathRooted(path) && path.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
            return path.Substring(projectRoot.Length + 1);
        return path;
    }

    private static string ProjectRoot()
    {
        return Path.GetDirectoryName(Application.dataPath);
    }

    private static void EnsureAssetFolder(string path)
    {
        string normalized = NormalizeAssetPath(path);
        if (string.IsNullOrEmpty(normalized) || !normalized.StartsWith("Assets")) return;
        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string NormalizeAssetPath(string value)
    {
        return (value ?? "").Replace("\\", "/");
    }

    [Serializable]
    private class DeliveryState
    {
        public int schema_version;
        public string generated_at;
        public string material_source_mode;
        public string material_source_path;
        public string material_update_policy;
        public string[] shader_slots;
        public DeliveryAsset[] assets;
    }

    [Serializable]
    private class DeliveryAsset
    {
        public string asset_name;
        public DeliveryMesh[] meshes;
        public DeliveryMaterial[] materials;
    }

    [Serializable]
    private class DeliveryMesh
    {
        public string name;
        public string asset_path;
        public string source_material;
    }

    [Serializable]
    private class DeliveryMaterial
    {
        public string name;
        public string asset_path;
        public string[] meshes;
        public DeliveryTexture[] textures;
    }

    [Serializable]
    private class DeliveryTexture
    {
        public string type;
        public string asset_path;
        public bool srgb;
        public string unity_type;
    }
}
