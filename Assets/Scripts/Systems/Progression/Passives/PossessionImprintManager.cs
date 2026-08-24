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
    readonly HashSet<long> consumedTransactions = new HashSet<long>();
    readonly HashSet<SinType> shownTutorials = new HashSet<SinType>();
    PossessionManager attachedPossessionManager;
    bool hasRun;
    bool restoredRun;

    public float GreedBonusProgress { get; private set; }
    public int GetStacks(SinType sin) => (int)sin > 0 && (int)sin < stacks.Length
        ? Mathf.Clamp(stacks[(int)sin], 0, Mathf.Max(1, PossessionImprintMath.MaxStacks))
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
        Array.Clear(stacks, 0, stacks.Length);
        consumedTransactions.Clear();
        shownTutorials.Clear();
        GreedBonusProgress = 0f;
        hasRun = true;
        restoredRun = false;
    }

    public void EndRun()
    {
        BeginNewRun();
        hasRun = false;
    }

    public void LoadFromSave(List<PossessionImprintState> saved, float greedProgress)
    {
        Array.Clear(stacks, 0, stacks.Length);
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
        GreedBonusProgress = Mathf.Clamp01(greedProgress);
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
        if (body == null || !IsRealPossession(reason)) return;
        body.ResolveSinIdentityFromHint(body.name + " " + body.displayName);
        if (body.sinType == SinType.None)
        {
            Debug.LogWarning("[PossessionImprint] 无法解析附身体罪印身份：" + body.name);
            return;
        }
        if (!consumedTransactions.Add(transactionId)) return;
        if (!hasRun) BeginNewRun();
        float greedProgress = GreedBonusProgress;
        PossessionImprintMath.ApplyTransaction(stacks, ref greedProgress, body.sinType);
        GreedBonusProgress = greedProgress;
        ApplyBodyEffects(body);
        OnImprintChanged?.Invoke(body.sinType, stacks[(int)body.sinType]);
    }

    static bool IsRealPossession(PossessionGrantReason reason)
    {
        return reason == PossessionGrantReason.InitialAssignment
            || reason == PossessionGrantReason.PlayerPossession
            || reason == PossessionGrantReason.DeathRelay;
    }

    public event Action<SinType, int> OnImprintChanged;

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
        return baseDuration + PossessionImprintMath.EnvyBulletTimeBonus(GetStacks(SinType.Envy));
    }

    public float GetLustControlChance()
    {
        return PossessionImprintMath.LustControlChance(GetStacks(SinType.Lust));
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
