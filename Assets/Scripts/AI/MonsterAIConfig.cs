using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 怪物 AI 配置条目：内联数据（非 SO），按 id 从 MonsterAIConfig 查询。
/// 攻击范围拆分：basicAttackRange（普攻）与 skillAttackRange（技能）相互独立、无大小关系。
///
/// 字段分 7 组（Inspector 按编号排布，调参/调试按症状定位）：
///   ① 范围/索敌 —— detectionRadius / basicAttackRange / skillAttackRange / aiMinRange
///   ② 解锁范围覆盖 —— rangeUnlocks（卡解锁后范围提升，取 max）
///   ③ 攻击决策 —— attackEagerness / decisionInterval / skillPriority / aiMobilityChance / chaseDuration
///   ④ 追击走位 —— strafeChance / strafeInterval / strafeStrength / moveSpeedJitter
///   ⑤ 移动平滑 —— moveAcceleration / moveDeceleration / turnSpeed / turnAcceleration
///   ⑥ 软分离 —— separationRadius / separationStrength
///   ⑦ 调试 —— showDebugRanges
///
/// 调试速查（按症状）：
///   怪完全不动/不发现玩家 → ① detectionRadius 是否太小（不被发现）；或开 ⑦ 圈核对范围。
///   能靠近却不开打 → basicAttackRange/skillAttackRange 是否 &gt; detectionRadius（脱节），或 ③ attackEagerness 过低。
///   怪黏成一团 → ⑥ separationStrength / separationRadius 调大。
///   同类怪整齐同步 → ③ decisionInterval、④ strafeInterval / moveSpeedJitter 随机区间太窄。
///   怪太凶/太肉 → ③ attackEagerness、skillPriority；⑤ 加速度。
/// 注：范围字段每帧实时读取（无快照），② 的解锁覆盖也实时生效，改完下一帧即见。
/// </summary>
[System.Serializable]
public class MonsterAIConfigEntry
{
    [Tooltip("配置唯一 id（全局唯一）。怪物 prefab 通过 MonsterActor.aiConfigId 引用；填错/不匹配会静默落到默认配置（见 MonsterActor.OnValidate 警告）。")]
    public string id;

    [Space]
    [Header("① 范围 / 索敌")]
    [Tooltip("索敌半径：玩家进入此半径，AI 才「发现」并行动（追击/攻击）；超出则待机。常见坑：basic/skillAttackRange 不得大于它，否则「能打却先得被发现」会脱节。")]
    public float detectionRadius = 8f;
    [Tooltip("普攻范围：玩家在此半径内才允许普攻。与技能范围相互独立，无大小关系（远程怪技能范围可远大于普攻）。")]
    public float basicAttackRange = 3f;
    [Tooltip("技能范围：玩家在此半径内才允许放技能。与普攻范围相互独立。")]
    public float skillAttackRange = 6f;
    [Tooltip("停步距离：玩家进入此距离内，AI 停止前进、只做侧移/攻击。太大=远处发呆不过来；0=贴脸才停。")]
    public float aiMinRange = 0f;

    [Space]
    [Header("② 解锁驱动的范围覆盖（取最大值）")]
    [Tooltip("解锁后范围覆盖列表。每条 = 解锁某能力（卡牌 effectId）后，把普攻/技能范围提升到指定值；最终生效 = max(基础值, 所有已解锁条目)。unlockId 一般为卡 effectId；范围填 -1 表示该项不改。详见 AIRangeUnlock。")]
    public List<AIRangeUnlock> rangeUnlocks = new List<AIRangeUnlock>();

    [Space]
    [Header("③ 攻击决策")]
    [Tooltip("攻击迟疑度（0~1）：技能/普攻 CD 就绪后，每个决策拍仅有该概率真正出手，否则本拍放弃。1=CD 好立刻放（最凶），0=永不出手（仅调试）。用于制造攻击间隙。")]
    [Range(0f, 1f)] public float attackEagerness = 0.8f;
    [Tooltip("决策节拍随机区间（秒）：每只怪在 [min,max] 内随机取决策间隔，配合相位随机化避免同类怪同步。都调小=反应更快更密集。")]
    public float decisionIntervalMin = 0.12f;
    public float decisionIntervalMax = 0.4f;
    [Tooltip("技能优先概率（0~1）：攻击范围内技能与普攻都就绪时，按此权重随机选技能；同一决策拍只放一种。0=只用普攻，1=只用技能。")]
    [Range(0f, 1f)] public float skillPriority = 0.6f;
    [Tooltip("位移技能触发概率（0~1）：攻击范围外追击时，按此概率尝试放位移技能（冲刺）赶路；位移冷却未就绪则自动回退普通追击。0=从不位移赶路。")]
    [Range(0f, 1f)] public float aiMobilityChance = 0.3f;
    [Tooltip("直线追击时长上限（秒）：追击超过此值仍未进攻击范围，转「对峙」（随机角度游走、面向玩家但不背离）。0=不限时一直直线追。调小可更快变灵活走位。")]
    public float chaseDuration = 0f;

    [Space]
    [Header("④ 追击走位 / 移动抖动")]
    [Tooltip("走位概率（0~1）：每次走位刷新有此概率改为侧移，否则直线追。高=爱绕，低=直冲。")]
    [Range(0f, 1f)] public float strafeChance = 0.4f;
    [Tooltip("走位刷新随机间隔范围（秒）。")]
    public float strafeIntervalMin = 0.3f;
    public float strafeIntervalMax = 0.9f;
    [Tooltip("侧移强度（0~1，1=完全横向）：越大走位越「横」，越小越「斜前」。")]
    [Range(0f, 1f)] public float strafeStrength = 0.45f;
    [Tooltip("追击速度随机抖动乘数范围：实际 moveSpeed 在 [min,max]×基础速度间随机，制造速度差、避免整齐划一。")]
    public float moveSpeedJitterMin = 0.7f;
    public float moveSpeedJitterMax = 1.3f;

    [Space]
    [Header("⑤ 移动平滑（加速度 / 转向）")]
    [Tooltip("移动加速度（单位/秒²）：AI 提速到目标速度的快慢。小=起步肉，大=瞬间响应。")]
    public float moveAcceleration = 20f;
    [Tooltip("移动减速度（单位/秒²）：停下/变向时的减速快慢。")]
    public float moveDeceleration = 28f;
    [Tooltip("最大转向速度（度/秒）。")]
    public float turnSpeed = 540f;
    [Tooltip("转向加速度（度/秒²）：转向提速快慢。")]
    public float turnAcceleration = 1440f;

    [Space]
    [Header("⑥ 怪物间软分离（防重叠）")]
    [Tooltip("分离半径（米）：与其它活怪水平距离小于此值时产生排斥。0=关闭分离（会叠在一起）。")]
    public float separationRadius = 1.2f;
    [Tooltip("分离强度（0~2）：分离速度=强度×moveSpeed。大=散得猛，小=挤一起也柔和。")]
    [Range(0f, 2f)] public float separationStrength = 0.8f;

    [Space]
    [Header("⑦ 调试")]
    [Tooltip("勾选后在 Game 视图用圆环可视化 索敌/普攻/技能 范围（Play 模式可见，运行时可随时开关）。调试范围/索敌必开。")]
    public bool showDebugRanges = false;

    /// <summary>生效普攻范围 = max(基础, 已解锁覆盖)。每次访问实时计算（无缓存），解锁变化下一帧即生效。</summary>
    public float EffectiveBasicAttackRange()
    {
        float v = basicAttackRange;
        if (rangeUnlocks != null)
            foreach (var u in rangeUnlocks)
                if (u != null && u.basicAttackRange >= 0f && MonsterAIConfig.IsUnlocked(u.unlockId))
                    v = Mathf.Max(v, u.basicAttackRange);
        return v;
    }

    /// <summary>生效技能范围 = max(基础, 已解锁覆盖)。</summary>
    public float EffectiveSkillAttackRange()
    {
        float v = skillAttackRange;
        if (rangeUnlocks != null)
            foreach (var u in rangeUnlocks)
                if (u != null && u.skillAttackRange >= 0f && MonsterAIConfig.IsUnlocked(u.unlockId))
                    v = Mathf.Max(v, u.skillAttackRange);
        return v;
    }
}

/// <summary>单条「解锁 → 范围覆盖」配置（策划在 MonsterAIConfigEntry.rangeUnlocks 中填写）。</summary>
[System.Serializable]
public class AIRangeUnlock
{
    [Tooltip("解锁标识：通常为卡牌 effectId（CardManager.UnlockedEffects 中存在即视为已解锁）。")]
    public string unlockId;
    [Tooltip("解锁后普攻范围（取大用）。-1 = 不修改普攻范围。")]
    public float basicAttackRange = -1f;
    [Tooltip("解锁后技能范围（取大用）。-1 = 不修改技能范围。")]
    public float skillAttackRange = -1f;
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

    /// <summary>解锁判定钩子：由游戏进度系统（如 CardManager）在启动时注入；默认未解锁。
    /// 入参为 MonsterAIConfigEntry.rangeUnlocks[].unlockId（通常即卡牌 effectId）。</summary>
    public static System.Func<string, bool> IsUnlocked = _ => false;

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

            if (e.rangeUnlocks != null)
            {
                foreach (var u in e.rangeUnlocks)
                {
                    if (u == null) continue;
                    if (string.IsNullOrEmpty(u.unlockId))
                        Debug.LogWarning($"[MonsterAIConfig] {e.id}: rangeUnlocks 含空 unlockId（不会被任何解锁触发）。", this);
                    if (u.basicAttackRange >= 0f && u.basicAttackRange > e.detectionRadius)
                        Debug.LogWarning($"[MonsterAIConfig] {e.id}: rangeUnlocks basicAttackRange({u.basicAttackRange}) > detectionRadius({e.detectionRadius})，普攻范围超出索敌半径，索敌将失效。", this);
                    if (u.skillAttackRange >= 0f && u.skillAttackRange > e.detectionRadius)
                        Debug.LogWarning($"[MonsterAIConfig] {e.id}: rangeUnlocks skillAttackRange({u.skillAttackRange}) > detectionRadius({e.detectionRadius})，技能范围超出索敌半径，索敌将失效。", this);
                }
            }

            e.skillPriority = Clamp01Warn(e.id, nameof(e.skillPriority), e.skillPriority);
            e.aiMobilityChance = Clamp01Warn(e.id, nameof(e.aiMobilityChance), e.aiMobilityChance);
            e.attackEagerness = Clamp01Warn(e.id, nameof(e.attackEagerness), e.attackEagerness);
            e.strafeStrength = Clamp01Warn(e.id, nameof(e.strafeStrength), e.strafeStrength);
        }
    }

    static float Clamp01Warn(string id, string field, float v)
    {
        if (v < 0f || v > 1f)
        {
            Debug.LogWarning($"[MonsterAIConfig] {id}: {field}({v}) 超出 [0,1]，已钳制。");
            return Mathf.Clamp(v, 0f, 1f);
        }
        return v;
    }
#endif
}
