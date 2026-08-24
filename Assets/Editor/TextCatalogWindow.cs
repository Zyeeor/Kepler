#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 文本目录查看器（菜单：Tools/Kepler/Text Catalog Viewer）。
///
/// 用途：
///   - 便捷查看/搜索所有玩家可见文本（TextCatalog 资产）；
///   - 跳转到资产条目、直接编辑；
///   - 一键导出全部文本为 CSV（策划审阅 / 后端字段对齐）。
/// </summary>
public class TextCatalogWindow : EditorWindow
{
    [MenuItem("Tools/Kepler/Text Catalog Viewer")]
    public static void Open()
    {
        GetWindow<TextCatalogWindow>("Text Catalog");
    }

    TextCatalog catalog;
    string search = "";
    Vector2 scroll;
    Object catalogAsset;

    void OnEnable()
    {
        catalog = TextCatalog.Instance;
        catalogAsset = catalog;
        if (catalogAsset != null)
            EditorUtility.SetDirty(catalogAsset); // 确保实例可编辑显示
    }

    void OnGUI()
    {
        EditorGUILayout.Space(4);

        // ── 资产选择 ──
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("文本目录资产", GUILayout.Width(90));
        var next = (TextCatalog)EditorGUILayout.ObjectField(catalog, typeof(TextCatalog), false);
        if (next != catalog)
        {
            catalog = next;
            catalogAsset = catalog;
        }
        if (GUILayout.Button("新建", GUILayout.Width(48)))
        {
            catalog = CreateCatalogAsset();
            catalogAsset = catalog;
        }
        EditorGUILayout.EndHorizontal();

        // ── 搜索 + 统计 ──
        EditorGUILayout.BeginHorizontal();
        search = EditorGUILayout.TextField("搜索", search);
        if (catalog != null)
        {
            GUILayout.Label($"{catalog.entries.Count} 条", EditorStyles.boldLabel);
            if (GUILayout.Button("导出 CSV", GUILayout.Width(80)))
                ExportCsv(catalog);
        }
        EditorGUILayout.EndHorizontal();

        if (catalog == null)
        {
            EditorGUILayout.HelpBox("未找到 TextCatalog 资产（运行时 Resource 路径：Resources/Text/TextCatalog）。\n点击“新建”创建。", MessageType.Info);
            return;
        }

        // ── 条目列表 ──
        scroll = EditorGUILayout.BeginScrollView(scroll);
        string q = search.Trim().ToLowerInvariant();
        int shown = 0;
        for (int i = 0; i < catalog.entries.Count; i++)
        {
            var e = catalog.entries[i];
            if (e == null) continue;
            if (!string.IsNullOrEmpty(q)
                && !(e.key != null && e.key.ToLowerInvariant().Contains(q))
                && !(e.text != null && e.text.ToLowerInvariant().Contains(q)))
                continue;
            shown++;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(e.key, EditorStyles.boldLabel);
            if (GUILayout.Button("↑", GUILayout.Width(24))) { int j = i; if (j > 0) Swap(j, j - 1); }
            if (GUILayout.Button("↓", GUILayout.Width(24))) { int j = i; if (j < catalog.entries.Count - 1) Swap(j, j + 1); }
            if (GUILayout.Button("删除", GUILayout.Width(44)))
            {
                catalog.entries.RemoveAt(i);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                return;
            }
            EditorGUILayout.EndHorizontal();
            e.text = EditorGUILayout.TextArea(e.text, GUILayout.MinHeight(40));
            EditorGUILayout.LabelField("Mythic（预留）", EditorStyles.miniLabel);
            e.mythicText = EditorGUILayout.TextArea(e.mythicText, GUILayout.MinHeight(32));
            EditorGUILayout.LabelField("System（预留）", EditorStyles.miniLabel);
            e.systemText = EditorGUILayout.TextArea(e.systemText, GUILayout.MinHeight(32));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"显示 {shown} / {catalog.entries.Count}", EditorStyles.miniLabel);
        if (GUILayout.Button("+ 添加条目", GUILayout.Width(100)))
        {
            catalog.entries.Add(new TextEntry { key = "new.key", text = "新文本" });
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }
        EditorGUILayout.EndHorizontal();

        if (GUI.changed && catalogAsset != null)
            EditorUtility.SetDirty(catalogAsset);
    }

    void Swap(int a, int b)
    {
        var t = catalog.entries[a];
        catalog.entries[a] = catalog.entries[b];
        catalog.entries[b] = t;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    /// <summary>新建 TextCatalog 资产（默认路径 Assets/Resources/Text/ 下；已存在则复用）。</summary>
    static TextCatalog CreateCatalogAsset()
    {
        const string dir = "Assets/Resources/Text";
        const string path = dir + "/TextCatalog.asset";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var existing = AssetDatabase.LoadAssetAtPath<TextCatalog>(path);
        if (existing != null)
        {
            Selection.activeObject = existing;
            return existing;
        }
        var created = ScriptableObject.CreateInstance<TextCatalog>();
        AssetDatabase.CreateAsset(created, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = created;
        return created;
    }

    /// <summary>导出全部文本为 CSV（key,text,mythic,system）。</summary>
    static void ExportCsv(TextCatalog c)
    {
        if (c == null) return;
        string path = EditorUtility.SaveFilePanel("导出文本目录", "Assets/Resources/Text", "TextCatalog.csv", "csv");
        if (string.IsNullOrEmpty(path)) return;
        var sb = new StringBuilder();
        sb.AppendLine("key,text,mythicText,systemText");
        foreach (var e in c.entries)
        {
            if (e == null) continue;
            sb.Append(CsvField(e.key)).Append(',');
            sb.Append(CsvField(e.text)).Append(',');
            sb.Append(CsvField(e.mythicText)).Append(',');
            sb.AppendLine(CsvField(e.systemText));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        EditorUtility.RevealInFinder(path);
        Debug.Log($"[TextCatalog] 已导出 {c.entries.Count} 条文本 → {path}");
    }

    static string CsvField(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
#endif
