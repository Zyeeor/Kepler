using UnityEngine;

public static class MonsterSpawnDifficulty
{
    public static int TierAt(float activeCombatSeconds)
    {
        return Mathf.Max(0, Mathf.FloorToInt(Mathf.Max(0f, activeCombatSeconds) / 30f));
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
