using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

internal static class VfxTemplateMenu
{
    private const string TemplateRoot = "Assets/Art folder/VFX/VFX-Tex/M";
    private const string MaterialCopyRoot = "Assets/Art folder/VFX/VFX-Tex/MaterialCopies";
    private const string MenuPath = "GameObject/VFX/添加子特效模板...";

    [MenuItem(MenuPath, false, 20)]
    private static void OpenTemplatePicker(MenuCommand command)
    {
        GameObject parent = GetTarget(command);
        if (parent == null)
        {
            return;
        }

        VfxTemplatePickerWindow.Open(parent);
    }

    private sealed class VfxTemplatePickerWindow : EditorWindow
    {
        private readonly List<string> assetPaths = new List<string>();
        private int parentInstanceId;
        private Vector2 scrollPosition;

        public static void Open(GameObject parent)
        {
            VfxTemplatePickerWindow window = CreateInstance<VfxTemplatePickerWindow>();
            window.titleContent = new GUIContent("添加子特效模板");
            window.parentInstanceId = parent.GetInstanceID();
            window.minSize = new Vector2(320f, 240f);
            window.maxSize = new Vector2(520f, 640f);
            window.LoadTemplates();
            window.ShowUtility();
            window.Focus();
        }

        private void LoadTemplates()
        {
            assetPaths.Clear();
            if (!AssetDatabase.IsValidFolder(TemplateRoot))
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { TemplateRoot });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null)
                {
                    assetPaths.Add(assetPath);
                }
            }

            assetPaths.Sort(StringComparer.OrdinalIgnoreCase);
            Repaint();
        }

        private void OnGUI()
        {
            GameObject parent = EditorUtility.InstanceIDToObject(parentInstanceId) as GameObject;
            if (parent == null)
            {
                EditorGUILayout.HelpBox("目标母粒子系统已不存在，请关闭窗口后重新选择。", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("添加到", parent.name);
            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("特效模板", EditorStyles.boldLabel);
            if (GUILayout.Button("刷新", GUILayout.Width(60f)))
            {
                LoadTemplates();
            }
            EditorGUILayout.EndHorizontal();

            if (!AssetDatabase.IsValidFolder(TemplateRoot))
            {
                EditorGUILayout.HelpBox($"模板文件夹不存在：\n{TemplateRoot}", MessageType.Error);
                return;
            }

            if (assetPaths.Count == 0)
            {
                EditorGUILayout.HelpBox("模板目录中没有找到 Prefab。", MessageType.Info);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            string currentCategory = string.Empty;

            foreach (string assetPath in assetPaths)
            {
                string relativePath = assetPath.Substring(TemplateRoot.Length).TrimStart('/');
                string displayPath = Path.ChangeExtension(relativePath, null).Replace('\\', '/');
                int separatorIndex = displayPath.LastIndexOf('/');
                string category = separatorIndex >= 0
                    ? displayPath.Substring(0, separatorIndex)
                    : "未分类";
                string templateName = separatorIndex >= 0
                    ? displayPath.Substring(separatorIndex + 1)
                    : displayPath;

                if (category != currentCategory)
                {
                    if (!string.IsNullOrEmpty(currentCategory))
                    {
                        EditorGUILayout.Space(6f);
                    }

                    currentCategory = category;
                    EditorGUILayout.LabelField(currentCategory, EditorStyles.boldLabel);
                }

                if (GUILayout.Button(templateName, GUILayout.Height(26f)))
                {
                    AddTemplateAsChild(assetPath, parent);
                    Close();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateShowTemplateMenu(MenuCommand command)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return false;
        }

        GameObject target = GetTarget(command);
        return target != null &&
               !EditorUtility.IsPersistent(target) &&
               target.GetComponent<ParticleSystem>() != null;
    }

    private static GameObject GetTarget(MenuCommand command)
    {
        return command.context as GameObject ?? Selection.activeGameObject;
    }

    private static void AddTemplateAsChild(string assetPath, GameObject parent)
    {
        if (parent == null)
        {
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            Debug.LogError($"[VfxTemplateMenu] 无法加载特效模板: {assetPath}");
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent.transform) as GameObject;
        if (instance == null)
        {
            Debug.LogError($"[VfxTemplateMenu] 无法实例化特效模板: {assetPath}");
            return;
        }

        const string undoName = "添加子特效模板";
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(undoName);
        Undo.RegisterCreatedObjectUndo(instance, undoName);
        PrefabUtility.UnpackPrefabInstance(
            instance,
            PrefabUnpackMode.Completely,
            InteractionMode.AutomatedAction);
        CopyAndAssignMaterials(instance, undoName);

        Undo.RecordObject(instance.transform, undoName);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        Undo.CollapseUndoOperations(undoGroup);

        Selection.activeGameObject = instance;
        EditorGUIUtility.PingObject(instance);
    }

    private static void CopyAndAssignMaterials(GameObject instance, string undoName)
    {
        if (!EnsureMaterialCopyFolder())
        {
            return;
        }

        Dictionary<Material, Material> materialCopies = new Dictionary<Material, Material>();
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            bool materialsChanged = false;

            for (int i = 0; i < materials.Length; i++)
            {
                Material sourceMaterial = materials[i];
                if (sourceMaterial == null)
                {
                    continue;
                }

                materials[i] = GetOrCreateMaterialCopy(
                    sourceMaterial,
                    instance.name,
                    materialCopies,
                    undoName);
                materialsChanged = true;
            }

            ParticleSystemRenderer particleRenderer = renderer as ParticleSystemRenderer;
            Material sourceTrailMaterial = particleRenderer != null
                ? particleRenderer.trailMaterial
                : null;
            Material copiedTrailMaterial = sourceTrailMaterial != null
                ? GetOrCreateMaterialCopy(
                    sourceTrailMaterial,
                    instance.name,
                    materialCopies,
                    undoName)
                : null;
            bool trailMaterialChanged = particleRenderer != null &&
                                        sourceTrailMaterial != null &&
                                        sourceTrailMaterial != copiedTrailMaterial;

            if (!materialsChanged && !trailMaterialChanged)
            {
                continue;
            }

            Undo.RecordObject(renderer, undoName);
            if (materialsChanged)
            {
                renderer.sharedMaterials = materials;
            }

            if (trailMaterialChanged)
            {
                particleRenderer.trailMaterial = copiedTrailMaterial;
            }
        }
    }

    private static Material GetOrCreateMaterialCopy(
        Material sourceMaterial,
        string instanceName,
        Dictionary<Material, Material> materialCopies,
        string undoName)
    {
        if (materialCopies.TryGetValue(sourceMaterial, out Material materialCopy))
        {
            return materialCopy;
        }

        string fileName = $"{SanitizeFileName(instanceName)}_{SanitizeFileName(sourceMaterial.name)}.mat";
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{MaterialCopyRoot}/{fileName}");
        materialCopy = new Material(sourceMaterial)
        {
            name = Path.GetFileNameWithoutExtension(assetPath)
        };

        AssetDatabase.CreateAsset(materialCopy, assetPath);
        Undo.RegisterCreatedObjectUndo(materialCopy, undoName);
        materialCopies.Add(sourceMaterial, materialCopy);
        return materialCopy;
    }

    private static bool EnsureMaterialCopyFolder()
    {
        if (AssetDatabase.IsValidFolder(MaterialCopyRoot))
        {
            return true;
        }

        string guid = AssetDatabase.CreateFolder(
            "Assets/Art folder/VFX/VFX-Tex",
            "MaterialCopies");
        if (!string.IsNullOrEmpty(guid))
        {
            return true;
        }

        Debug.LogError($"[VfxTemplateMenu] 无法创建材质副本目录: {MaterialCopyRoot}");
        return false;
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidCharacter, '_');
        }

        return string.IsNullOrWhiteSpace(fileName) ? "Material" : fileName;
    }
}
