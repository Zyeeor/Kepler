using UnityEngine;

public static class MonsterSpawnDifficulty
{
    public static int TierAt(float activeCombatSeconds)
        => TierAt(activeCombatSeconds, 30f);

    public static int TierAt(float activeCombatSeconds, float growthIntervalSeconds)
    {
        return Mathf.Max(0, Mathf.FloorToInt(Mathf.Max(0f, activeCombatSeconds) / Mathf.Max(0.1f, growthIntervalSeconds)));
    }

    public static float HealthMultiplier(int tier)
    {
        return Mathf.Min(3f, 1f + Mathf.Max(0, tier) * 0.10f);
    }

    public static float DamageMultiplier(int tier)
    {
        return Mathf.Min(2.2f, 1f + Mathf.Max(0, tier) * 0.06f);
    }
}
