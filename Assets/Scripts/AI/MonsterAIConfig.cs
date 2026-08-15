using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 怪物 AI 配置条目：内联数据（非 SO），按 id 从 MonsterAIConfig 查询。
/// 攻击范围拆分：basicAttackRange（普攻范围）与 skillAttackRange（技能范围）相互独立，
/// 无大小关系——远程怪可技能范围大、近战怪可普攻范围大。
/// </summary>
[System.Serializable]
public class MonsterAIConfigEntry
{
    [Tooltip("配置唯一 id，怪物 prefab 通过 MonsterActor.aiConfigId 引用。")]
    public string id;

    [Header("范围（攻击范围独立于索敌半径，且普攻/技能范围互无大小关系）")]
    [Tooltip("索敌半径：玩家在此半径内，AI 才产生行为（追击/攻击）；超出则待机。")]
    public float detectionRadius = 8f;
    [Tooltip("普攻范围：玩家在此半径内，AI 才能尝试普攻。与技能范围相互独立。")]
    public float basicAttackRange = 3f;
    [Tooltip("技能范围：玩家在此半径内，AI 才能释放技能。与普攻范围相互独立。")]
    public float skillAttackRange = 6f;
    [Tooltip("AI 停步距离：玩家在此距离内不再前进（只侧移）。")]
    public float aiMinRange = 0f;

    [Header("攻击节奏（冷却以技能自身 cooldown 为准）")]
    [Tooltip("攻击迟疑度（0~1）：技能/普攻 CD 就绪后，每决策拍仅有该概率真正出手；否则该拍放弃、等下一决策拍。1 = CD 好了立刻放，0 = 永不出手（仅调试用）。用于让 AI 不「CD 一好就无缝放」，保留攻击间隙。")]
    [Range(0f, 1f)] public float attackEagerness = 0.8f;

    [Header("决策节拍（随机化打散）")]
    [Tooltip("决策节拍最小间隔（秒）。每只怪每次决策在 [min, max] 内随机取，间隔抖动 + 相位随机化，避免同类怪同步行动。")]
    public float decisionIntervalMin = 0.12f;
    [Tooltip("决策节拍最大间隔（秒）。")]
    public float decisionIntervalMax = 0.4f;

    [Header("行为权重")]
    [Tooltip("攻击范围内技能优先概率（0~1）。技能与普攻都就绪时，按该权重随机选中技能；同一决策节拍只执行一种攻击。")]
    [Range(0f, 1f)] public float skillPriority = 0.6f;
    [Tooltip("攻击范围外追击时触发位移技能（冲刺）的概率（0~1）。位移冷却未就绪时自动回退普通追击。")]
    [Range(0f, 1f)] public float aiMobilityChance = 0.3f;

    [Header("追击时长")]
    [Tooltip("连续直线追击时长上限（秒）。追击超过此时长仍未进入攻击范围，则转为对峙状态（随机角度游走、面向玩家方向随机但不背离玩家）。0 = 不限时长，一直直线追击。")]
    public float chaseDuration = 0f;

    [Header("追击走位")]
    [Tooltip("追击时随机走位概率：每次走位刷新有该概率侧移，否则直线追击。")]
    [Range(0f, 1f)] public float strafeChance = 0.4f;
    [Tooltip("走位刷新随机间隔范围（秒）。")]
    public float strafeIntervalMin = 0.3f;
    public float strafeIntervalMax = 0.9f;
    [Tooltip("侧移分量强度（0~1，1=完全横向）。")]
    [Range(0f, 1f)] public float strafeStrength = 0.45f;
    [Tooltip("追击速度随机抖动范围（乘数，作用于 moveSpeed）。")]
    public float moveSpeedJitterMin = 0.7f;
    public float moveSpeedJitterMax = 1.3f;

    [Header("AI Movement Smoothing")]
    [Tooltip("AI 加速到目标移动速度的速率（单位/秒²）。")]
    public float moveAcceleration = 20f;
    [Tooltip("AI 停止或改变走位时的减速速率（单位/秒²）。")]
    public float moveDeceleration = 28f;
    [Tooltip("AI 最大转向速度（度/秒）。")]
    public float turnSpeed = 540f;
    [Tooltip("AI 转向速度达到最大值的加速度（度/秒²）。")]
    public float turnAcceleration = 1440f;

    [Header("调试可视化")]
    [Tooltip("在游戏视图中用圆环可视化索敌/普攻/技能范围（Play 模式可见，运行中可勾选/取消）。")]
    public bool showDebugRanges = false;
}

/// <summary>
/// 怪物 AI 配置库：1 个 SO 资产 = 全部怪的 AI 配置（同 CardLibrary 模式）。
/// 所有怪物 prefab 通过 MonsterActor.aiConfig + aiConfigId 引用同一条目，
/// 资产集中存放在 Assets/Configs/。OnValidate 编辑期查重，重复 id 给出警告。
/// </summary>
[CreateAssetMenu(fileName = "MonsterAIConfig", menuName = "Possession/AI/Monster AI Config")]
public class MonsterAIConfig : ScriptableObject
{
    [Tooltip("全部怪的 AI 配置。id 需全局唯一（OnValidate 查重，运行时重复项忽略）。")]
    public List<MonsterAIConfigEntry> entries = new List<MonsterAIConfigEntry>();

    /// <summary>按 id 查配置（线性查找，条目数量少；未命中返回 null）。</summary>
    public MonsterAIConfigEntry Get(string id)
    {
        if (string.IsNullOrEmpty(id) || entries == null) return null;
        for (int i = 0; i < entries.Count; i++)
            if (entries[i] != null && entries[i].id == id) return entries[i];
        return null;
    }

    /// <summary>未配置/未命中时共享的默认条目（纯 C# 对象，字段为默认值）。</summary>
    static MonsterAIConfigEntry _defaultEntry;
    public static MonsterAIConfigEntry DefaultEntry
    {
        get
        {
            if (_defaultEntry == null) _defaultEntry = new MonsterAIConfigEntry();
            return _defaultEntry;
        }
    }

#if UNITY_EDITOR
    /// <summary>编辑器配置防御：id 查重 + 非法半径/区间组合警告，避免运行时行为异常难以排查。</summary>
    void OnValidate()
    {
        var seen = new HashSet<string>();
        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            if (!seen.Add(e.id))
            {
                Debug.LogWarning($"[MonsterAIConfig] duplicate id '{e.id}' in {name}; later entries ignored at runtime.", this);
                continue;
            }
            if (e.basicAttackRange > e.detectionRadius)
                Debug.LogWarning($"[MonsterAIConfig] {e.id}: basicAttackRange({e.basicAttackRange}) > detectionRadius({e.detectionRadius})，普攻范围超出索敌半径，索敌将失效。", this);
            if (e.skillAttackRange > e.detectionRadius)
                Debug.LogWarning($"[MonsterAIConfig] {e.id}: skillAttackRange({e.skillAttackRange}) > detectionRadius({e.detectionRadius})，技能范围超出索敌半径，索敌将失效。", this);
            if (e.decisionIntervalMin > e.decisionIntervalMax)
                Debug.LogWarning($"[MonsterAIConfig] {e.id}: decisionIntervalMin({e.decisionIntervalMin}) > decisionIntervalMax({e.decisionIntervalMax})，决策间隔将随机抖动为负。", this);
            if (e.strafeIntervalMin > e.strafeIntervalMax)
                Debug.LogWarning($"[MonsterAIConfig] {e.id}: strafeIntervalMin({e.strafeIntervalMin}) > strafeIntervalMax({e.strafeIntervalMax})。", this);
            if (e.moveSpeedJitterMin > e.moveSpeedJitterMax)
                Debug.LogWarning($"[MonsterAIConfig] {e.id}: moveSpeedJitterMin({e.moveSpeedJitterMin}) > moveSpeedJitterMax({e.moveSpeedJitterMax})。", this);
            if (e.moveAcceleration < 0f || e.moveDeceleration < 0f || e.turnSpeed < 0f || e.turnAcceleration < 0f)
                Debug.LogWarning($"[MonsterAIConfig] {e.id}: movement smoothing values must be non-negative.", this);
        }
    }
#endif
}
