using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Run-scoped seven-sin imprint state. It is deliberately separate from CardManager and
/// consumes exactly one PossessionCommitted transaction id per real possession.
/// </summary>
public sealed class PossessionImprintManager : MonoBehaviour
{
    public static PossessionImprintManager Instance { get; private set; }

    readonly int[] stacks = new int[8];
    readonly int[] displayedStacks = new int[8];
    readonly HashSet<long> consumedTransactions = new HashSet<long>();
    readonly HashSet<SinType> shownTutorials = new HashSet<SinType>();
    PossessionManager attachedPossessionManager;
    bool hasRun;
    bool restoredRun;
    int visualGeneration;

    public float GreedBonusProgress { get; private set; }
    public float LustHealProgress { get; private set; }
    public int GetStacks(SinType sin) => (int)sin > 0 && (int)sin < stacks.Length
        ? Mathf.Clamp(stacks[(int)sin], 0, Mathf.Max(1, PossessionImprintMath.MaxStacks))
        : 0;
    public int GetDisplayedStacks(SinType sin) => (int)sin > 0 && (int)sin < displayedStacks.Length
        ? Mathf.Clamp(displayedStacks[(int)sin], 0, Mathf.Max(1, PossessionImprintMath.MaxStacks))
        : 0;
    public IReadOnlyList<int> Stacks => stacks;
    public bool HasRun => hasRun;
    public bool IsRestoredRun => restoredRun;

    public static PossessionImprintManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("[PossessionImprints]");
        DontDestroyOnLoad(go);
        return go.AddComponent<PossessionImprintManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        Detach();
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (attachedPossessionManager == null || attachedPossessionManager != PossessionManager.Instance)
            Attach(PossessionManager.Instance);
    }

    public void Attach(PossessionManager manager)
    {
        if (attachedPossessionManager == manager) return;
        Detach();
        attachedPossessionManager = manager;
        if (attachedPossessionManager != null)
            attachedPossessionManager.PossessionCommitted += HandlePossessionCommitted;
    }

    void Detach()
    {
        if (attachedPossessionManager != null)
            attachedPossessionManager.PossessionCommitted -= HandlePossessionCommitted;
        attachedPossessionManager = null;
    }

    public void BeginNewRun()
    {
        visualGeneration++;
        Array.Clear(stacks, 0, stacks.Length);
        Array.Clear(displayedStacks, 0, displayedStacks.Length);
        consumedTransactions.Clear();
        shownTutorials.Clear();
        GreedBonusProgress = 0f;
        LustHealProgress = 0f;
        hasRun = true;
        restoredRun = false;
    }

    /// <summary>Boss 模式开局：七种罪印统一获得可配置层数。</summary>
    public void BeginBossModeRun(int initialStacks)
    {
        BeginNewRun();
        int value = Mathf.Clamp(initialStacks, 0, Mathf.Max(1, PossessionImprintMath.MaxStacks));
        for (int i = 1; i < stacks.Length; i++)
        {
            stacks[i] = value;
            displayedStacks[i] = value;
        }
        GreedBonusProgress = PossessionImprintMath.GreedProgressPerPossession(value);
        hasRun = true;
        restoredRun = false;
    }

    public void EndRun()
    {
        BeginNewRun();
        hasRun = false;
    }

    public void LoadFromSave(List<PossessionImprintState> saved, float greedProgress, float lustHealProgress = 0f)
    {
        visualGeneration++;
        Array.Clear(stacks, 0, stacks.Length);
        Array.Clear(displayedStacks, 0, displayedStacks.Length);
        consumedTransactions.Clear();
        shownTutorials.Clear();
        if (saved != null)
        {
            for (int i = 0; i < saved.Count; i++)
            {
                int index = (int)saved[i].sin;
                if (index >= 0 && index < stacks.Length)
                    stacks[index] = Mathf.Clamp(saved[i].stacks, 0, Mathf.Max(1, PossessionImprintMath.MaxStacks));
            }
        }
        // Fractional Greed progress is now rolled immediately per possession. Rebuild this
        // value as the next-possession preview and discard legacy carried progress.
        GreedBonusProgress = 0f;
        LustHealProgress = Mathf.Max(0f, lustHealProgress);
        GreedBonusProgress = PossessionImprintMath.GreedProgressPerPossession(stacks[(int)SinType.Greed]);
        Array.Copy(stacks, displayedStacks, stacks.Length);
        hasRun = true;
        restoredRun = true;
    }

    public List<PossessionImprintState> CaptureStates()
    {
        var result = new List<PossessionImprintState>(7);
        for (int i = 1; i < stacks.Length; i++)
            result.Add(new PossessionImprintState((SinType)i, stacks[i]));
        return result;
    }

    void HandlePossessionCommitted(MonsterActor body, PossessionGrantReason reason, long transactionId)
    {
        if (body == null || !IsRealPossession(reason))
        {
            Debug.Log($"[PossessionImprint] Commit ignored: body={(body != null ? body.name : "NULL")}, reason={reason}, transaction={transactionId}.");
            return;
        }

        Debug.Log($"[PossessionImprint] Commit received: body='{body.name}', displayName='{body.displayName}', reason={reason}, transaction={transactionId}, world={body.transform.position}, active={body.isActiveAndEnabled}, possessed={body.isPossessed}, managerHasRun={hasRun}.");
        body.ResolveSinIdentityFromHint(body.name + " " + body.displayName);
        if (body.sinType == SinType.None)
        {
            Debug.LogWarning("[PossessionImprint] 无法解析附身体罪印身份：" + body.name);
            return;
        }
        if (!consumedTransactions.Add(transactionId))
        {
            Debug.Log($"[PossessionImprint] Commit ignored as duplicate: body='{body.name}', transaction={transactionId}.");
            return;
        }
        if (!hasRun) BeginNewRun();
        int oldStackCount = stacks[(int)body.sinType];
        int oldDisplayedStackCount = displayedStacks[(int)body.sinType];
        float greedProgress = 0f;
        PossessionImprintMath.ApplyTransaction(stacks, ref greedProgress, body.sinType);
        GreedBonusProgress = PossessionImprintMath.GreedProgressPerPossession(stacks[(int)SinType.Greed]);
        ApplyBodyEffects(body);

        int gainedStacks = Mathf.Max(0, stacks[(int)body.sinType] - oldStackCount);
        Debug.Log($"[PossessionImprint] Transaction applied: sin={body.sinType}, actualStacks={oldStackCount}->{stacks[(int)body.sinType]}, displayedStacks={oldDisplayedStackCount}, gained={gainedStacks}, gainListenerCount={(OnImprintGainRequested != null ? OnImprintGainRequested.GetInvocationList().Length : 0)}.");
        if (gainedStacks <= 0)
        {
            Debug.Log($"[PossessionImprint] No visual gain requested: sin={body.sinType}, transaction={transactionId}.");
            return;
        }

        int generation = visualGeneration;
        bool completed = false;
        Action completeVisualGain = () =>
        {
            if (completed || generation != visualGeneration) return;
            completed = true;
            displayedStacks[(int)body.sinType] = Mathf.Clamp(
                displayedStacks[(int)body.sinType] + gainedStacks,
                0,
                Mathf.Max(1, PossessionImprintMath.MaxStacks));
            Debug.Log($"[PossessionImprint] Visual gain completed: sin={body.sinType}, displayedStacks={oldDisplayedStackCount}->{displayedStacks[(int)body.sinType]}, gained={gainedStacks}, transaction={transactionId}.");
            OnImprintChanged?.Invoke(body.sinType, displayedStacks[(int)body.sinType]);
        };

        if (OnImprintGainRequested != null)
        {
            Debug.Log($"[PossessionImprint] Visual gain requested: sin={body.sinType}, body='{body.name}', gained={gainedStacks}, transaction={transactionId}.");
            OnImprintGainRequested.Invoke(body.sinType, body, gainedStacks, completeVisualGain);
        }
        else
        {
            Debug.LogWarning($"[PossessionImprint] No visual listener; completing immediately: sin={body.sinType}, transaction={transactionId}.");
            completeVisualGain();
        }
    }

    static bool IsRealPossession(PossessionGrantReason reason)
    {
        return reason == PossessionGrantReason.InitialAssignment
            || reason == PossessionGrantReason.PlayerPossession
            || reason == PossessionGrantReason.DeathRelay;
    }

    public event Action<SinType, int> OnImprintChanged;
    public event Action<SinType, MonsterActor, int, Action> OnImprintGainRequested;

    public void ApplyBodyEffects(MonsterActor body)
    {
        if (body == null || !body.isPossessed) return;
        int gluttonyStacks = GetStacks(SinType.Gluttony);
        float healthMultiplier = PossessionImprintMath.GluttonyHealthMultiplier(gluttonyStacks);
        body.ApplyPossessionImprintStats(healthMultiplier);
        body.ApplyPossessionVisualScale(PossessionImprintMath.GluttonyScaleMultiplier(gluttonyStacks));
    }

    public float GetCooldownMultiplier(MonsterActor body)
    {
        return body != null && body.isPossessed
            ? PossessionImprintMath.PrideCooldownMultiplier(GetStacks(SinType.Pride)) : 1f;
    }

    public float GetOutgoingDamageMultiplier(MonsterActor body)
    {
        return body != null && body.isPossessed
            ? PossessionImprintMath.WrathDamageMultiplier(GetStacks(SinType.Wrath)) : 1f;
    }

    /// <summary>HP drain multiplier for the currently possessed body.</summary>
    public float GetPossessionDrainMultiplier(MonsterActor body)
    {
        return body != null && body.isPossessed
            ? PossessionImprintMath.SlothDrainMultiplier(GetStacks(SinType.Sloth)) : 1f;
    }

    public float GetBulletTimeDuration(float baseDuration)
    {
        return baseDuration; // Envy 罪印已改为全局移动速度，不再加成子弹时间
    }

    /// <summary>Envy 全局移动速度倍率（附身态生效，每层 +1%，上限 +50%）。</summary>
    public float GetMoveSpeedMultiplier(MonsterActor body)
    {
        return body != null && body.isPossessed
            ? 1f + PossessionImprintMath.EnvyMoveSpeedBonus(GetStacks(SinType.Envy)) : 1f;
    }

    public float GetLustLifestealMultiplier(MonsterActor body)
    {
        return body != null && body.isPossessed
            ? PossessionImprintMath.LustLifestealMultiplier(GetStacks(SinType.Lust)) : 0f;
    }

    /// <summary>
    /// Accumulates fractional Lust healing and applies only the whole HP portion to the
    /// currently possessed body. The remainder survives body switches within the run.
    /// </summary>
    public void ApplyLustLifesteal(MonsterActor body, float actualDamage)
    {
        if (body == null || !body.isPossessed || actualDamage <= 0f) return;
        float multiplier = GetLustLifestealMultiplier(body);
        if (multiplier <= 0f) return;

        LustHealProgress += actualDamage * multiplier;
        int wholeHealing = Mathf.FloorToInt(LustHealProgress);
        if (wholeHealing <= 0) return;

        LustHealProgress -= wholeHealing;
        body.Heal(wholeHealing);
    }

    public bool HasSeenTutorial(SinType sin)
    {
        return shownTutorials.Contains(sin);
    }

    public void MarkTutorialSeen(SinType sin)
    {
        shownTutorials.Add(sin);
    }
}
