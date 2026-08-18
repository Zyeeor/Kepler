using System;
using UnityEngine;

/// <summary>
/// One configurable combat/movement stat snapshot for a monster.
/// Prefabs author two blocks: enemy (AI) and possessed (player-controlled body).
/// </summary>
[Serializable]
public struct MonsterStatBlock
{
    [Min(0f)] public float maxHealth;
    [Min(0f)] public float moveSpeed;
    [Min(0f)] public float acceleration;
    [Min(0f)] public float deceleration;
    [Min(0f)] public float maxTenacity;
    [Min(0f)] public float collisionDamage;
    [Tooltip("Attack speed multiplier. 1.0 = normal.")]
    [Min(0.01f)] public float attackSpeed;

    public static MonsterStatBlock FromRuntime(
        float maxHealth,
        float moveSpeed,
        float acceleration,
        float deceleration,
        float maxTenacity,
        float collisionDamage,
        float attackSpeed)
    {
        return new MonsterStatBlock
        {
            maxHealth = maxHealth,
            moveSpeed = moveSpeed,
            acceleration = acceleration,
            deceleration = deceleration,
            maxTenacity = maxTenacity,
            collisionDamage = collisionDamage,
            attackSpeed = attackSpeed > 0f ? attackSpeed : 1f
        };
    }

    public bool HasConfiguredHealth => maxHealth > 0f;
}
