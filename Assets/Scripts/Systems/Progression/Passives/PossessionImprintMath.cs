using UnityEngine;

/// <summary>Pure formulas for the run-level seven-sin imprint system.</summary>
public static class PossessionImprintMath
{
    /// <summary>Single run-wide cap for every sin imprint stack.</summary>
    public static int MaxStacks = 100;

    public const float MaxWrathDamageBonus = 1.20f;
    public const float MaxGluttonyHealthBonus = 1.00f;
    public const float MaxGluttonyScaleBonus = 0.25f;
    public const float MaxEnvyBulletTimeBonus = 3.00f;
    public const float MaxLustControlChance = 0.30f;
    public const float MaxSlothDrainReduction = 0.60f;

    static int ClampStacks(int stacks)
    {
        return Mathf.Clamp(stacks, 0, Mathf.Max(1, MaxStacks));
    }

    public static float PrideCooldownMultiplier(int stacks)
    {
        return 1f / (1f + ClampStacks(stacks) * 0.05f);
    }

    public static float WrathDamageMultiplier(int stacks)
    {
        return 1f + Mathf.Min(MaxWrathDamageBonus, ClampStacks(stacks) * 0.06f);
    }

    public static float GluttonyHealthMultiplier(int stacks)
    {
        return 1f + Mathf.Min(MaxGluttonyHealthBonus, ClampStacks(stacks) * 0.05f);
    }

    public static float GluttonyScaleMultiplier(int stacks)
    {
        return 1f + Mathf.Min(MaxGluttonyScaleBonus, ClampStacks(stacks) * 0.025f);
    }

    public static float GreedProgressPerPossession(int oldStacks)
    {
        return Mathf.Min(ClampStacks(oldStacks) * 0.05f, 1f);
    }

    public static float EnvyBulletTimeBonus(int stacks)
    {
        return Mathf.Min(MaxEnvyBulletTimeBonus, ClampStacks(stacks) * 0.15f);
    }

    public static float LustControlChance(int stacks)
    {
        return Mathf.Min(MaxLustControlChance, ClampStacks(stacks) * 0.02f);
    }

    /// <summary>Multiplier applied to possessed-body HP drain, not incoming combat damage.</summary>
    public static float SlothDrainMultiplier(int stacks)
    {
        float reduction = Mathf.Min(MaxSlothDrainReduction, 1f - 1f / (1f + ClampStacks(stacks) * 0.04f));
        return 1f - reduction;
    }

    /// <summary>
    /// Applies a single real possession transaction using the previous Greed stack count.
    /// Returns the extra Greed stack earned by deterministic progress (0 or 1).
    /// </summary>
    public static int ApplyTransaction(int[] stacks, ref float greedProgress, SinType target)
    {
        if (stacks == null || stacks.Length < 8) return 0;
        int index = (int)target;
        if (index <= 0 || index >= stacks.Length) return 0;

        int cap = Mathf.Max(1, MaxStacks);
        int oldGreed = ClampStacks(stacks[(int)SinType.Greed]);
        stacks[index] = Mathf.Min(cap, ClampStacks(stacks[index]) + 1);
        greedProgress += GreedProgressPerPossession(oldGreed);
        if (greedProgress < 1f) return 0;

        greedProgress -= 1f;
        stacks[index] = Mathf.Min(cap, stacks[index] + 1);
        return 1;
    }
}
