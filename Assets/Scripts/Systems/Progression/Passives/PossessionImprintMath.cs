using UnityEngine;

/// <summary>Pure formulas for the run-level seven-sin imprint system.</summary>
public static class PossessionImprintMath
{
    /// <summary>Single run-wide cap for every sin imprint stack.</summary>
    public static int MaxStacks = 100;

    public const float MaxGluttonyScaleMultiplier = 2.00f;
    const float PrideCooldownFactorPerStack = 7f / 60f;
    const float WrathDamagePerStack = 0.10f;
    const float GluttonyHealthPerStack = 0.10f;
    const float GluttonyScalePerStack = 0.05f;
    const float GreedProgressPerStack = 0.10f;
    const float EnvyMoveSpeedPerStack = 0.01f;
    const float EnvyMoveSpeedMaxBonus = 0.50f;
    const float LustLifestealPerStack = 0.01f;
    const float SlothDrainFactorPerStack = 7f / 60f;

    static int ClampStacks(int stacks)
    {
        return Mathf.Clamp(stacks, 0, Mathf.Max(1, MaxStacks));
    }

    public static float PrideCooldownMultiplier(int stacks)
    {
        return 1f / (1f + ClampStacks(stacks) * PrideCooldownFactorPerStack);
    }

    public static float WrathDamageMultiplier(int stacks)
    {
        return 1f + ClampStacks(stacks) * WrathDamagePerStack;
    }

    public static float GluttonyHealthMultiplier(int stacks)
    {
        return 1f + ClampStacks(stacks) * GluttonyHealthPerStack;
    }

    public static float GluttonyScaleMultiplier(int stacks)
    {
        return Mathf.Min(MaxGluttonyScaleMultiplier, 1f + ClampStacks(stacks) * GluttonyScalePerStack);
    }

    public static float GreedProgressPerPossession(int oldStacks)
    {
        return ClampStacks(oldStacks) * GreedProgressPerStack;
    }

    /// <summary>Envy 全局移动速度加成（每层 +1%，上限 +50%）。</summary>
    public static float EnvyMoveSpeedBonus(int stacks)
    {
        return Mathf.Min(EnvyMoveSpeedMaxBonus, ClampStacks(stacks) * EnvyMoveSpeedPerStack);
    }

    public static float LustLifestealMultiplier(int stacks)
    {
        return ClampStacks(stacks) * LustLifestealPerStack;
    }

    /// <summary>Multiplier applied to possessed-body HP drain, not incoming combat damage.</summary>
    public static float SlothDrainMultiplier(int stacks)
    {
        return 1f / (1f + ClampStacks(stacks) * SlothDrainFactorPerStack);
    }

    /// <summary>
    /// Applies a single real possession transaction using the previous Greed stack count.
    /// The guaranteed integer bonus is granted immediately and the fractional remainder is
    /// rolled once as a probability. The ref parameter remains for save/API compatibility,
    /// but fractional progress is no longer carried between possessions.
    /// </summary>
    public static int ApplyTransaction(int[] stacks, ref float greedProgress, SinType target)
    {
        if (stacks == null || stacks.Length < 8) return 0;
        int index = (int)target;
        if (index <= 0 || index >= stacks.Length) return 0;

        int cap = Mathf.Max(1, MaxStacks);
        int oldGreed = ClampStacks(stacks[(int)SinType.Greed]);
        stacks[index] = Mathf.Min(cap, ClampStacks(stacks[index]) + 1);
        float bonusProgress = GreedProgressPerPossession(oldGreed);
        int extraStacks = Mathf.FloorToInt(bonusProgress);
        float fractionalProgress = bonusProgress - extraStacks;
        if (fractionalProgress > 0f && UnityEngine.Random.value < fractionalProgress)
            extraStacks++;

        greedProgress = 0f;
        stacks[index] = Mathf.Min(cap, stacks[index] + extraStacks);
        return extraStacks;
    }
}
