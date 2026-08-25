using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Independent, non-possessable Sevenfold Convergence boss actor.</summary>
public sealed class BossSevenfoldActor : Enemy
{
    public const int AbilityCount = 14;
    const float DefaultBossMaxHealth = 7777f;
    public bool IsDefeated { get; private set; }
    public bool CanAct { get; private set; }
    public BossCombatBrain CombatBrain { get; private set; }
    public BossAffixAssimilator AffixAssimilator { get; private set; }
    public Transform teleportAnchor;
    public float baseBossMaxHealth = DefaultBossMaxHealth;
    public float normalDamageMultiplier = 1.65f;
    public int minimumEscortCount = 3;
    public float escortRefreshSeconds = 18f;
    [Header("Boss Combat Scale")]
    [Tooltip("Uses the shared ability-scale pipeline for hitboxes, projectile visuals and summons.")]
    [Min(1f)] public float bossCombatScaleMultiplier = 2f;
    [Header("Void Walk")]
    [Tooltip("After this long outside its pressure band, the Boss begins a telegraphed void walk.")]
    public float teleportFarTriggerDistance = 16f;
    public float teleportEmergencyDistance = 24f;
    public float teleportFarTargetDelay = 1.2f;
    [Min(1f)] public float voidWalkInterval = 5f;
    [Header("Void Walk Landing")]
    [Tooltip("Safe minimum distance from the current player body. This prevents overlap at the arrival point.")]
    [Min(0f)] public float voidWalkMinPlayerDistance = 5f;
    [Tooltip("Preferred landing distance from the player. Default is half of the former 10m target.")]
    [Min(0f)] public float voidWalkPreferredPlayerDistance = 5f;
    [Tooltip("Maximum landing distance from the player. Keep this at or above the preferred distance.")]
    [Min(0f)] public float voidWalkMaxPlayerDistance = 6f;

    Coroutine takeoverRoutine;
    Coroutine teleportRoutine;
    Transform bossAbilitiesRoot;
    float farTargetSince = -1f;
    float nextTeleportTime;
    Collider[] rootColliders;
    bool observingPlayerMobility;
    bool requiresVoidWalkFollowUp;
    bool abilitySequenceLocked;

    public bool HasTeleportAnchor => teleportAnchor != null;
    public bool IsTeleporting { get; private set; }
    public float BossCombatScaleMultiplier => Mathf.Max(1f, bossCombatScaleMultiplier);
    public bool IsAbilitySequenceLocked => abilitySequenceLocked;
    public bool HasVoidWalkFollowUp => requiresVoidWalkFollowUp;
    public int CombatPhase
    {
        get
        {
            float healthFraction = maxHealth > 0f ? currentHealth / maxHealth : 1f;
            return healthFraction > 0.70f ? 1 : (healthFraction > 0.35f ? 2 : 3);
        }
    }

    protected override IController CreateDefaultController()
    {
        return NullController.Instance;
    }

    protected override void Awake()
    {
        sinType = SinType.Gluttony;
        isPossessable = false;
        bodyType = BodyType.Boss;
        baseBossMaxHealth = DefaultBossMaxHealth;
        bossAbilitiesRoot = transform.Find("Abilities");
        base.Awake();
        ApplyPossessionVisualScale(BossCombatScaleMultiplier);
        DisableVisualPartBehaviours();
        if (GetComponent<BossSpatialDistortionController>() == null)
            gameObject.AddComponent<BossSpatialDistortionController>();
        CombatBrain = GetComponent<BossCombatBrain>();
        if (CombatBrain == null) CombatBrain = gameObject.AddComponent<BossCombatBrain>();
        CombatBrain.BuildDefaultProfiles(GetBossAbilities());
        AffixAssimilator = GetComponent<BossAffixAssimilator>();
        if (AffixAssimilator == null) AffixAssimilator = gameObject.AddComponent<BossAffixAssimilator>();
        rootColliders = GetComponents<Collider>();
        CanAct = false;
    }

    protected override void Start()
    {
        base.Start();
        DisableVisualPartBehaviours();
        CombatBrain.BuildDefaultProfiles(GetBossAbilities());
    }

    protected override void OnResetForSpawn()
    {
        if (takeoverRoutine != null) StopCoroutine(takeoverRoutine);
        if (teleportRoutine != null) StopCoroutine(teleportRoutine);
        takeoverRoutine = null;
        teleportRoutine = null;
        IsDefeated = false;
        CanAct = false;
        IsTeleporting = false;
        requiresVoidWalkFollowUp = false;
        abilitySequenceLocked = false;
        StopObservingPlayerMobility();
        farTargetSince = -1f;
        nextTeleportTime = 0f;
        ApplyPossessionVisualScale(BossCombatScaleMultiplier);
        SetRootCollidersEnabled(true);
        BossSpatialDistortionController distortion = GetComponent<BossSpatialDistortionController>();
        if (distortion != null) distortion.ClearRifts();
        DisableVisualPartBehaviours();
    }

    protected override bool ShouldManageAbilityComponent(EnemyAbility ability)
    {
        if (ability == null) return false;
        if (bossAbilitiesRoot == null) bossAbilitiesRoot = transform.Find("Abilities");
        return bossAbilitiesRoot == null || ability.transform.IsChildOf(bossAbilitiesRoot);
    }

    EnemyAbility[] GetBossAbilities()
    {
        if (bossAbilitiesRoot == null) bossAbilitiesRoot = transform.Find("Abilities");
        return bossAbilitiesRoot != null
            ? bossAbilitiesRoot.GetComponentsInChildren<EnemyAbility>(true)
            : GetComponentsInChildren<EnemyAbility>(true);
    }

    /// <summary>
    /// The seven visual source prefabs are art payload only. Their legacy Enemy/AI/combat
    /// components must never register, fade, or fight as independent actors inside the boss.
    /// </summary>
    void DisableVisualPartBehaviours()
    {
        Transform visualRoot = transform.Find("VisualRoot");
        if (visualRoot == null) return;

        foreach (Enemy actor in visualRoot.GetComponentsInChildren<Enemy>(true))
            if (actor != null) actor.enabled = false;
        foreach (AIController controller in visualRoot.GetComponentsInChildren<AIController>(true))
            if (controller != null) controller.enabled = false;
        foreach (CombatAbilityComponent combat in visualRoot.GetComponentsInChildren<CombatAbilityComponent>(true))
            if (combat != null) combat.enabled = false;
        foreach (ActorVisualFx fx in visualRoot.GetComponentsInChildren<ActorVisualFx>(true))
            if (fx != null) fx.enabled = false;
        foreach (EnemyAbility ability in visualRoot.GetComponentsInChildren<EnemyAbility>(true))
            if (ability != null) ability.enabled = false;
        foreach (Collider collider in visualRoot.GetComponentsInChildren<Collider>(true))
            if (collider != null) collider.enabled = false;
    }

    public bool HasAllFourteenAbilities
    {
        get { return CombatBrain != null && CombatBrain.profiles.Count == AbilityCount; }
    }

    public void BeginTakeover()
    {
        isPossessable = false;
        CanAct = false;
        DisableVisualPartBehaviours();
        currentHealth = baseBossMaxHealth;
        maxHealth = baseBossMaxHealth;
        ApplyPossessionVisualScale(BossCombatScaleMultiplier);
        spawnDamageMultiplier *= normalDamageMultiplier;
        DisableMovementAbilities();
        CombatBrain.BuildDefaultProfiles(GetBossAbilities());
        StartObservingPlayerMobility();
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
            if (candidate != null && candidate != this && candidate != currentBody
                && !candidate.transform.IsChildOf(transform)) victims.Add(candidate);
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
        if (IsDefeated) return;
        nextTeleportTime = Time.unscaledTime + voidWalkInterval;
        CanAct = true;
        BossHealthBarUI.ShowFor(this);
        Debug.Log($"[BossDamage] Combat enabled: hp={currentHealth:F1}/{maxHealth:F1}, phase={CombatPhase}, rootCollidersEnabled={AreRootCollidersEnabled()}", this);
        BossBattleAnnouncementUI.ShowBossBattleStart();
        TrySummonInitialDrone();
    }

    void TrySummonInitialDrone()
    {
        EnemyAbility[] abilities = GetBossAbilities();
        for (int i = 0; i < abilities.Length; i++)
        {
            EnemyAbility_SlothDrone drone = abilities[i] as EnemyAbility_SlothDrone;
            if (drone != null && drone.CanTrigger())
            {
                drone.Trigger();
                return;
            }
        }
    }

    void StartObservingPlayerMobility()
    {
        if (observingPlayerMobility) return;
        EnemyAbility.OnAnyTriggered += HandlePlayerAbilityTriggered;
        observingPlayerMobility = true;
    }

    void StopObservingPlayerMobility()
    {
        if (!observingPlayerMobility) return;
        EnemyAbility.OnAnyTriggered -= HandlePlayerAbilityTriggered;
        observingPlayerMobility = false;
    }

    void HandlePlayerAbilityTriggered(EnemyAbility ability)
    {
        if (ability == null || ability.type != EnemyAbility.AbilityType.Mobility || !ability.IsOwnedByPlayer) return;
        MonsterActor currentBody = PossessionManager.Instance != null ? PossessionManager.Instance.CurrentBody : null;
        if (currentBody == null || ability.OwnerMonster != currentBody) return;

        EnemyAbility[] abilities = GetBossAbilities();
        for (int i = 0; i < abilities.Length; i++)
        {
            EnemyAbility_PrideBlinkChain blink = abilities[i] as EnemyAbility_PrideBlinkChain;
            if (blink != null) blink.InterruptBossBlink();
        }
    }

    void DisableMovementAbilities()
    {
        EnemyAbility[] abilities = GetBossAbilities();
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
        if (teleportRoutine != null || IsDefeated || Time.unscaledTime < nextTeleportTime) return false;
        BossTeleportPlanner planner = GetComponent<BossTeleportPlanner>();
        if (planner == null) planner = gameObject.AddComponent<BossTeleportPlanner>();
        planner.minPlayerDistance = Mathf.Max(0f, voidWalkMinPlayerDistance);
        planner.preferredDistance = Mathf.Max(planner.minPlayerDistance, voidWalkPreferredPlayerDistance);
        planner.maxPlayerDistance = Mathf.Max(planner.preferredDistance, voidWalkMaxPlayerDistance);
        if (!planner.TryPlanAroundTarget(this, targetPosition, out Vector3 destination)) return false;
        teleportRoutine = StartCoroutine(TeleportRoutine(destination));
        return true;
    }

    /// <summary>
    /// Keeps the Boss inside a threatening range without turning every distant cast into
    /// an unreadable blink. Extreme separation and repeated decision failures recover
    /// immediately; ordinary range pressure waits through a short telegraphable grace.
    /// </summary>
    public bool TryRequestTacticalTeleport(Vector3 targetPosition, float distance,
        bool hasRangedOption, int failedDecisions)
    {
        if (requiresVoidWalkFollowUp) return false;
        bool farAway = distance >= teleportFarTriggerDistance;
        if (!farAway) farTargetSince = -1f;
        else if (farTargetSince < 0f) farTargetSince = Time.unscaledTime;

        // Void walk is a readable five-second pressure beat. Distance remains a
        // separate recovery trigger when the player disengages too far.
        if (Time.unscaledTime >= nextTeleportTime)
            return TryTeleportTowardsTarget(targetPosition);

        if (!farAway) return false;
        bool emergency = distance >= teleportEmergencyDistance || failedDecisions >= 3;
        bool pressureGap = Time.unscaledTime - farTargetSince >= teleportFarTargetDelay;
        if (!emergency && (!pressureGap || !hasRangedOption)) return false;
        return TryTeleportTowardsTarget(targetPosition);
    }

    public void SetAbilitySequenceLocked(bool locked)
    {
        abilitySequenceLocked = locked;
    }

    public bool TryGetVoidWalkFollowUp(Vector3 targetPosition, out EnemyAbility ability)
    {
        ability = null;
        if (!requiresVoidWalkFollowUp) return false;

        EnemyAbility_SwordQi swordQi = null;
        EnemyAbility_GluttonyDevour devour = null;
        EnemyAbility[] abilities = GetBossAbilities();
        for (int i = 0; i < abilities.Length; i++)
        {
            if (swordQi == null) swordQi = abilities[i] as EnemyAbility_SwordQi;
            if (devour == null) devour = abilities[i] as EnemyAbility_GluttonyDevour;
        }

        Vector3 delta = targetPosition - transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;
        bool canDevour = devour != null && devour.CanTrigger()
            && distance <= devour.range * BossCombatScaleMultiplier;
        bool canSwordQi = swordQi != null && swordQi.CanTrigger()
            && distance <= swordQi.maxRange * BossCombatScaleMultiplier;

        if (canDevour && (!canSwordQi || AiRandomValue() < 0.45f)) ability = devour;
        else if (canSwordQi) ability = swordQi;
        return ability != null;
    }

    public void CompleteVoidWalkFollowUp(EnemyAbility ability)
    {
        if (ability is EnemyAbility_SwordQi || ability is EnemyAbility_GluttonyDevour)
            requiresVoidWalkFollowUp = false;
    }

    public override void TakeDamage(float amount)
    {
        TakeDamage(amount, allowGreedGuardAbsorb: true);
    }

    public override void TakeDamage(float amount, bool allowGreedGuardAbsorb)
    {
        if (IsTeleporting)
        {
            Debug.LogWarning($"[BossDamage] Blocked before base settlement: reason=Teleporting, incoming={amount:F2}, hp={currentHealth:F1}/{maxHealth:F1}, phase={CombatPhase}, source={DescribeLastDamageSource()}", this);
            return;
        }

        Debug.Log($"[BossDamage] Enter settlement: incoming={amount:F2}, hp={currentHealth:F1}/{maxHealth:F1}, phase={CombatPhase}, allowGuard={allowGreedGuardAbsorb}, source={DescribeLastDamageSource()}, canAct={CanAct}, rootCollidersEnabled={AreRootCollidersEnabled()}", this);
        base.TakeDamage(amount, allowGreedGuardAbsorb);
    }

    IEnumerator TeleportRoutine(Vector3 destination)
    {
        CanAct = false;
        IsTeleporting = true;
        SetRootCollidersEnabled(false);
        LustBodyState lust = GetComponent<LustBodyState>();
        if (lust == null) lust = gameObject.AddComponent<LustBodyState>();
        LustAnchorMarker marker = lust.PlaceOrReplaceAnchor(transform.position, transform.rotation, 6f);
        teleportAnchor = marker != null ? marker.transform : null;
        BossSpatialDistortionController distortion = GetComponent<BossSpatialDistortionController>();
        if (distortion != null)
            yield return distortion.PlayTeleport(destination, RestoreTeleportHitbox);
        else
        {
            transform.position = destination;
            RestoreTeleportHitbox();
            yield return new WaitForSecondsRealtime(0.22f);
        }
        IsTeleporting = false;
        requiresVoidWalkFollowUp = true;
        nextTeleportTime = Time.unscaledTime + voidWalkInterval;
        farTargetSince = -1f;
        CanAct = !IsDefeated;
        teleportRoutine = null;
    }

    void RestoreTeleportHitbox()
    {
        if (IsDefeated) return;
        MonsterPool.SnapCapsuleBottomToGround(gameObject);
        SetRootCollidersEnabled(true);
    }

    void SetRootCollidersEnabled(bool enabled)
    {
        if (rootColliders == null) rootColliders = GetComponents<Collider>();
        for (int i = 0; i < rootColliders.Length; i++)
            if (rootColliders[i] != null) rootColliders[i].enabled = enabled;
        Debug.Log($"[BossDamage] Root colliders set enabled={enabled}, active={AreRootCollidersEnabled()}, teleporting={IsTeleporting}", this);
    }

    bool AreRootCollidersEnabled()
    {
        if (rootColliders == null) rootColliders = GetComponents<Collider>();
        for (int i = 0; i < rootColliders.Length; i++)
            if (rootColliders[i] != null && rootColliders[i].enabled) return true;
        return false;
    }

    string DescribeLastDamageSource()
    {
        return lastDamageSource != null
            ? $"{lastDamageSource.name}({lastDamageSource.GetType().Name})"
            : "<none>";
    }

    protected override void Die()
    {
        if (IsDefeated) return;
        IsDefeated = true;
        BossHealthBarUI.HideFor(this);
        CanAct = false;
        CancelInvoke();
        if (takeoverRoutine != null) StopCoroutine(takeoverRoutine);
        if (teleportRoutine != null) StopCoroutine(teleportRoutine);
        IsTeleporting = false;
        requiresVoidWalkFollowUp = false;
        abilitySequenceLocked = false;
        BossSpatialDistortionController distortion = GetComponent<BossSpatialDistortionController>();
        if (distortion != null) distortion.ClearRifts();
        if (PossessionManager.Instance != null) PossessionManager.Instance.SetBossBattleSwitchMode(false);
        StopObservingPlayerMobility();
        if (RunSpawnDirector.Instance != null)
        {
            RunSpawnDirector.Instance.MarkBossDefeated();
            RunSpawnDirector.Instance.ClearBossBattleReserveBodies();
        }
        base.BeginDisappearing();
        if (RunSession.Instance != null && RunSession.Instance.CurrentPhase == RunPhase.Final)
            RunSession.Instance.TransitionTo(RunPhase.Result);
    }

    void OnDestroy()
    {
        StopObservingPlayerMobility();
    }
}
