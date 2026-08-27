#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 词缀表配置工具（菜单：Kepler > Cards > Epithet 词缀表）。
///
/// 存在理由（Owner B2 要求"方便策划配置"）：
///   词缀数据天然散在三处——卡在 CardLibrary、结构在 CardEpithetCatalog、文本在 TextCatalog。
///   本窗口把三处收敛为**一个入口**，策划无需手改任何 .asset、无需懂 Text Key 规则。
///
/// 功能：
///   1. 一屏总览 49 张 Sin 卡（effectId / 卡名 / axis / slot / weight / 明线词 / 暗线词）
///   2. 一键从 CardLibrary 同步卡池（新增卡自动出现，按默认档预填权重）
///   3. 词文本直填，保存时自动写入 TextCatalog 的 card.&lt;id&gt;.epithet（策划不碰文本资产）
///   4. 实时预览：输入卡组合即时看到 Mythic / System / Neutral 三版名字
///   5. 七项自动校验：字数 / 同罪重字 / 跨罪撞词 / 品质词 / 数值暴露 / 缺词 / 轴覆盖
///   6. 一键 bump 词表版本（epithetRev）
/// </summary>
/// <summary>
/// 首次导入器：编译完成后自动把种子词表灌入词缀目录（仅一次）。
///
/// 存在理由：MCP script-execute 在部分环境下不可用（工具注册丢失），
/// 无法由 Agent 远程触发导入。改为编译即自举，策划打开工程就能看到预填词表。
///
/// 行为：
///   - 只在词缀目录"一条词都没有"时执行（SessionState 保证每个编辑器会话只跑一次）；
///   - 只填空值，绝不覆盖策划后续的手工修改；
///   - 导入结果打一条 Log，可在 Console 用 "[Epithet]" 过滤查看。
/// </summary>
[InitializeOnLoad]
public static class CardEpithetSeedImporter
{
    const string SessionKey = "CardEpithet_SeedImported";

    static CardEpithetSeedImporter()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);

        EditorApplication.delayCall += TryImportOnce;
    }

    static void TryImportOnce()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryImportOnce;   // 编译/播放中，稍后重试
            return;
        }

        var catalog = AssetDatabase.LoadAssetAtPath<CardEpithetCatalog>(CardEpithetEditorWindow.CatalogPath);
        if (catalog == null) return;   // 尚未创建目录：等策划用工具创建后再手动导入

        // 已有词 → 说明策划已配置过，不再动它
        // 注意：这里直接读资产而非 TextCatalog.Instance，避免 Resources 未就绪导致的误判
        var tc = AssetDatabase.LoadAssetAtPath<TextCatalog>(CardEpithetEditorWindow.TextCatalogPath);
        if (tc != null)
        {
            foreach (var e in catalog.entries)
            {
                if (e == null) continue;
                var te = CardEpithetEditorWindow.FindEntryInSectionStatic(tc, CardEpithetCatalog.ResolveTextKey(e));
                if (te != null && !string.IsNullOrEmpty(te.mythicText)) return;
            }
        }

        string report = CardEpithetEditorWindow.ImportSeedPublic(catalog, tc, false);
        Debug.Log("[Epithet] 首次编译自动导入种子词表：\n" + report);
    }
}

public class CardEpithetEditorWindow : EditorWindow
{
    internal const string CatalogPath = "Assets/Resources/CardEpithetCatalog.asset";
    internal const string TextCatalogPath = "Assets/Resources/Text/TextCatalog.asset";
    internal const string EpithetSectionName = "card.epithet";
    internal const string SinSectionName = "concept.sin";

    // 卡池同步写入的哨兵权重：种子导入时视为"未配置"，允许覆盖
    const float SentinelWeightBasic = 35f;
    const float SentinelWeightTypeGrowth = 1000f;

    // 禁用词（品质词 / 生僻字示例；设计文档 §4.2）
    static readonly string[] BannedQualityWords = { "传说", "稀有", "至尊", "神级", "史诗", "神话", "传奇" };
    static readonly string[] BannedNumericWords = { "一", "二", "三", "十", "百", "千", "秒", "分" };

    CardEpithetCatalog catalog;
    TextCatalog textCatalog;

    Vector2 tableScroll;
    Vector2 reportScroll;
    string validationReport = "";
    string previewInput = "";
    string previewResult = "";
    bool showOnlyMissing;

    [MenuItem("Kepler/Cards/Epithet 导入种子词表")]
    public static void ImportSeedFromMenu()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<CardEpithetCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogWarning("[Epithet] 未找到词缀目录，请先打开「Epithet 词缀表」创建并同步卡池。");
            return;
        }
        var tc = AssetDatabase.LoadAssetAtPath<TextCatalog>(TextCatalogPath);
        string report = ImportSeed(catalog, tc, forceOverwrite: false);
        Debug.Log("[Epithet 导入种子词表]\n" + report);
    }

    [MenuItem("Kepler/Cards/Epithet 自校验")]
    public static void RunSelfTest()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<CardEpithetCatalog>(CatalogPath);
        string report = CardEpithetSelfTest.Run(catalog);
        Debug.Log("[Epithet 自校验]\n" + report);
        Debug.Log(report.Contains("FAIL") ? "<color=red>存在失败用例，见上方报告</color>" : "<color=green>全部通过</color>");
    }

    [MenuItem("Kepler/Cards/Epithet 词缀表")]
    public static void Open()
    {
        var w = GetWindow<CardEpithetEditorWindow>("词缀表");
        w.minSize = new Vector2(1000f, 640f);
        w.Show();
    }

    void OnEnable()
    {
        catalog = AssetDatabase.LoadAssetAtPath<CardEpithetCatalog>(CatalogPath);
        textCatalog = AssetDatabase.LoadAssetAtPath<TextCatalog>(TextCatalogPath);
    }

    void OnGUI()
    {
        DrawAssetBar();
        EditorGUILayout.Space(4f);

        if (catalog == null)
        {
            EditorGUILayout.HelpBox(
                "未找到词缀目录资产：" + CatalogPath + "\n请点击下方按钮创建。",
                MessageType.Warning);
            if (GUILayout.Button("创建 CardEpithetCatalog", GUILayout.Height(28f))) CreateCatalog();
            return;
        }

        DrawToolbar();
        EditorGUILayout.Space(4f);
        DrawTable();
        EditorGUILayout.Space(6f);
        DrawPreview();
        EditorGUILayout.Space(6f);
        DrawValidation();
    }

    // ── 顶部资产栏 ──

    void DrawAssetBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("词缀目录", EditorStyles.boldLabel, GUILayout.Width(60f));
        catalog = (CardEpithetCatalog)EditorGUILayout.ObjectField(catalog, typeof(CardEpithetCatalog), false);
        EditorGUILayout.LabelField("文本目录", EditorStyles.boldLabel, GUILayout.Width(60f));
        textCatalog = (TextCatalog)EditorGUILayout.ObjectField(textCatalog, typeof(TextCatalog), false);
        EditorGUILayout.EndHorizontal();
    }

    // ── 工具条 ──

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("同步卡池", GUILayout.Height(26f), GUILayout.Width(100f)))
            SyncFromCardLibrary();

        if (GUILayout.Button("保存", GUILayout.Height(26f), GUILayout.Width(80f)))
            Save();

        if (GUILayout.Button("校验", GUILayout.Height(26f), GUILayout.Width(80f)))
            RunValidation();

        if (GUILayout.Button("bump 版本", GUILayout.Height(26f), GUILayout.Width(90f)))
            BumpRev();

        GUILayout.FlexibleSpace();
        showOnlyMissing = GUILayout.Toggle(showOnlyMissing, "只看缺词", GUILayout.Height(26f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("词数上限", GUILayout.Width(60f));
        catalog.maxEpithetCount = EditorGUILayout.IntField(catalog.maxEpithetCount, GUILayout.Width(40f));
        EditorGUILayout.LabelField("连接词", GUILayout.Width(45f));
        catalog.connector = EditorGUILayout.TextField(catalog.connector, GUILayout.Width(50f));
        EditorGUILayout.LabelField("词表版本 Rev", GUILayout.Width(80f));
        catalog.epithetRev = EditorGUILayout.IntField(catalog.epithetRev, GUILayout.Width(50f));
        EditorGUILayout.LabelField($"条目 {catalog.entries.Count}", GUILayout.Width(80f));
        EditorGUILayout.EndHorizontal();
    }

    // ── 主表格 ──

    void DrawTable()
    {
        EditorGUILayout.LabelField("词缀条目（明线词 2 字 / 同罪不重字 / 禁品质词）", EditorStyles.boldLabel);

        // 表头
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        Label("effectId", 90f);
        Label("卡名", 130f);
        Label("Axis", 90f);
        Label("Slot", 80f);
        Label("Weight", 60f);
        Label("明线词(Mythic)", 100f);
        Label("暗线词(System)", 120f);
        Label("中性词", 80f);
        EditorGUILayout.EndHorizontal();

        tableScroll = EditorGUILayout.BeginScrollView(tableScroll, GUILayout.Height(260f));

        var lib = CardLibrary.Instance;
        for (int i = 0; i < catalog.entries.Count; i++)
        {
            var e = catalog.entries[i];
            if (e == null) continue;

            if (showOnlyMissing && HasWord(e)) continue;

            var card = lib != null ? lib.FindCard(e.effectId) : null;
            string cardName = card != null ? card.ResolveCardName() : "(卡池缺失)";

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(e.effectId, GUILayout.Width(90f));
            EditorGUILayout.LabelField(cardName, GUILayout.Width(130f));
            e.axis = (CardEpithetAxis)EditorGUILayout.EnumPopup(e.axis, GUILayout.Width(90f));
            e.slot = (CardEpithetSlot)EditorGUILayout.EnumPopup(e.slot, GUILayout.Width(80f));
            e.weight = EditorGUILayout.FloatField(e.weight, GUILayout.Width(60f));

            var entry = FindOrPeekTextEntry(e);
            entry.mythicText = EditorGUILayout.TextField(entry.mythicText, GUILayout.Width(100f));
            entry.systemText = EditorGUILayout.TextField(entry.systemText, GUILayout.Width(120f));
            entry.text = EditorGUILayout.TextField(entry.text, GUILayout.Width(80f));

            if (GUILayout.Button("×", GUILayout.Width(22f)))
            {
                catalog.entries.RemoveAt(i);
                RemoveTextEntry(CardEpithetCatalog.ResolveTextKey(e));
                GUI.changed = true;
                break;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    // ── 实时预览 ──

    void DrawPreview()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("实时预览（输入 effectId，逗号或空格分隔）", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        previewInput = EditorGUILayout.TextField(previewInput);
        if (GUILayout.Button("生成", GUILayout.Width(70f))) DoPreview();
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(previewResult))
            EditorGUILayout.HelpBox(previewResult, MessageType.None);
        EditorGUILayout.EndVertical();
    }

    void DoPreview()
    {
        if (string.IsNullOrWhiteSpace(previewInput)) { previewResult = ""; return; }

        var ids = new List<string>();
        foreach (var raw in previewInput.Split(new[] { ',', ' ', '、', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            ids.Add(raw.Trim());

        var words = CardEpithetGenerator.Generate(ids, catalog);
        int valid = CardEpithetGenerator.CountValid(ids, catalog);

        string sin = InferSin(ids);
        var sb = new StringBuilder();
        sb.AppendLine("中性词序列：" + (words.Length > 0 ? string.Join(" | ", words) : "(无)"));
        sb.AppendLine("有效卡数：" + valid);
        sb.AppendLine("Mythic ：" + FormatForLine(words, valid, sin, TextLinePreference.Mythic));
        sb.AppendLine("System ：" + FormatForLine(words, valid, sin, TextLinePreference.System));
        sb.AppendLine("Neutral：" + FormatForLine(words, valid, sin, TextLinePreference.Neutral));
        previewResult = sb.ToString();
    }

    /// <summary>按指定线格式化（预览用，绕过运行时全局显示线）。</summary>
    string FormatForLine(string[] words, int valid, string sin, TextLinePreference line)
    {
        if (words == null || words.Length == 0) return SinLabel(sin, line) + (line == TextLinePreference.System ? " · Rev.0" : "");

        var mapped = new string[words.Length];
        for (int i = 0; i < words.Length; i++)
        {
            var e = FindEntryByNeutral(words[i]);
            var te = e != null ? FindTextEntry(CardEpithetCatalog.ResolveTextKey(e)) : null;
            mapped[i] = te == null ? words[i]
                : line == TextLinePreference.Mythic ? te.MythicText
                : line == TextLinePreference.System ? te.SystemText
                : te.NeutralText;
        }

        string conn = string.IsNullOrEmpty(catalog.connector) ? "之" : catalog.connector;
        if (line == TextLinePreference.System)
            return $"{SinLabel(sin, line)} · {string.Join("/", mapped)} · Rev.{valid}";
        if (line == TextLinePreference.Mythic)
            return string.Concat(mapped) + conn + SinLabel(sin, line);
        return $"{SinLabel(sin, line)}（{valid} 卡构筑）";
    }

    static string SinLabel(string sinWire, TextLinePreference line)
    {
        // 预览态不依赖 NarrativeDisplay，直接用 TextCatalog 三线
        var tc = TextCatalog.Instance;
        if (tc != null)
        {
            var ent = FindEntryGlobal(tc, "concept.sin." + sinWire);
            if (ent != null)
                return line == TextLinePreference.System ? ent.SystemText : ent.MythicText;
        }
        return line == TextLinePreference.System ? sinWire + " Carrier" : sinWire;
    }

    static string InferSin(List<string> ids)
    {
        if (ids.Count == 0) return "pride";
        var parts = ids[0].Split('-');
        if (parts.Length == 0) return "pride";
        switch (parts[0].ToLowerInvariant())
        {
            case "pr": return "pride";
            case "sl": return "sloth";
            case "gl": return "gluttony";
            case "en": return "envy";
            case "wr": return "wrath";
            case "gr": return "greed";
            case "lu": return "lust";
            default: return "pride";
        }
    }

    // ── 校验 ──

    void DrawValidation()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("校验报告", EditorStyles.boldLabel);
        reportScroll = EditorGUILayout.BeginScrollView(reportScroll, GUILayout.Height(120f));
        EditorGUILayout.LabelField(string.IsNullOrEmpty(validationReport) ? "（未运行）" : validationReport,
            EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    void RunValidation()
    {
        var sb = new StringBuilder();
        int problems = 0;

        var bySin = new Dictionary<string, List<CardEpithetCatalog.Entry>>();
        var allWords = new Dictionary<string, string>(); // 词 → effectId

        foreach (var e in catalog.entries)
        {
            if (e == null) continue;
            string sin = SinOf(e.effectId);
            if (!bySin.TryGetValue(sin, out var list)) { list = new List<CardEpithetCatalog.Entry>(); bySin[sin] = list; }
            list.Add(e);

            var te = FindTextEntry(CardEpithetCatalog.ResolveTextKey(e));
            string mythic = te != null ? te.mythicText : "";

            // 缺词
            if (string.IsNullOrEmpty(mythic))
            {
                sb.AppendLine($"[缺词] {e.effectId}：未配置明线词（该卡不参与命名）");
                problems++;
                continue;
            }

            // 字数
            if (mythic.Length != 2)
            {
                sb.AppendLine($"[字数] {e.effectId}：明线词「{mythic}」为 {mythic.Length} 字，必须为 2 字");
                problems++;
            }

            // 品质词
            foreach (var banned in BannedQualityWords)
                if (mythic.Contains(banned))
                {
                    sb.AppendLine($"[品质词] {e.effectId}：「{mythic}」含禁用品质词「{banned}」");
                    problems++;
                }

            // 数值暴露
            foreach (var banned in BannedNumericWords)
                if (mythic.Contains(banned))
                {
                    sb.AppendLine($"[数值] {e.effectId}：「{mythic}」疑似暴露数值「{banned}」");
                    problems++;
                }

            // 跨罪撞词
            if (allWords.TryGetValue(mythic, out var other))
            {
                sb.AppendLine($"[跨罪撞词] 「{mythic}」同时用于 {other} 与 {e.effectId}（允许但需 Owner 认可）");
                problems++;
            }
            else allWords[mythic] = e.effectId;
        }

        // 同罪重字
        foreach (var kv in bySin)
        {
            var chars = new Dictionary<char, string>();
            foreach (var e in kv.Value)
            {
                var te = FindTextEntry(CardEpithetCatalog.ResolveTextKey(e));
                string w = te != null ? te.mythicText : "";
                if (string.IsNullOrEmpty(w)) continue;
                foreach (char ch in w)
                {
                    if (chars.TryGetValue(ch, out var owner))
                    {
                        sb.AppendLine($"[同罪重字] {kv.Key}：「{ch}」同时出现在 {owner} 与 {e.effectId}");
                        problems++;
                    }
                    else chars[ch] = e.effectId;
                }
            }
        }

        // 卡池覆盖
        var lib = CardLibrary.Instance;
        if (lib != null)
        {
            foreach (var c in lib.cards)
            {
                if (c == null) continue;
                if (c.category != CardCategory.MonsterType && c.category != CardCategory.TypeGrowth) continue;
                if (catalog.Find(c.effectId) == null)
                {
                    sb.AppendLine($"[未收录] {c.effectId}（{c.ResolveCardName()}）是 Sin 卡但词缀目录无条目");
                    problems++;
                }
            }
        }

        validationReport = problems == 0
            ? "✓ 全部通过\n" + AxisCoverage(bySin)
            : $"发现 {problems} 个问题：\n\n" + sb + "\n" + AxisCoverage(bySin);

        Repaint();
    }

    static string AxisCoverage(Dictionary<string, List<CardEpithetCatalog.Entry>> bySin)
    {
        var sb = new StringBuilder("── 轴覆盖统计 ──\n");
        foreach (var kv in bySin)
        {
            var axes = new HashSet<CardEpithetAxis>();
            foreach (var e in kv.Value) axes.Add(e.axis);
            sb.AppendLine($"{kv.Key,-10} 词数 {kv.Value.Count,-3} 轴 {axes.Count}");
        }
        return sb.ToString();
    }

    static string SinOf(string effectId)
    {
        if (string.IsNullOrEmpty(effectId)) return "?";
        var parts = effectId.Split('-');
        if (parts.Length == 0) return "?";
        switch (parts[0].ToLowerInvariant())
        {
            case "pr": return "Pride";
            case "sl": return "Sloth";
            case "gl": return "Gluttony";
            case "en": return "Envy";
            case "wr": return "Wrath";
            case "gr": return "Greed";
            case "lu": return "Lust";
            default: return parts[0];
        }
    }

    // ── 操作 ──

    void CreateCatalog()
    {
        var dir = System.IO.Path.GetDirectoryName(CatalogPath);
        if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var asset = CreateInstance<CardEpithetCatalog>();
        AssetDatabase.CreateAsset(asset, CatalogPath);
        AssetDatabase.SaveAssets();
        catalog = asset;
        SyncFromCardLibrary();
    }

    /// <summary>从 CardLibrary 扫描 MonsterType / TypeGrowth 卡，生成缺失条目。</summary>
    void SyncFromCardLibrary()
    {
        var lib = CardLibrary.Instance;
        if (lib == null)
        {
            Debug.LogWarning("[Epithet 词缀表] 未找到 CardLibrary（Resources/UI/CardLibrary），无法同步。");
            return;
        }

        int added = 0;
        foreach (var c in lib.cards)
        {
            if (c == null) continue;
            if (c.category != CardCategory.MonsterType && c.category != CardCategory.TypeGrowth) continue;
            if (string.IsNullOrEmpty(c.effectId)) continue;
            if (lib.disabledEffectIds != null && lib.disabledEffectIds.Contains(c.effectId)) continue;
            if (catalog.Find(c.effectId) != null) continue;

            float w = c.category == CardCategory.TypeGrowth
                ? CardEpithetGenerator.DefaultWeightTypeGrowth
                : CardEpithetGenerator.DefaultWeightBasic;

            catalog.entries.Add(new CardEpithetCatalog.Entry
            {
                effectId = c.effectId,
                epithetTextKey = "",
                axis = CardEpithetAxis.Rate,
                slot = CardEpithetSlot.None,
                weight = w,
            });
            added++;
        }

        // 按 effectId 字典序排列，便于策划按罪查看
        catalog.entries.Sort((a, b) => string.Compare(
            a != null ? a.effectId : "", b != null ? b.effectId : "", StringComparison.Ordinal));

        EditorUtility.SetDirty(catalog);
        Debug.Log($"[Epithet 词缀表] 同步完成，新增 {added} 条（总计 {catalog.entries.Count}）。");
        Repaint();
    }

    /// <summary>保存：结构写 SO，词文本写 TextCatalog。</summary>
    void Save()
    {
        if (textCatalog == null)
        {
            Debug.LogWarning("[Epithet 词缀表] 未指定 TextCatalog，仅保存结构（词文本未写入）。");
        }
        else
        {
            var section = EnsureSection();
            foreach (var e in catalog.entries)
            {
                if (e == null) continue;
                string key = CardEpithetCatalog.ResolveTextKey(e);
                var entry = FindEntryInSection(section, key);
                if (entry == null)
                {
                    entry = new TextEntry { key = key };
                    section.entries.Add(entry);
                }
                // 三线：text=中性 / mythicText=明线 / systemText=暗线
                var src = FindTextEntry(key);
                if (src != null && src != entry)
                {
                    entry.text = src.text;
                    entry.mythicText = src.mythicText;
                    entry.systemText = src.systemText;
                }
                entry.carrier = NarrativeCarrier.Card;
            }
            EditorUtility.SetDirty(textCatalog);
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Epithet 词缀表] 已保存 {catalog.entries.Count} 条。");
    }

    /// <summary>
    /// 导入种子词表（CardEpithetSeedData）。
    /// 默认**只填充空值**，不覆盖策划已填内容；forceOverwrite=true 时强制覆盖。
    /// 同时把三线文本写入 TextCatalog 的 card.epithet 分区。
    /// </summary>
    /// <summary>供 CardEpithetSeedImporter 调用的公开入口。</summary>
    internal static string ImportSeedPublic(CardEpithetCatalog catalog, TextCatalog textCatalog, bool forceOverwrite)
        => ImportSeed(catalog, textCatalog, forceOverwrite);

    /// <summary>供 CardEpithetSeedImporter 调用的静态查条目（跨根级 entries + 所有分区，不依赖 Resources）。</summary>
    internal static TextEntry FindEntryInSectionStatic(TextCatalog tc, string key)
    {
        if (tc == null) return null;
        if (tc.entries != null)
            foreach (var e in tc.entries)
                if (e != null && e.key == key) return e;
        if (tc.sections != null)
            foreach (var s in tc.sections)
            {
                if (s == null) continue;
                var hit = FindEntryInSection(s, key);
                if (hit != null) return hit;
            }
        return null;
    }

    static string ImportSeed(CardEpithetCatalog catalog, TextCatalog textCatalog, bool forceOverwrite)
    {
        var sb = new System.Text.StringBuilder();
        int filled = 0, skipped = 0, missing = 0;

        // TextCatalog 分区（种子导入走静态路径，不依赖窗口实例）
        TextSection section = null;
        if (textCatalog != null)
        {
            if (textCatalog.sections == null) textCatalog.sections = new List<TextSection>();
            foreach (var s in textCatalog.sections)
                if (s != null && s.sectionName == EpithetSectionName) { section = s; break; }
            if (section == null)
            {
                section = new TextSection { sectionName = EpithetSectionName, entries = new List<TextEntry>() };
                textCatalog.sections.Add(section);
            }
        }

        foreach (var row in CardEpithetSeedData.Rows)
        {
            var e = catalog.Find(row.effectId);
            if (e == null)
            {
                sb.AppendLine($"[未匹配] {row.effectId}（{row.cardName}）：卡池/目录中无此 ID，已跳过");
                missing++;
                continue;
            }

            // 结构：轴 / 槽 / 权重
            //
            // 判定"未配置"不能只看 weight <= 0：卡池同步时会预填哨兵权重
            // （Basic=35 / TypeGrowth=1000），它们都 > 0，若据此判断会导致种子永不导入。
            // 因此把这两个哨兵值本身视为"未配置"，允许种子覆盖；
            // 策划在窗口中手改过的非哨兵值则予以保留。
            bool isSentinel = Mathf.Approximately(e.weight, SentinelWeightBasic)
                           || Mathf.Approximately(e.weight, SentinelWeightTypeGrowth);
            if (forceOverwrite || isSentinel)
            {
                e.axis = row.axis;
                e.slot = row.slot;
                e.weight = row.weight;
            }

            if (textCatalog == null || section == null) continue;

            string key = CardEpithetCatalog.ResolveTextKey(e);
            var entry = FindEntryInSection(section, key);
            if (entry == null)
            {
                entry = new TextEntry { key = key, carrier = NarrativeCarrier.Card };
                section.entries.Add(entry);
            }

            // 三线：text(中性) = 明线词，mythicText = 明线词，systemText = 暗线词
            // （依据设计文档 §4.3：中性词与明线词同值，暗线词为技术语汇）
            bool changed = false;
            if (forceOverwrite || string.IsNullOrEmpty(entry.text)) { entry.text = row.mythic; changed = true; }
            if (forceOverwrite || string.IsNullOrEmpty(entry.mythicText)) { entry.mythicText = row.mythic; changed = true; }
            if (forceOverwrite || string.IsNullOrEmpty(entry.systemText)) { entry.systemText = row.system; changed = true; }

            if (changed) filled++; else skipped++;
        }

        // 罪名称谓（concept.sin.<wire>）：缺失会导致名字退化成「…之gluttony」
        int sinFilled = 0;
        if (textCatalog != null)
        {
            TextSection sinSection = null;
            foreach (var s in textCatalog.sections)
                if (s != null && s.sectionName == SinSectionName) { sinSection = s; break; }
            if (sinSection == null)
            {
                sinSection = new TextSection { sectionName = SinSectionName, entries = new List<TextEntry>() };
                textCatalog.sections.Add(sinSection);
            }

            foreach (var sin in CardEpithetSeedData.SinRows)
            {
                string key = "concept.sin." + sin.wire;
                var entry = FindEntryInSection(sinSection, key);
                if (entry == null)
                {
                    entry = new TextEntry { key = key, carrier = NarrativeCarrier.Card };
                    sinSection.entries.Add(entry);
                }
                if (forceOverwrite || string.IsNullOrEmpty(entry.text)) { entry.text = sin.mythic; sinFilled++; }
                if (forceOverwrite || string.IsNullOrEmpty(entry.mythicText)) entry.mythicText = sin.mythic;
                if (forceOverwrite || string.IsNullOrEmpty(entry.systemText)) entry.systemText = sin.system;
            }
        }

        EditorUtility.SetDirty(catalog);
        if (textCatalog != null) EditorUtility.SetDirty(textCatalog);
        AssetDatabase.SaveAssets();

        sb.Insert(0, $"种子词表共 {CardEpithetSeedData.Rows.Length} 条 → 填充 {filled}，已填跳过 {skipped}，ID 未匹配 {missing}；罪称谓 {sinFilled} 条\n\n");
        if (!forceOverwrite && skipped > 0)
            sb.AppendLine("（默认不覆盖已填内容；如需用种子重置，调用 ImportSeed(forceOverwrite: true)）");
        return sb.ToString();
    }

    void BumpRev()
    {
        catalog.epithetRev++;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Epithet 词缀表] 词表版本 → Rev.{catalog.epithetRev}");
        Repaint();
    }

    // ── TextCatalog 辅助 ──

    TextSection EnsureSection()
    {
        if (textCatalog.sections == null) textCatalog.sections = new List<TextSection>();
        foreach (var s in textCatalog.sections)
            if (s != null && s.sectionName == EpithetSectionName) return s;

        var created = new TextSection { sectionName = EpithetSectionName, entries = new List<TextEntry>() };
        textCatalog.sections.Add(created);
        return created;
    }

    /// <summary>取（或惰性登记）该词缀对应的文本条目句柄，供表格直接编辑。</summary>
    TextEntry FindOrPeekTextEntry(CardEpithetCatalog.Entry e)
    {
        string key = CardEpithetCatalog.ResolveTextKey(e);
        var existing = FindTextEntry(key);
        if (existing != null) return existing;

        // 尚未登记：返回临时句柄，保存时（Save）才真正写入资产
        if (pendingEntries == null) pendingEntries = new Dictionary<string, TextEntry>();
        if (!pendingEntries.TryGetValue(key, out var tmp))
        {
            tmp = new TextEntry { key = key, carrier = NarrativeCarrier.Card };
            pendingEntries[key] = tmp;
        }
        return tmp;
    }

    Dictionary<string, TextEntry> pendingEntries;

    static TextEntry FindEntryInSection(TextSection section, string key)
    {
        if (section == null || section.entries == null) return null;
        foreach (var e in section.entries)
            if (e != null && e.key == key) return e;
        return null;
    }

    static TextEntry FindTextEntry(string key)
    {
        var tc = TextCatalog.Instance;
        return tc == null ? null : FindEntryGlobal(tc, key);
    }

    static TextEntry FindEntryGlobal(TextCatalog tc, string key)
    {
        if (tc == null) return null;
        if (tc.entries != null)
            foreach (var e in tc.entries)
                if (e != null && e.key == key) return e;
        if (tc.sections != null)
            foreach (var s in tc.sections)
            {
                if (s == null) continue;
                var hit = FindEntryInSection(s, key);
                if (hit != null) return hit;
            }
        return null;
    }

    void RemoveTextEntry(string key)
    {
        if (textCatalog == null) return;
        if (textCatalog.entries != null)
            textCatalog.entries.RemoveAll(e => e != null && e.key == key);
        if (textCatalog.sections != null)
            foreach (var s in textCatalog.sections)
                if (s != null && s.entries != null)
                    s.entries.RemoveAll(e => e != null && e.key == key);
        EditorUtility.SetDirty(textCatalog);
    }

    CardEpithetCatalog.Entry FindEntryByNeutral(string neutralWord)
    {
        foreach (var e in catalog.entries)
        {
            if (e == null) continue;
            var te = FindTextEntry(CardEpithetCatalog.ResolveTextKey(e));
            if (te == null) continue;
            string n = string.IsNullOrEmpty(te.text) ? te.key : te.text;
            if (n == neutralWord) return e;
        }
        return null;
    }

    static bool HasWord(CardEpithetCatalog.Entry e)
    {
        var te = FindTextEntry(CardEpithetCatalog.ResolveTextKey(e));
        return te != null && !string.IsNullOrEmpty(te.mythicText);
    }

    static void Label(string text, float width)
    {
        EditorGUILayout.LabelField(text, EditorStyles.miniBoldLabel, GUILayout.Width(width));
    }
}
#endif
