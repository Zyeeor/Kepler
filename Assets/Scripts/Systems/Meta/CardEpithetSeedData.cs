using UnityEngine;

/// <summary>
/// 词缀种子数据（预填词表，来源：设计文档附录 A v2）。
///
/// 状态：**AGENT DRAFT / PENDING OWNER WORDING REVIEW**
///   —— 词汇仅为语域与结构示范，Owner / 策划可逐词替换。
///
/// 为什么用代码存而不是直接写进 SO：
///   1. 词表是可审阅的**文本资产**，改词 = 改这个文件，diff 清晰可见；
///   2. 便于重新导入覆盖（SO 里改过的值可被种子重置 / 增量补齐）；
///   3. 策划日常无需碰本文件——用 `Kepler > Cards > Epithet 词缀表` 窗口可视化编辑即可，
///      编辑结果保存在 CardEpithetCatalog.asset + TextCatalog.asset（优先级高于本种子）。
///
/// 导入方式：菜单 `Kepler > Cards > Epithet 导入种子词表`（CardEpithetEditorWindow）。
/// 导入行为：**只填充空值，不覆盖策划已填内容**（除非勾选"强制覆盖"）。
/// </summary>
public static class CardEpithetSeedData
{
    public struct Row
    {
        public string effectId;
        public string cardName;   // 仅注释/展示用，不参与匹配
        public string mythic;     // 明线词（2 字）
        public string system;     // 暗线词（技术语汇）
        public CardEpithetAxis axis;
        public CardEpithetSlot slot;
        public float weight;
    }

    /// <summary>
    /// 罪名称谓（设计文档 §4.1 中心词表）。写入 TextCatalog 的 concept.sin.&lt;wire&gt;。
    /// 缺失时名字会退化成「…之gluttony」这类英文 wire 名，因此必须配置。
    /// </summary>
    public struct SinRow
    {
        public string wire;      // 网络/存档用的英文标识，如 "gluttony"
        public string mythic;    // Mythic / Neutral 同值（Dual_Line §6：不增生同义词）
        public string system;    // System 线，技术语汇（"Xxx Carrier" 的 Xxx 部分）
    }

    public static readonly SinRow[] SinRows =
    {
        new SinRow { wire = "pride",    mythic = "傲慢", system = "Pride" },
        new SinRow { wire = "sloth",    mythic = "怠惰", system = "Sloth" },
        new SinRow { wire = "gluttony", mythic = "暴食", system = "Gluttony" },
        new SinRow { wire = "envy",     mythic = "嫉妒", system = "Envy" },
        new SinRow { wire = "wrath",    mythic = "愤怒", system = "Wrath" },
        new SinRow { wire = "greed",    mythic = "贪婪", system = "Greed" },
        new SinRow { wire = "lust",     mythic = "色欲", system = "Lust" },
    };

    /// <summary>49 张 Sin 卡的预填词表（附录 A.1 – A.7）。</summary>
    public static readonly Row[] Rows =
    {
        // ── A.1 傲慢 Pride（8）──
        new Row { effectId = "PR-TG01", cardName = "王权疾令",     mythic = "迅捷", system = "速率统调", axis = CardEpithetAxis.Rate,     slot = CardEpithetSlot.Body,     weight = 1000f },
        new Row { effectId = "PR-A04",  cardName = "王命远征",     mythic = "远击", system = "程增",     axis = CardEpithetAxis.Reach,    slot = CardEpithetSlot.Attack,   weight = 35f },
        new Row { effectId = "PR-A01",  cardName = "王冠军势",     mythic = "三重", system = "三实例",   axis = CardEpithetAxis.Count,    slot = CardEpithetSlot.Attack,   weight = 80f },
        new Row { effectId = "PR-S01",  cardName = "王权巡猎",     mythic = "疾巡", system = "段数扩展", axis = CardEpithetAxis.Count,    slot = CardEpithetSlot.Special,  weight = 65f },
        new Row { effectId = "PR-A02",  cardName = "十字圣裁",     mythic = "交错", system = "十字面",   axis = CardEpithetAxis.Shape,    slot = CardEpithetSlot.Attack,   weight = 80f },
        new Row { effectId = "PR-X01",  cardName = "征服者之径",   mythic = "遗锋", system = "路径派生", axis = CardEpithetAxis.Derive,   slot = CardEpithetSlot.Special,  weight = 65f },
        new Row { effectId = "PR-A03",  cardName = "异端噤声",     mythic = "斩绝", system = "切断协议", axis = CardEpithetAxis.Interact, slot = CardEpithetSlot.Attack,   weight = 80f },
        new Row { effectId = "PR-M01",  cardName = "王座之步",     mythic = "骁烈", system = "前端增益", axis = CardEpithetAxis.Lethal,   slot = CardEpithetSlot.Movement, weight = 55f },

        // ── A.2 怠惰 Sloth（7）──
        new Row { effectId = "SL-S03",  cardName = "侍从圣武",     mythic = "不倦", system = "节奏提升", axis = CardEpithetAxis.Rate,     slot = CardEpithetSlot.Special,  weight = 65f },
        new Row { effectId = "SL-A03",  cardName = "圣骸分裂",     mythic = "迸散", system = "散射扩展", axis = CardEpithetAxis.Count,    slot = CardEpithetSlot.Attack,   weight = 65f },
        new Row { effectId = "SL-S01",  cardName = "沉眠侍从",     mythic = "众仆", system = "单元扩容", axis = CardEpithetAxis.Count,    slot = CardEpithetSlot.Special,  weight = 65f },
        new Row { effectId = "SL-A04",  cardName = "众仆齐鸣",     mythic = "齐鸣", system = "多发分摊", axis = CardEpithetAxis.Shape,    slot = CardEpithetSlot.Attack,   weight = 80f },
        new Row { effectId = "SL-M01",  cardName = "遗下的守望者", mythic = "遗伏", system = "起点部署", axis = CardEpithetAxis.Derive,   slot = CardEpithetSlot.Movement, weight = 65f },
        new Row { effectId = "SL-A05",  cardName = "巨像践踏",     mythic = "厚重", system = "压制协议", axis = CardEpithetAxis.Interact, slot = CardEpithetSlot.Attack,   weight = 80f },
        new Row { effectId = "SL-M02",  cardName = "迟来的地鸣",   mythic = "震地", system = "终点结算", axis = CardEpithetAxis.Lethal,   slot = CardEpithetSlot.Movement, weight = 55f },

        // ── A.3 暴食 Gluttony（7）──
        new Row { effectId = "GL-M01",  cardName = "饥神猎步",     mythic = "轻捷", system = "形态保持", axis = CardEpithetAxis.Rate,     slot = CardEpithetSlot.Movement, weight = 55f },
        new Row { effectId = "GL-A03",  cardName = "远方圣餐",     mythic = "远诱", system = "落点解锁", axis = CardEpithetAxis.Reach,    slot = CardEpithetSlot.Attack,   weight = 80f },
        new Row { effectId = "GL-A02",  cardName = "猎步圣餐",     mythic = "阔口", system = "覆盖翻倍", axis = CardEpithetAxis.Scale,    slot = CardEpithetSlot.Attack,   weight = 65f },
        new Row { effectId = "GL-A01",  cardName = "群口圣宴",     mythic = "双颚", system = "双实例",   axis = CardEpithetAxis.Count,    slot = CardEpithetSlot.Attack,   weight = 80f },
        new Row { effectId = "GL-S03",  cardName = "万物皆食",     mythic = "贪噬", system = "全量吞噬", axis = CardEpithetAxis.Interact, slot = CardEpithetSlot.Special,  weight = 80f },
        new Row { effectId = "GL-S01",  cardName = "鲜血圣餐",     mythic = "饱血", system = "耐久回补", axis = CardEpithetAxis.Endure,   slot = CardEpithetSlot.Special,  weight = 55f },
        new Row { effectId = "GL-S02",  cardName = "最后一餐",     mythic = "终食", system = "低耐久终止", axis = CardEpithetAxis.Lethal, slot = CardEpithetSlot.Special,  weight = 80f },

        // ── A.4 嫉妒 Envy（6）──
        new Row { effectId = "EN-A01",  cardName = "万眼同视",     mythic = "万眼", system = "四目标分配", axis = CardEpithetAxis.Count,  slot = CardEpithetSlot.Attack,   weight = 80f },
        new Row { effectId = "EN-A03",  cardName = "穿镜圣光",     mythic = "穿镜", system = "穿透协议",   axis = CardEpithetAxis.Shape,  slot = CardEpithetSlot.Attack,   weight = 80f },
        new Row { effectId = "EN-M01",  cardName = "镜痕巡猎",     mythic = "遗痕", system = "路径写入",   axis = CardEpithetAxis.Bind,   slot = CardEpithetSlot.Movement, weight = 65f },
        new Row { effectId = "EN-R01",  cardName = "无底之镜",     mythic = "无底", system = "储存增益",   axis = CardEpithetAxis.Bind,   slot = CardEpithetSlot.Body,     weight = 55f },
        new Row { effectId = "EN-S01",  cardName = "雷霆作证",     mythic = "复雷", system = "同点复现",   axis = CardEpithetAxis.Derive, slot = CardEpithetSlot.Special,  weight = 65f },
        new Row { effectId = "EN-A05",  cardName = "妒焰渐炽",     mythic = "渐炽", system = "伤害爬升",   axis = CardEpithetAxis.Lethal, slot = CardEpithetSlot.Attack,   weight = 80f },

        // ── A.5 愤怒 Wrath（6）──
        new Row { effectId = "WR-S01",  cardName = "风暴锁链",     mythic = "风驰", system = "移动增益",   axis = CardEpithetAxis.Rate,    slot = CardEpithetSlot.Special,  weight = 55f },
        new Row { effectId = "WR-M02",  cardName = "末日锁链",     mythic = "远锁", system = "位移扩展",   axis = CardEpithetAxis.Reach,   slot = CardEpithetSlot.Movement, weight = 55f },
        new Row { effectId = "WR-S03",  cardName = "终末飓风",     mythic = "长旋", system = "窗口延长",   axis = CardEpithetAxis.Scale,   slot = CardEpithetSlot.Special,  weight = 35f },
        new Row { effectId = "WR-B02",  cardName = "以身为薪",     mythic = "炙体", system = "贴身燃烧",   axis = CardEpithetAxis.Shape,   slot = CardEpithetSlot.Body,     weight = 65f },
        new Row { effectId = "WR-M01",  cardName = "焚途誓约",     mythic = "焚途", system = "路径伤害",   axis = CardEpithetAxis.Derive,  slot = CardEpithetSlot.Movement, weight = 65f },
        new Row { effectId = "WR-B01",  cardName = "殉身加冕",     mythic = "殉身", system = "低耐久响应", axis = CardEpithetAxis.Endure,  slot = CardEpithetSlot.Body,     weight = 80f },

        // ── A.6 贪婪 Greed（8）──
        new Row { effectId = "GR-A01",  cardName = "万手圣库",     mythic = "万手", system = "库存扩展", axis = CardEpithetAxis.Count,    slot = CardEpithetSlot.Attack,   weight = 55f },
        new Row { effectId = "GR-A03",  cardName = "亡者遗产",     mythic = "增殖", system = "实例派生", axis = CardEpithetAxis.Count,    slot = CardEpithetSlot.Attack,   weight = 80f },
        new Row { effectId = "GR-A07",  cardName = "迂回纳贡",     mythic = "迂回", system = "路径重写", axis = CardEpithetAxis.Shape,    slot = CardEpithetSlot.Attack,   weight = 55f },
        new Row { effectId = "GR-M01",  cardName = "黑油圣路",     mythic = "泥沼", system = "移动抑制", axis = CardEpithetAxis.Bind,     slot = CardEpithetSlot.Movement, weight = 65f },
        new Row { effectId = "GR-A02",  cardName = "未收之贡",     mythic = "不舍", system = "再获取",   axis = CardEpithetAxis.Derive,   slot = CardEpithetSlot.Attack,   weight = 65f },
        new Row { effectId = "GR-S01",  cardName = "圣库纳贡",     mythic = "纳贡", system = "资源转化", axis = CardEpithetAxis.Derive,   slot = CardEpithetSlot.Special,  weight = 55f },
        new Row { effectId = "GR-M02",  cardName = "圣路恩赐",     mythic = "庇路", system = "地形隔离", axis = CardEpithetAxis.Endure,   slot = CardEpithetSlot.Movement, weight = 55f },
        new Row { effectId = "GR-S04",  cardName = "贪神庇护",     mythic = "坚壁", system = "窗口延长", axis = CardEpithetAxis.Endure,   slot = CardEpithetSlot.Special,  weight = 80f },

        // ── A.7 色欲 Lust（7）──
        new Row { effectId = "LU-TG01", cardName = "欲潮不息",     mythic = "迅潮", system = "循环加速", axis = CardEpithetAxis.Rate,     slot = CardEpithetSlot.Body,     weight = 1000f },
        new Row { effectId = "LU-A04",  cardName = "色欲潮汐",     mythic = "环涌", system = "径向扩散", axis = CardEpithetAxis.Shape,    slot = CardEpithetSlot.Attack,   weight = 80f },
        new Row { effectId = "LU-M05",  cardName = "同欲之影",     mythic = "同影", system = "镜像执行", axis = CardEpithetAxis.Shape,    slot = CardEpithetSlot.Movement, weight = 80f },
        new Row { effectId = "LU-A03",  cardName = "欲痕殉爆",     mythic = "殉爆", system = "终止爆破", axis = CardEpithetAxis.Derive,   slot = CardEpithetSlot.Attack,   weight = 80f },
        new Row { effectId = "LU-M03",  cardName = "背离之罚",     mythic = "背离", system = "换位爆破", axis = CardEpithetAxis.Derive,   slot = CardEpithetSlot.Movement, weight = 65f },
        new Row { effectId = "LU-S05",  cardName = "同欲相噬",     mythic = "相噬", system = "碰撞爆破", axis = CardEpithetAxis.Interact, slot = CardEpithetSlot.Special,  weight = 65f },
        new Row { effectId = "LU-S06",  cardName = "无害之拥",     mythic = "无害", system = "伤害隔离", axis = CardEpithetAxis.Endure,   slot = CardEpithetSlot.Special,  weight = 80f },
    };
}
