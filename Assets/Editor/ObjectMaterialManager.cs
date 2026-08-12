using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

// 快速复制Unity对象和材质 - v2.0
// ChatGPT的搬运工 - SWY
public class ObjectMaterialManager : MonoBehaviour
{


    [MenuItem("GameObject/复制材质并重新命名", false, 10)]
    private static void DuplicateMaterialsForPrefab(MenuCommand menuCommand)
    {
        GameObject selectedObject = menuCommand.context as GameObject;
        if (selectedObject == null)
        {
            Debug.LogError("未选择任何Prefab。");
            return;
        }

        Renderer[] renderers = selectedObject.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogError("Prefab中未找到任何Renderer组件。");
            return;
        }

        string prefabName = selectedObject.name;

        Dictionary<Material, Material> materialMap = new Dictionary<Material, Material>();
        int total = renderers.Length;  // 总的Renderer数量
        int materialCounter = 0;       // 用于跟踪处理的材质数量
        int materialsCopied = 0;       // 记录复制的材质总数

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            bool materialsChanged = false;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null)
                {
                    if (!materialMap.ContainsKey(materials[i]))
                    {
                        // 获取材质的原始路径
                        string originalMaterialPath = AssetDatabase.GetAssetPath(materials[i]);

                        // 确保路径有效
                        if (string.IsNullOrEmpty(originalMaterialPath))
                        {
                            Debug.LogWarning($"材质 {materials[i].name} 的路径无效，跳过此材质。");
                            continue;
                        }

                        string originalMaterialDirectory = Path.GetDirectoryName(originalMaterialPath);

                        if (string.IsNullOrEmpty(originalMaterialDirectory))
                        {
                            Debug.LogWarning($"无法找到材质 {materials[i].name} 的目录，跳过此材质。");
                            continue;
                        }

                        // 根据递增命名规则生成新的材质名称
                        string newMaterialName = GetIncrementalMaterialName(materials[i].name, originalMaterialDirectory);
                        string newMaterialPath = $"{originalMaterialDirectory}/{newMaterialName}.mat";

                        // 复制材质
                        Material newMaterial = new Material(materials[i]);
                        AssetDatabase.CreateAsset(newMaterial, newMaterialPath);
                        materialMap[materials[i]] = newMaterial;
                        materialsCopied++;  // 增加复制计数
                    }

                    // 将材质替换为复制后的材质
                    materials[i] = materialMap[materials[i]];
                    materialsChanged = true;
                }
            }

            if (materialsChanged)
            {
                renderer.sharedMaterials = materials;
            }

            // 更新并显示进度条
            materialCounter++;
            EditorUtility.DisplayProgressBar("复制材质", $"正在复制材质 {materialCounter} / {total}", (float)materialCounter / total);
        }

        // 完成后移除进度条
        EditorUtility.ClearProgressBar();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"材质复制并重新命名完成，共复制了 {materialsCopied} 个材质。");
    }

    // 获取递增命名的材质名
    private static string GetIncrementalMaterialName(string originalMaterialName, string assetPath)
    {
        // 找到材质名称的最后一个数字部分并递增
        string baseName = originalMaterialName.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        int number = 1;

        string[] files = Directory.GetFiles(assetPath, $"{baseName}*.mat");
        foreach (string file in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.StartsWith(baseName) && int.TryParse(fileName.Substring(baseName.Length), out int parsedNumber))
            {
                number = Mathf.Max(number, parsedNumber + 1);
            }
        }

        return $"{baseName}{number.ToString("D2")}";
    }
    private const bool placeAbove = false; // 固定为放在下方
    private static NamingRule currentNamingRule;
    private static string customSuffix;

    private const string NamingRuleKey = "ObjectMaterialManager_NamingRule";
    private const string CustomSuffixKey = "ObjectMaterialManager_CustomSuffix";

    public enum NamingRule
    {
        Default,         // 默认：保持名称一致
        Incremental,     // 递增命名
        CustomSuffix     // 自定义后缀（支持递增）
    }

    [InitializeOnLoadMethod]
    private static void LoadSettings()
    {
        currentNamingRule = (NamingRule)EditorPrefs.GetInt(NamingRuleKey, (int)NamingRule.Default);
        customSuffix = EditorPrefs.GetString(CustomSuffixKey, "_Copy");
    }

    private static void SaveSettings()
    {
        EditorPrefs.SetInt(NamingRuleKey, (int)currentNamingRule);
        EditorPrefs.SetString(CustomSuffixKey, customSuffix);
    }

    [MenuItem("Tools/通用材质管理/复制对象以及引用材质", false, 100)]
    public static void BatchBackupObjects()
    {
        var selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            Debug.LogWarning("未选择任何对象!");
            return;
        }

        foreach (var selectedObject in selectedObjects)
        {
            if (!HasSupportedRenderer(selectedObject))
            {
                Debug.LogWarning($"对象 '{selectedObject.name}' 不支持渲染组件（粒子系统、Mesh、Trail等），已跳过!");
                continue;
            }

            GameObject backup = Instantiate(selectedObject);
            backup.transform.SetParent(selectedObject.transform.parent);
            backup.transform.localPosition = selectedObject.transform.localPosition;
            backup.transform.localRotation = selectedObject.transform.localRotation;
            backup.transform.localScale = selectedObject.transform.localScale;

            Renderer renderer = backup.GetComponent<Renderer>();
            if (renderer == null) continue;

            Material[] newMaterials = CopyMaterials(renderer.sharedMaterials);

            // 根据当前命名规则设置对象名称
            backup.name = GetBackupName(selectedObject.name, selectedObject.transform.parent);

            renderer.sharedMaterials = newMaterials;

            int siblingIndex = selectedObject.transform.GetSiblingIndex();
            backup.transform.SetSiblingIndex(placeAbove ? siblingIndex : siblingIndex + 1);

            // 标记更改，确保 Unity 可以检测到
            EditorUtility.SetDirty(backup);
            EditorSceneManager.MarkSceneDirty(backup.scene);

            // 整合材质名称并输出完成信息
            string materialNames = string.Join(", ", newMaterials.Select(mat => mat != null ? mat.name : "null"));
            Debug.Log($"对象 '{backup.name}' 和材质复制完成，材质名称: {materialNames}");
        }
    }

    [MenuItem("Tools/通用材质管理/只复制材质并引用", false, 101)]
    public static void CopyMaterialsOnly()
    {
        var selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            Debug.LogWarning("未选择任何对象!");
            return;
        }

        foreach (var selectedObject in selectedObjects)
        {
            if (!HasSupportedRenderer(selectedObject))
            {
                Debug.LogWarning($"对象 '{selectedObject.name}' 不支持渲染组件（粒子系统、Mesh、Trail等），已跳过!");
                continue;
            }

            Renderer renderer = selectedObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogWarning($"对象 '{selectedObject.name}' 无法找到 Renderer，已跳过!");
                continue;
            }

            Material[] newMaterials = CopyMaterials(renderer.sharedMaterials);

            renderer.sharedMaterials = newMaterials;

            // 整合材质名称并输出完成信息
            string materialNames = string.Join(", ", newMaterials.Select(mat => mat != null ? mat.name : "null"));
            Debug.Log($"对象 '{selectedObject.name}' 的材质复制完成，材质名称: {materialNames}");

            // 标记材质更改
            EditorUtility.SetDirty(selectedObject);
        }
    }

    [MenuItem("Tools/通用材质管理/设置复制对象命名规则", false, 200)]
    public static void OpenNamingRuleWindow()
    {
        NamingRuleWindow.ShowWindow();
    }

    private static string GetBackupName(string originalName, Transform parent)
    {
        switch (currentNamingRule)
        {
            case NamingRule.Default:
                return originalName;
            case NamingRule.Incremental:
                return GenerateIncrementalName(originalName, parent);
            case NamingRule.CustomSuffix:
                return GenerateCustomSuffixName(originalName, parent);
            default:
                return originalName;
        }
    }

    private static string GenerateIncrementalName(string baseName, Transform parent)
    {
        string objectBaseName = Regex.Replace(baseName, @"_\d+$", "");
        Regex objectRegex = new Regex(@"_(\d+)$");
        int maxObjectNumber = 0;
        int objectNumberLength = 0;

        if (parent != null)
        {
            foreach (Transform sibling in parent)
            {
                if (sibling.name.StartsWith(objectBaseName))
                {
                    Match match = objectRegex.Match(sibling.name);
                    if (match.Success)
                    {
                        int number = int.Parse(match.Groups[1].Value);
                        objectNumberLength = match.Groups[1].Value.Length;
                        if (number > maxObjectNumber)
                        {
                            maxObjectNumber = number;
                        }
                    }
                }
            }
        }

        int newObjectNumber = maxObjectNumber + 1;
        return objectBaseName + "_" + newObjectNumber.ToString(new string('0', objectNumberLength));
    }

    private static string GenerateCustomSuffixName(string baseName, Transform parent)
    {
        string objectBaseName = baseName + customSuffix;
        Regex suffixRegex = new Regex(Regex.Escape(objectBaseName) + @"_(\d+)$");
        int maxSuffixNumber = 0;
        int suffixNumberLength = 0;

        if (parent != null)
        {
            foreach (Transform sibling in parent)
            {
                Match match = suffixRegex.Match(sibling.name);
                if (match.Success)
                {
                    int number = int.Parse(match.Groups[1].Value);
                    suffixNumberLength = match.Groups[1].Value.Length;
                    if (number > maxSuffixNumber)
                    {
                        maxSuffixNumber = number;
                    }
                }
            }
        }

        int newSuffixNumber = maxSuffixNumber + 1;
        return objectBaseName + "_" + newSuffixNumber.ToString(new string('0', suffixNumberLength > 0 ? suffixNumberLength : 2));
    }

    private static Material[] CopyMaterials(Material[] originalMaterials)
    {
        if (originalMaterials == null || originalMaterials.Length == 0)
        {
            Debug.LogWarning("该对象没有使用材质!");
            return new Material[0];
        }

        Material[] newMaterials = new Material[originalMaterials.Length];
        Dictionary<string, Material> materialMap = new Dictionary<string, Material>();

        for (int i = 0; i < originalMaterials.Length; i++)
        {
            if (originalMaterials[i] != null)
            {
                string materialPath = AssetDatabase.GetAssetPath(originalMaterials[i]);
                if (string.IsNullOrEmpty(materialPath))
                {
                    Debug.LogWarning("材质没有有效的路径: " + originalMaterials[i].name);
                    continue;
                }

                if (!materialMap.ContainsKey(materialPath))
                {
                    Material newMaterial = new Material(originalMaterials[i]);
                    string materialDirectory = Path.GetDirectoryName(materialPath);
                    if (string.IsNullOrEmpty(materialDirectory))
                    {
                        Debug.LogWarning("材质的目录路径无效: " + materialPath);
                        continue;
                    }

                    string originalMaterialName = Path.GetFileNameWithoutExtension(materialPath);
                    string baseName = Regex.Replace(originalMaterialName, @"_\d+$", "");
                    Regex regex = new Regex(@"_(\d+)$");
                    int maxNumber = 0;
                    int numberLength = 0;

                    string[] allFiles = Directory.GetFiles(materialDirectory, "*.mat");
                    foreach (string file in allFiles)
                    {
                        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                        Match match = regex.Match(fileNameWithoutExtension);

                        if (match.Success && fileNameWithoutExtension.StartsWith(baseName))
                        {
                            int number = int.Parse(match.Groups[1].Value);
                            numberLength = match.Groups[1].Value.Length;
                            if (number > maxNumber)
                            {
                                maxNumber = number;
                            }
                        }
                    }

                    int newNumber = maxNumber + 1;
                    string newMaterialName = baseName + "_" + newNumber.ToString(new string('0', numberLength)) + ".mat";

                    string newMaterialPath = Path.Combine(materialDirectory, newMaterialName);
                    AssetDatabase.CreateAsset(newMaterial, newMaterialPath);

                    materialMap[materialPath] = newMaterial;
                    newMaterials[i] = newMaterial;
                }
                else
                {
                    newMaterials[i] = materialMap[materialPath];
                }
            }
        }

        return newMaterials;
    }

    private static bool HasSupportedRenderer(GameObject obj)
    {
        return obj.GetComponent<Renderer>() != null;
    }

    public class NamingRuleWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            var window = GetWindow<NamingRuleWindow>("命名规则设置");
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("选择命名规则", EditorStyles.boldLabel);

            if (GUILayout.Toggle(currentNamingRule == NamingRule.Default, "默认（与原名称一致）"))
                currentNamingRule = NamingRule.Default;

            if (GUILayout.Toggle(currentNamingRule == NamingRule.Incremental, "递增命名"))
                currentNamingRule = NamingRule.Incremental;

            if (GUILayout.Toggle(currentNamingRule == NamingRule.CustomSuffix, "自定义后缀"))
            {
                currentNamingRule = NamingRule.CustomSuffix;
                customSuffix = EditorGUILayout.TextField("后缀", customSuffix);
            }

            if (GUILayout.Button("保存"))
            {
                SaveSettings();
                Close();
            }
        }
    }
}
