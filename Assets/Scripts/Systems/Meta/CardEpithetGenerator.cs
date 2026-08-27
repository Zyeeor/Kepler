using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物词缀名生成器（纯函数，无 MonoBehaviour，可 EditMode 单测）。
///
/// 职责：把「该 Sin 的 Card 清单」确定性派生为一个独特的称号名词序列。
///
/// 三条铁律（设计文档 §3）：
///   1. 确定性：同一 cardIds 集合 → 恒定同一名字。无随机数、无时间依赖、无玩家 ID 依赖。
///   2. 可反向读：词与卡 1:1 映射，玩家看到"三重"应能想起「王冠军势」。
///   3. 零 Gameplay 耦合：纯读取 + 纯展示层，不回写任何战斗数据（Meta_Progression §5.1）。
///
/// 消费方两处，**必须共用本类的 Generate()**（验收项 14，不得各写一份）：
///   - 局外：荣誉殿堂条目标题（写 epithetCache）
///   - 局内：Elite 快照 monsterType（上传端编码 → 他人局横幅显示）
/// </summary>
public static class CardEpithetGenerator
{
    /// <summary>默认代表性权重档（设计文档 §5.3）。策划可在 SO 中显式覆盖。</summary>
    public const float DefaultWeightTypeGrowth = 1000f;   // Type Growth：强制入选并置于其轴位
    public const float DefaultWeightQuality = 80f;        // 形态质变 / 目标方式解锁 / 高阶 Interaction
    public const float DefaultWeightLinkage = 65f;        // 跨槽联动 / 派生 / 击杀增殖
    public const float DefaultWeightResource = 55f;       // 资源 / 状态 / 条件强化
    public const float DefaultWeightBasic = 35f;          // 单轴基础强化

    // ── 对外 API ──

    /// <summary>
    /// 确定性生成中性词序列（已按语感位次升序排列）。
    /// 无目录 / 无有效卡 / 全部失效时返回空数组（调用方走兜底命名，见 §7.3）。
    /// </summary>
    public static string[] Generate(IEnumerable<string> cardIds)
    {
        return Generate(cardIds, CardEpithetCatalog.Instance);
    }

    /// <summary>可注入目录的重载（单测 / Editor 工具用，不依赖 Resources）。</summary>
    public static string[] Generate(IEnumerable<string> cardIds, CardEpithetCatalog catalog)
    {
        if (catalog == null || cardIds == null) return Empty;

        // step 0+1：过滤失效 ID + 按 effectId 字典序排序（消除集合无序性）
        var candidates = CollectCandidates(cardIds, catalog);
        if (candidates.Count == 0) return Empty;

        // step 2：词数 N = clamp(有效条数, 1, maxEpithetCount)
        int max = catalog.maxEpithetCount > 0 ? catalog.maxEpithetCount : 4;
        int n = Mathf.Clamp(candidates.Count, 1, max);

        // step 3：贪心选择（惩罚而非硬排斥，避免同轴集中时凑不满 N 词）
        var picked = new List<Candidate>(n);
        var pickedAxisCount = new Dictionary<CardEpithetAxis, int>();
        var pickedSlotCount = new Dictionary<CardEpithetSlot, int>();

        for (int round = 0; round < n; round++)
        {
            Candidate best = null;
            float bestScore = float.NegativeInfinity;

            foreach (var c in candidates)
            {
                if (c.picked) continue;

                pickedAxisCount.TryGetValue(c.axis, out int axisSame);
                pickedSlotCount.TryGetValue(c.slot, out int slotSame);

                // axisPenalty = 1 / 2^k（已选同轴数 k）
                float axisPenalty = 1f / Mathf.Pow(2f, axisSame);
                // slotPenalty = 0.8^m（已选同槽数 m）
                float slotPenalty = Mathf.Pow(0.8f, slotSame);

                float score = c.weight * axisPenalty * slotPenalty;

                // tie-break：① score 大者胜 ② baseWeight 大者胜 ③ effectId 字典序小者胜
                if (best == null || score > bestScore
                    || (Mathf.Approximately(score, bestScore) && IsBetterTieBreak(c, best)))
                {
                    best = c;
                    bestScore = score;
                }
            }

            if (best == null) break;
            best.picked = true;
            picked.Add(best);
            pickedAxisCount[best.axis] = pickedAxisCount.TryGetValue(best.axis, out int ac) ? ac + 1 : 1;
            pickedSlotCount[best.slot] = pickedSlotCount.TryGetValue(best.slot, out int sc) ? sc + 1 : 1;
        }

        if (picked.Count == 0) return Empty;

        // step 4：按语感位次升序排列（与选取顺序无关 → 确定性）
        // 同位次时按 effectId 字典序，保证完全稳定
        picked.Sort((a, b) =>
        {
            int ra = catalog.AxisRank(a.axis), rb = catalog.AxisRank(b.axis);
            if (ra != rb) return ra.CompareTo(rb);
            return string.Compare(a.effectId, b.effectId, StringComparison.Ordinal);
        });

        var words = new string[picked.Count];
        for (int i = 0; i < picked.Count; i++) words[i] = picked[i].neutralWord;
        return words;
    }

    /// <summary>
    /// 按当前显示线套模板输出成品名（设计文档 §7.1）。
    /// 词数 0 时输出兜底名（只有中心词，不虚构词缀）。
    /// </summary>
    public static string Format(string sinWire, string[] neutralWords, int validCardCount)
    {
        return Format(sinWire, neutralWords, validCardCount, CardEpithetCatalog.Instance);
    }

    /// <summary>可注入目录的重载（单测 / Editor 工具用）。</summary>
    public static string Format(string sinWire, string[] neutralWords, int validCardCount, CardEpithetCatalog catalog)
    {
        string connector = (catalog != null && !string.IsNullOrEmpty(catalog.connector))
            ? catalog.connector : "之";

        // 兜底命名（§7.3）：有效词数 0 → 只有中心词，附失效标记由调用方处理
        if (neutralWords == null || neutralWords.Length == 0)
            return FallbackName(sinWire, validCardCount);

        // 词的三线映射：中性词 → 当前显示线的词（未知中性词原样显示，不丢字）
        var mapped = new string[neutralWords.Length];
        for (int i = 0; i < neutralWords.Length; i++)
            mapped[i] = ResolveEpithetWord(neutralWords[i], catalog);

        string joined = string.Concat(mapped);
        string sinName = SinDisplayName(sinWire);

        // 当前显示线：Mythic 用「词…之罪称谓」，System 用「Carrier · 词/词 · Rev.N」，Neutral 用「罪（N 卡构筑）」
        var line = CurrentLine();
        if (line == TextLinePreference.System)
        {
            string rev = validCardCount > 0 ? validCardCount.ToString() : "0";
            return $"{SystemSinName(sinWire)} · {string.Join("/", mapped)} · Rev.{rev}";
        }
        if (line == TextLinePreference.Mythic)
            return $"{joined}{connector}{sinName}";

        // Neutral
        return $"{sinName}（{(validCardCount > 0 ? validCardCount : 0)} 卡构筑）";
    }

    /// <summary>有效卡数（目录中存在的卡数量；失效 ID 不计）。</summary>
    public static int CountValid(IEnumerable<string> cardIds, CardEpithetCatalog catalog)
    {
        if (catalog == null || cardIds == null) return 0;
        int n = 0;
        foreach (var id in cardIds)
        {
            if (string.IsNullOrEmpty(id)) continue;
            if (catalog.Find(id) != null) n++;
        }
        return n;
    }

    // ── Wire 编解码（ADR-3：上传端生成，下发中性词序列）──

    /// <summary>
    /// 上传端用：编码为 monsterType 载荷，格式 "sin|w1|w2|…"。
    /// 服务器只校验非空、不解析内容，因此改格式无需改服务器 / 无需数据迁移。
    /// </summary>
    public static string EncodeForWire(string sinWire, string[] words)
    {
        if (string.IsNullOrEmpty(sinWire)) return "";
        if (words == null || words.Length == 0) return sinWire; // 无词缀 → 纯 sin（旧格式兼容）
        var parts = new List<string>(words.Length + 1) { sinWire };
        for (int i = 0; i < words.Length; i++)
            if (!string.IsNullOrEmpty(words[i])) parts.Add(words[i]);
        return string.Join("|", parts);
    }

    /// <summary>
    /// 遭遇端用：解析 wire 载荷。
    /// 旧格式（无 '|'）返回 false —— 调用方按"无词缀"降级为 catalog 名（验收项 15）。
    /// </summary>
    public static bool TryDecodeFromWire(string monsterType, out string sinWire, out string[] words)
    {
        sinWire = "";
        words = Empty;
        if (string.IsNullOrEmpty(monsterType)) return false;

        var parts = monsterType.Split('|');
        if (parts.Length < 2) return false;   // 旧格式：纯 catalog 名，无分隔符

        sinWire = parts[0];
        var list = new List<string>(parts.Length - 1);
        for (int i = 1; i < parts.Length; i++)
            if (!string.IsNullOrEmpty(parts[i])) list.Add(parts[i]);

        words = list.ToArray();
        return true;
    }

    /// <summary>存档用：中性词序列 → 字符串（epithetCache）。存中性词而非成品，便于换线重套模板。</summary>
    public static string EncodeCache(string[] words)
    {
        return words == null || words.Length == 0 ? "" : string.Join("|", words);
    }

    /// <summary>存档用：字符串 → 中性词序列。</summary>
    public static string[] DecodeCache(string cache)
    {
        return string.IsNullOrEmpty(cache) ? Empty : cache.Split('|');
    }

    // ── 内部 ──

    static readonly string[] Empty = new string[0];

    class Candidate
    {
        public string effectId;
        public string neutralWord;
        public CardEpithetAxis axis;
        public CardEpithetSlot slot;
        public float weight;
        public bool picked;
    }

    /// <summary>step 0+1：过滤目录不存在的 ID（含已删卡），并按 effectId 字典序排序去重。</summary>
    static List<Candidate> CollectCandidates(IEnumerable<string> cardIds, CardEpithetCatalog catalog)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var raw = new List<string>();
        foreach (var id in cardIds)
        {
            if (string.IsNullOrEmpty(id)) continue;
            if (!seen.Add(id)) continue;          // 同卡去重
            if (catalog.Find(id) == null) continue; // 失效 ID 静默丢弃
            raw.Add(id);
        }

        // 字典序排序：消除集合无序性（step 1）
        raw.Sort(StringComparer.Ordinal);

        var list = new List<Candidate>(raw.Count);
        foreach (var id in raw)
        {
            var e = catalog.Find(id);
            string word = NeutralWordOf(id, catalog);
            if (string.IsNullOrEmpty(word)) continue;  // 有目录条目但无词文本 → 不参与命名
            list.Add(new Candidate
            {
                effectId = id,
                neutralWord = word,
                axis = e.axis,
                slot = e.slot,
                weight = ResolveWeight(e),
            });
        }
        return list;
    }

    static bool IsBetterTieBreak(Candidate c, Candidate best)
    {
        if (!Mathf.Approximately(c.weight, best.weight))
            return c.weight > best.weight;                                       // ② baseWeight 大者胜
        return string.Compare(c.effectId, best.effectId, StringComparison.Ordinal) < 0; // ③ 字典序小者胜
    }

    /// <summary>生效权重：显式 weight > 0 时用显式值，否则按默认档推断。</summary>
    static float ResolveWeight(CardEpithetCatalog.Entry e)
    {
        if (e == null) return DefaultWeightBasic;
        if (e.weight > 0f) return e.weight;

        // 默认档推断（§5.3）：TypeGrowth 恒 1000；其余暂按单轴基础档，
        // 具体档位由策划在配置工具中显式指定（无法从数据自动判定"质变/联动"等语义类别）
        if (IsTypeGrowth(e)) return DefaultWeightTypeGrowth;
        return DefaultWeightBasic;
    }

    static bool IsTypeGrowth(CardEpithetCatalog.Entry e)
    {
        // effectId 约定：TypeGrowth 卡形如 "PR-TG01"（中段为 TG）
        if (string.IsNullOrEmpty(e.effectId)) return false;
        var parts = e.effectId.Split('-');
        return parts.Length >= 2 && parts[1].StartsWith("TG", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>中性词（三线中的 text 字段，用于存档与调试）。</summary>
    static string NeutralWordOf(string effectId, CardEpithetCatalog catalog)
    {
        var e = catalog.Find(effectId);
        if (e == null) return "";
        string key = CardEpithetCatalog.ResolveTextKey(e);
        var entry = TextCatalog.Instance != null ? FindTextEntry(TextCatalog.Instance, key) : null;
        if (entry == null) return "";
        return string.IsNullOrEmpty(entry.text) ? entry.key : entry.text;
    }

    /// <summary>
    /// 中性词 → 当前显示线的词。
    /// 本机词表未知该中性词时**原样返回**（中性词本身是可读中文，不是 ID）→ 不丢字不报错（验收项 16）。
    /// 注意：中性词到 key 的反查需遍历，实际项目词表 49 条，开销可忽略。
    /// </summary>
    static string ResolveEpithetWord(string neutralWord, CardEpithetCatalog catalog)
    {
        if (catalog == null) return neutralWord;
        var tc = TextCatalog.Instance;
        if (tc == null) return neutralWord;

        // 先按目录条目反查：找 neutralWord 对应的 key
        string key = null;
        foreach (var e in catalog.entries)
        {
            if (e == null) continue;
            string k = CardEpithetCatalog.ResolveTextKey(e);
            var entry = FindTextEntry(tc, k);
            if (entry == null) continue;
            string neutral = string.IsNullOrEmpty(entry.text) ? entry.key : entry.text;
            if (neutral == neutralWord) { key = k; break; }
        }

        if (key == null) return neutralWord;   // 未知中性词 → 原样显示

        var hit = FindTextEntry(tc, key);
        if (hit == null) return neutralWord;

        var line = CurrentLine();
        if (line == TextLinePreference.Mythic) return hit.MythicText;
        if (line == TextLinePreference.System) return hit.SystemText;
        return hit.NeutralText;
    }

    static TextLinePreference CurrentLine()
    {
        // 沿用既有 NarrativeDisplay 机制，不新增开关（设计文档 §7.3）
        if (NarrativeDisplay.IsReady)
            return NarrativeDisplay.EffectiveLine(NarrativeCarrier.Card);
        return TextLinePreference.Neutral;
    }

    static TextEntry FindTextEntry(TextCatalog catalog, string key)
    {
        // TextCatalog.FindEntry 是 private static，此处按同样查找顺序复刻
        if (catalog == null || string.IsNullOrEmpty(key)) return null;
        var list = catalog.entries;
        if (list != null)
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].key == key) return list[i];

        var sections = catalog.sections;
        if (sections != null)
            for (int s = 0; s < sections.Count; s++)
            {
                if (sections[s] == null) continue;
                var el = sections[s].entries;
                if (el == null) continue;
                for (int i = 0; i < el.Count; i++)
                    if (el[i] != null && el[i].key == key) return el[i];
            }
        return null;
    }

    // ── 罪名（三线）──

    static string SinDisplayName(string sinWire)
    {
        // Mythic / Neutral 同值（Dual_Line §6：同一概念不增生同义词）
        return LocalizedSin(sinWire, mythic: true);
    }

    static string SystemSinName(string sinWire)
    {
        return LocalizedSin(sinWire, mythic: false) + " Carrier";
    }

    static string LocalizedSin(string sinWire, bool mythic)
    {
        // 走 TextCatalog（concept.sin.<wire>）；未配置时回退 wire 名本身
        var tc = TextCatalog.Instance;
        if (tc != null)
        {
            string key = "concept.sin." + sinWire;
            var entry = FindTextEntry(tc, key);
            if (entry != null)
                return mythic ? entry.MythicText : entry.SystemText;
        }
        return sinWire;
    }

    /// <summary>兜底命名（§7.3）：有效词数 0 → 只有中心词，不虚构词缀。</summary>
    static string FallbackName(string sinWire, int validCardCount)
    {
        var line = CurrentLine();
        if (line == TextLinePreference.System)
            return $"{SystemSinName(sinWire)} · Rev.0";
        if (line == TextLinePreference.Mythic)
            return SinDisplayName(sinWire);
        return $"{SinDisplayName(sinWire)}（0 卡构筑）";
    }
}
