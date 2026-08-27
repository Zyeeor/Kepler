using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 词缀轴（Epithet Axis）—— 保证一个名字里的多个词描述**不同侧面**。
///
/// 背景：若只按权重取 Top N，满构筑的傲慢可能选出全是"攻击变多变远"的四个词，
/// 读起来像同一句话说四遍。轴用于保证多样性（设计文档 §5）。
///
/// 枚举值 = 默认"语感位次"：最终名字中的词按此升序排列，
/// **与选取顺序、取得顺序无关** —— 这是确定性（Determinism）的关键之一。
/// 位次可在 CardEpithetCatalog.axisOrder 中重排（策划可配，不写死）。
/// </summary>
public enum CardEpithetAxis
{
    [Tooltip("速率：速度、频率、冷却")]
    Rate = 1,
    [Tooltip("距程：距离、远程化")]
    Reach = 2,
    [Tooltip("幅度：范围、体量、持续时长")]
    Scale = 3,
    [Tooltip("数目：数量、分裂、增殖、次数")]
    Count = 4,
    [Tooltip("形态：形态替换、路径重写、弹幕形状")]
    Shape = 5,
    [Tooltip("缚印：标记、减速、牵引、储存")]
    Bind = 6,
    [Tooltip("派生：延迟派生、路径残留、二次结算")]
    Derive = 7,
    [Tooltip("交锋：切断、吞噬、压制、反射")]
    Interact = 8,
    [Tooltip("存续：防护、回复、低耐久响应")]
    Endure = 9,
    [Tooltip("杀伐：伤害提升、处决")]
    Lethal = 10,
}

/// <summary>
/// 词缀槽位（三槽动作身份的次级去重维度）。
/// 用于 slotPenalty —— 让名字尽量横跨多槽，呼应 03_PRESENTATION §15 三槽动作身份。
/// </summary>
public enum CardEpithetSlot
{
    None = 0,
    Movement = 1,
    Attack = 2,
    Special = 3,
    Body = 4,
}

/// <summary>
/// 词缀目录：effectId → { 轴, 槽, 权重 }（设计文档 §9.1）。
///
/// 为什么是独立 SO 而不是给 CardData 加字段（ADR-1）：
///   1) CardLibrary.asset 有 49+ 条既有序列化数据，改 CardData 结构触碰
///      .vibe/rules.md §3.3「禁止随意改动 [SerializeField] 字段顺序与类型」；
///   2) 词缀是**局外表现**配置，混进 Gameplay 卡数据违反关注点分离；
///   3) 缺词 = 该卡不参与命名，优雅降级，无需改卡数据；
///   4) 与既有 EliteMonsterCatalog / TextCatalog 的单文件 SO 模式一致。
///
/// 词本身**不存这里**，存 TextCatalog 的 card.&lt;effectId&gt;.epithet（三线字段天然支持双线）。
/// 本 SO 只存结构化配置。
///
/// 资产路径：Assets/Resources/CardEpithetCatalog.asset（CardEpithetCatalog.Instance 自动 Resources.Load）。
/// 策划配置入口：Kepler > Cards > Epithet 词缀表（CardEpithetEditorWindow）。
/// </summary>
[CreateAssetMenu(fileName = "CardEpithetCatalog", menuName = "Kepler/Cards/Card Epithet Catalog")]
public class CardEpithetCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("关联键：卡牌 effectId，如 \"PR-A01\"。需与 CardLibrary 中该卡的 effectId 一致。")]
        public string effectId;

        [Tooltip("词缀文本 Key。留空 → 自动推导为 \"card.<effectId>.epithet\"。")]
        public string epithetTextKey;

        [Tooltip("词缀轴：决定语义侧面与名字中的排列位次。")]
        public CardEpithetAxis axis = CardEpithetAxis.Rate;

        [Tooltip("槽位：次级去重维度，让名字尽量横跨三槽。")]
        public CardEpithetSlot slot = CardEpithetSlot.None;

        [Tooltip("代表性权重。0 = 按默认档推断（TypeGrowth 1000 / 质变·解锁 80 / 联动·派生 65 / 资源·状态 55 / 单轴基础 35）。")]
        public float weight;
    }

    [Tooltip("词缀条目（key = effectId）。缺词的卡不参与命名，不报错。")]
    public List<Entry> entries = new List<Entry>();

    [Tooltip("词数上限。4 词 = 8 字 + 连接词 1 字 + 罪称谓 2 字 = 11 字，1920×1080 下条目标题行可单行容纳。")]
    public int maxEpithetCount = 4;

    [Tooltip("连接词（Mythic 线）：\"{词1}{词2}…{连接词}{罪称谓}\"。")]
    public string connector = "之";

    [Tooltip("语感位次：最终名字中词的排列顺序（升序）。留空 = 按 CardEpithetAxis 枚举值默认位次。")]
    public List<CardEpithetAxis> axisOrder = new List<CardEpithetAxis>();

    [Tooltip("词表版本号。词表内容变更时递增，用于驱动 epithetCache 重算（策划用配置工具 bump）。")]
    public int epithetRev = 1;

    // ── 访问 ──

    static CardEpithetCatalog instance;

    /// <summary>运行时单例（Resources/CardEpithetCatalog）。未配置返回 null，调用方须判空降级。</summary>
    public static CardEpithetCatalog Instance
    {
        get
        {
            if (instance == null) instance = Resources.Load<CardEpithetCatalog>("CardEpithetCatalog");
            return instance;
        }
    }

    /// <summary>按 effectId 查词缀条目；未找到返回 null（该卡不参与命名）。</summary>
    public Entry Find(string effectId)
    {
        if (entries == null || string.IsNullOrEmpty(effectId)) return null;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e != null && string.Equals(e.effectId, effectId, StringComparison.Ordinal)) return e;
        }
        return null;
    }

    /// <summary>生效的文本 Key（留空自动推导）。</summary>
    public static string ResolveTextKey(Entry e)
    {
        if (e == null) return "";
        return string.IsNullOrEmpty(e.epithetTextKey)
            ? "card." + e.effectId + ".epithet"
            : e.epithetTextKey;
    }

    /// <summary>
    /// 轴的语感位次（用于最终排列）。axisOrder 未配置该轴时回退枚举值。
    /// </summary>
    public int AxisRank(CardEpithetAxis axis)
    {
        if (axisOrder != null)
        {
            for (int i = 0; i < axisOrder.Count; i++)
                if (axisOrder[i] == axis) return i + 1;
        }
        return (int)axis;
    }
}
