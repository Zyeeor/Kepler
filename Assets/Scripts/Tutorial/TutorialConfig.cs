using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 教学阻断模式（Tutorial_Delivery_Baseline §4 三级）：
/// - NonBlocking：只提示不阻塞任何玩法（默认）；
/// - BlockTutorialChain：当前 Step 未完成前，后续教学 Step 不开始（不阻塞战斗）；
/// - BriefWaveBlock：短阻塞波次推进（M2 接 WaveGate 实现；M1 行为等价 NonBlocking）。
/// </summary>
public enum TutorialBlockingMode
{
    NonBlocking = 0,
    BlockTutorialChain = 1,
    BriefWaveBlock = 2,
}

/// <summary>
/// 单个教学 Step 的数据驱动配置（TutorialConfig 条目）。
/// 完成条件 = 事实集合（AND）：所有 completeFacts 都报告过即完成。
/// 文案 v1 直接内联中文（textKey 留位，未来本地化启用）。
/// </summary>
[Serializable]
public class TutorialStepConfig
{
    [Tooltip("Step 唯一 ID（如 TUT-01，勿与 profile 存储冲突）")]
    public string id = "TUT-XX";

    [Tooltip("Step 标题（Banner 标题行）")]
    public string title = "教学";

    [TextArea(2, 5)]
    [Tooltip("Step 正文（v1 内联中文；可含 {KEY} 占位符，运行时替换为动态键位）")]
    public string text = "";

    [Tooltip("文本目录 Key（TextCatalog；已配置时优先于 title/text 内联文本）")]
    public string textKey = "";

    /// <summary>生效标题：textKey 命中目录时取目录值，否则内联 title。</summary>
    public string ResolveTitle()
    {
        if (!string.IsNullOrEmpty(textKey) && TextCatalog.Instance != null)
            return TextCatalog.Get(textKey + ".title");
        return title;
    }

    /// <summary>生效正文：textKey 命中目录时取目录值，否则内联 text。</summary>
    public string ResolveBody()
    {
        if (!string.IsNullOrEmpty(textKey) && TextCatalog.Instance != null)
            return TextCatalog.Get(textKey + ".body");
        return text;
    }

    [Tooltip("开始条件事实：为空 = 阶段进入即激活；非空 = 需先报告这些事实（追溯判定用）")]
    public List<TutorialFact> startFacts = new List<TutorialFact>();

    [Tooltip("完成条件事实集合（AND）：全部满足即完成。KilledFirstMonster 类一次性事实由 profile 追溯判定")]
    public List<TutorialFact> completeFacts = new List<TutorialFact>();

    [Tooltip("阻断模式")]
    public TutorialBlockingMode blocking = TutorialBlockingMode.NonBlocking;

    [Tooltip("提醒间隔（秒）：未完成时 Banner 每隔该间隔重新弹出一次；0 = 不提醒")]
    public float remindInterval = 12f;

    [Tooltip("超时（秒）：激活后超时仍未完成 → 自动标记完成并放行；0 = 永不超时")]
    public float timeoutSeconds = 0f;

    [Tooltip("是否跨 Run 持久化（true = 完成后永不再要求；false = 每次新 Run 都要求）")]
    public bool persistAcrossRuns = true;

    [Tooltip("完成后自动激活的下一个 Step ID（空 = 无后续）")]
    public string nextStepId = "";

    [Tooltip("教学目标失效后是否重获取（如目标尸体被附身掉 → 等待下一具）。v1 恒 true 行为，字段留位")]
    public bool reacquireOnTargetLost = true;

    [Tooltip("本 Step 激活期间保护场上尸体不消散（TUT-04 用：教学读提示/走位时尸体不因 5s 窗口消失）")]
    public bool protectCorpseDuringStep = false;
}

/// <summary>
/// 教学配置资产（ScriptableObject）：策划可编辑的 Step 列表。
/// 场景挂载方式同 CardLibrary：在 TutorialController 上引用本资产。
/// </summary>
[CreateAssetMenu(menuName = "Kepler/Tutorial/Tutorial Config", fileName = "TutorialConfig")]
public class TutorialConfig : ScriptableObject
{
    [Tooltip("教学 Step 列表（按顺序推进；BlockTutorialChain 时串行，否则并行检测）")]
    public List<TutorialStepConfig> steps = new List<TutorialStepConfig>();

    /// <summary>按 ID 查找 Step（找不到返回 null）。</summary>
    public TutorialStepConfig FindStep(string stepId)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i].id == stepId) return steps[i];
        }
        return null;
    }
}
