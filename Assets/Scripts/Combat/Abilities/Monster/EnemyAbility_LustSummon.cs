using UnityEngine;

/// <summary>Lust skill: spawn configured minions around the owner for their own prefab-defined behavior.</summary>
public class EnemyAbility_LustSummon : EnemyAbility
{
    public GameObject minionPrefab;
    public int summonCount = 1;
    public float spawnRadius = 1.5f;
    public float minionLifetime = 8f;

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "召唤小怪";
        cooldown = cooldown <= 0f ? 8f : cooldown;
    }

    protected override void OnTrigger()
    {
        if (owner == null || minionPrefab == null) { EndActivationEffect(); return; }
        int count = Mathf.RoundToInt(GetCardParameter("SummonCount", summonCount));
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = (owner != null ? owner.AiRandomInsideUnitCircle() : Random.insideUnitCircle)
                * spawnRadius * OwnerCombatScaleMultiplier;
            SpawnVfxTracked(minionPrefab, owner.transform.position + new Vector3(offset.x, 0f, offset.y), Quaternion.identity, minionLifetime);
        }
        EndActivationEffect();
    }
}
