using System;
using UnityEngine;

[Serializable]
public sealed class ContinuousSpawnEntry
{
    [Tooltip("普通怪轮换罪印，同时也是该条精英的生成类型。")]
    public SinType sin;
    [Tooltip("从战斗开始计时的精英生成时刻（秒）。")]
    [Min(0f)] public float eliteSpawnTimeSeconds;
    [Tooltip("精英生命值系数。最终生命值 = 基础生命值 × 当前生命曲线 × 此系数。")]
    [Min(0.01f)] public float eliteHealthMultiplier = 2f;
    [Tooltip("精英攻击力系数。最终攻击力 = 基础攻击力 × 当前攻击曲线 × 此系数。")]
    [Min(0.01f)] public float eliteAttackMultiplier = 2f;
    [Tooltip("击杀该精英后的选卡次数：1 = 单选（1 Gem → 1 次选卡）；2 = 双选（1 Gem → 连续 2 次选卡，复用 doublePick 机制）。")]
    [Min(1)] public int eliteRewardPickCount = 1;

    public ContinuousSpawnEntry(SinType sin, float eliteSpawnTimeSeconds,
        float eliteHealthMultiplier, float eliteAttackMultiplier, int eliteRewardPickCount = 1)
    {
        this.sin = sin;
        this.eliteSpawnTimeSeconds = eliteSpawnTimeSeconds;
        this.eliteHealthMultiplier = eliteHealthMultiplier;
        this.eliteAttackMultiplier = eliteAttackMultiplier;
        this.eliteRewardPickCount = eliteRewardPickCount;
    }
}
