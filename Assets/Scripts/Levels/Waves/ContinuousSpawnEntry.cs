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

    public ContinuousSpawnEntry(SinType sin, float eliteSpawnTimeSeconds,
        float eliteHealthMultiplier, float eliteAttackMultiplier)
    {
        this.sin = sin;
        this.eliteSpawnTimeSeconds = eliteSpawnTimeSeconds;
        this.eliteHealthMultiplier = eliteHealthMultiplier;
        this.eliteAttackMultiplier = eliteAttackMultiplier;
    }
}
