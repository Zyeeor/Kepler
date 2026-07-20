using UnityEngine;

/// <summary>
/// Passive component on an enemy prefab. Defines a permanent stat bonus that gets
/// accumulated onto the player when this enemy is possessed.
/// Supports: moveSpeed, lifesteal
/// </summary>
public class EnemyPassiveBuff : EnemyAbility
{
    [Header("Passive Buff (permanent, stackable)")]
    [Tooltip("Move speed bonus (additive). 5 = +5% move speed.")]
    public float moveSpeedBonusPercent = 0f;
    [Tooltip("Lifesteal bonus (additive). 1 = +1% lifesteal (0.01).")]
    public float lifestealBonusPercent = 0f;

    private void OnEnable()
    {
        type = AbilityType.Passive;
        abilityName = "被动";
    }

    protected override void OnTrigger()
    {
        // Passive: no manual trigger
    }

    public override bool CanTrigger() { return false; }
}
