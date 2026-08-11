using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
internal static class PandaPostUrpInstaller
{
    private const string InstallMenuPath = "Tools/Panda VFX/安装 Panda Post URP Renderer Feature";

    static PandaPostUrpInstaller()
    {
        EditorApplication.delayCall += InstallAutomatically;
    }

    [MenuItem(InstallMenuPath)]
    private static void InstallFromMenu()
    {
        int installedCount = InstallIntoAllRenderers();
        Debug.Log(installedCount > 0
            ? $"[Panda Post] 已向 {installedCount} 个 URP Renderer 安装 Renderer Feature。"
            : "[Panda Post] 所有 URP Renderer 均已安装 Renderer Feature。");
    }

    private static void InstallAutomatically()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += InstallAutomatically;
            return;
        }

        InstallIntoAllRenderers();
    }

    private static int InstallIntoAllRenderers()
    {
        int installedCount = 0;
        string[] rendererGuids = AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" });

        foreach (string guid in rendererGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(assetPath);
            if (rendererData == null || HasPandaPostFeature(rendererData))
            {
                continue;
            }

            PandaPostRendererFeature feature = ScriptableObject.CreateInstance<PandaPostRendererFeature>();
            feature.name = "Panda Post Process";
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            SerializedObject serializedRenderer = new SerializedObject(rendererData);
            SerializedProperty features = serializedRenderer.FindProperty("m_RendererFeatures");
            SerializedProperty featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");
            if (features == null || featureMap == null)
            {
                Object.DestroyImmediate(feature, true);
                Debug.LogError($"[Panda Post] 无法写入 URP Renderer：{assetPath}");
                continue;
            }

            serializedRenderer.Update();
            int featureIndex = features.arraySize;
            features.arraySize++;
            features.GetArrayElementAtIndex(featureIndex).objectReferenceValue = feature;
            featureMap.arraySize = features.arraySize;
            featureMap.GetArrayElementAtIndex(featureIndex).longValue = localId;
            serializedRenderer.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
            rendererData.SetDirty();
            installedCount++;
        }

        if (installedCount > 0)
        {
            AssetDatabase.SaveAssets();
        }

        return installedCount;
    }

    private static bool HasPandaPostFeature(UniversalRendererData rendererData)
    {
        foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
        {
            if (feature is PandaPostRendererFeature)
            {
                return true;
            }
        }

        return false;
    }
}
