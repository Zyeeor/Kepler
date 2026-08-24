using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Independent, non-possessable Sevenfold Convergence boss actor.</summary>
public sealed class BossSevenfoldActor : Enemy
{
    public const int AbilityCount = 14;
    public bool IsDefeated { get; private set; }
    public bool CanAct { get; private set; }
    public BossCombatBrain CombatBrain { get; private set; }
    public BossAffixAssimilator AffixAssimilator { get; private set; }
    public Transform teleportAnchor;
    public float baseBossMaxHealth = 8000f;
    public float normalDamageMultiplier = 1.65f;
    public int minimumEscortCount = 3;
    public float escortRefreshSeconds = 18f;

    Coroutine takeoverRoutine;
    Coroutine teleportRoutine;

    public bool HasTeleportAnchor => teleportAnchor != null;

    protected override IController CreateDefaultController()
    {
        return NullController.Instance;
    }

    protected override void Awake()
    {
        sinType = SinType.Gluttony;
        isPossessable = false;
        bodyType = BodyType.Boss;
        base.Awake();
        CombatBrain = GetComponent<BossCombatBrain>();
        if (CombatBrain == null) CombatBrain = gameObject.AddComponent<BossCombatBrain>();
        CombatBrain.BuildDefaultProfiles(GetComponentsInChildren<EnemyAbility>(true));
        AffixAssimilator = GetComponent<BossAffixAssimilator>();
        if (AffixAssimilator == null) AffixAssimilator = gameObject.AddComponent<BossAffixAssimilator>();
        CanAct = false;
    }

    protected override void Start()
    {
        base.Start();
        CombatBrain.BuildDefaultProfiles(GetComponentsInChildren<EnemyAbility>(true));
    }

    public bool HasAllFourteenAbilities
    {
        get { return CombatBrain != null && CombatBrain.profiles.Count == AbilityCount; }
    }

    public void BeginTakeover()
    {
        isPossessable = false;
        CanAct = false;
        currentHealth = Mathf.Max(baseBossMaxHealth, currentHealth);
        maxHealth = currentHealth;
        spawnDamageMultiplier *= normalDamageMultiplier;
        DisableMovementAbilities();
        CombatBrain.BuildDefaultProfiles(GetComponentsInChildren<EnemyAbility>(true));
        if (takeoverRoutine != null) StopCoroutine(takeoverRoutine);
        takeoverRoutine = StartCoroutine(TakeoverRoutine());
    }

    IEnumerator TakeoverRoutine()
    {
        MonsterActor currentBody = PossessionManager.Instance != null ? PossessionManager.Instance.CurrentBody : null;
        var victims = new List<Enemy>(EnemyRegistry.Count);
        IReadOnlyList<Enemy> all = EnemyRegistry.All;
        for (int i = 0; i < all.Count; i++)
        {
            Enemy candidate = all[i];
            if (candidate != null && candidate != this && candidate != currentBody) victims.Add(candidate);
        }
        for (int i = 0; i < victims.Count; i++)
            if (victims[i] != null) victims[i].BeginDisappearing();

        PossessionManager.Instance?.SetBossBattleSwitchMode(true);
        RunSpawnDirector.Instance?.SpawnBossBattleReserveBodies();

        yield return new WaitForSecondsRealtime(2.2f);
        EnableBossCombat();
        yield return new WaitForSecondsRealtime(0.8f);
        RunSpawnDirector.Instance?.SpawnBossMinions(minimumEscortCount);

        while (!IsDefeated)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(1f, escortRefreshSeconds));
            int activeEscorts = 0;
            all = EnemyRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                Enemy candidate = all[i];
                if (candidate != null && candidate != this && candidate != currentBody && !candidate.isDowned)
                    activeEscorts++;
            }
            if (activeEscorts < minimumEscortCount)
                RunSpawnDirector.Instance?.SpawnBossMinions(minimumEscortCount - activeEscorts);
        }
    }

    void EnableBossCombat()
    {
        if (!IsDefeated) CanAct = true;
    }

    void DisableMovementAbilities()
    {
        EnemyAbility[] abilities = GetComponentsInChildren<EnemyAbility>(true);
        for (int i = 0; i < abilities.Length; i++)
            if (abilities[i] != null && abilities[i].type == EnemyAbility.AbilityType.Mobility)
                abilities[i].enabled = false;
    }

    public override void BeginDisappearing()
    {
        if (!IsDefeated)
        {
            Debug.LogWarning("[BossSevenfold] 忽略非死亡路径的 Boss 消散请求：" + name, this);
            return;
        }
        base.BeginDisappearing();
    }

    public Vector3 GetBossTargetPosition()
    {
        if (PossessionManager.Instance != null && PossessionManager.Instance.CurrentBody != null)
            return PossessionManager.Instance.CurrentBody.transform.position;
        PlayerHealth health = PlayerHealth.Instance;
        return health != null ? health.transform.position : transform.position + transform.forward * 8f;
    }

    public void FaceBossTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    public bool TargetHasEnvyMark()
    {
        MonsterActor target = PossessionManager.Instance != null ? PossessionManager.Instance.CurrentBody : null;
        return target != null && target.GetComponent<EnvyMarkTarget>() != null && target.GetComponent<EnvyMarkTarget>().IsActive;
    }

    public bool TryTeleportTowardsTarget(Vector3 targetPosition)
    {
        if (teleportRoutine != null) return false;
        BossTeleportPlanner planner = GetComponent<BossTeleportPlanner>();
        if (planner == null) planner = gameObject.AddComponent<BossTeleportPlanner>();
        if (!planner.TryPlanAroundTarget(this, targetPosition, out Vector3 destination)) return false;
        teleportRoutine = StartCoroutine(TeleportRoutine(destination));
        return true;
    }

    IEnumerator TeleportRoutine(Vector3 destination)
    {
        CanAct = false;
        LustBodyState lust = GetComponent<LustBodyState>();
        if (lust == null) lust = gameObject.AddComponent<LustBodyState>();
        LustAnchorMarker marker = lust.PlaceOrReplaceAnchor(transform.position, transform.rotation);
        teleportAnchor = marker != null ? marker.transform : null;
        BossSpatialDistortionController distortion = GetComponent<BossSpatialDistortionController>();
        if (distortion != null)
            yield return distortion.PlayTeleport(destination);
        else
        {
            transform.position = destination;
            yield return null;
        }
        CanAct = !IsDefeated;
        teleportRoutine = null;
    }

    protected override void Die()
    {
        if (IsDefeated) return;
        IsDefeated = true;
        CanAct = false;
        CancelInvoke();
        if (takeoverRoutine != null) StopCoroutine(takeoverRoutine);
        if (teleportRoutine != null) StopCoroutine(teleportRoutine);
        if (PossessionManager.Instance != null) PossessionManager.Instance.SetBossBattleSwitchMode(false);
        if (RunSpawnDirector.Instance != null)
        {
            RunSpawnDirector.Instance.MarkBossDefeated();
            RunSpawnDirector.Instance.ClearBossBattleReserveBodies();
        }
        base.BeginDisappearing();
        if (RunSession.Instance != null && RunSession.Instance.CurrentPhase == RunPhase.Final)
            RunSession.Instance.TransitionTo(RunPhase.Result);
    }
}
