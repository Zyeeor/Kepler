using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;


/// <summary>
/// Greed Attack: orbiting hand inventory. Regen 1/sec, dump all on LMB (0.2s spacing, 20 HP once).
/// Cards: GR-A01 cap, GR-A02 retarget, GR-A03 spawn-on-kill, GR-A07 flank arc.
/// </summary>
public class EnemyAbility_GreedHands : EnemyAbility
{
    [Header("Hand Inventory")]
    [FormerlySerializedAs("baseInventoryMax")]
    public int maxDaggers = 6;

    public int cardInventoryMax = 10;
    public int initialPossessedHands = 2;
    public float regenInterval = 1f;

    [Header("Dagger")]
    [FormerlySerializedAs("handProjectilePrefab")]
    public GameObject daggerPrefab;
    public Transform orbitCircleCenterOffset;

    public GameObject handSpawnVfxPrefab;

    public float orbitRadius = 1.5f;
    public float orbitSpeed = 200f;
    [FormerlySerializedAs("orbitHeight")]
    public float heightOffset;

    [Header("Homing")]
    public float detectRange = 8f;
    [FormerlySerializedAs("releaseInterval")]
    public float launchInterval = 0.3f;
    public float aimTurnSpeed = 720f;
    public float homingSpeed = 20f;
    public float homingTurnRate = 720f;
    [Range(0f, 1f)] public float homingCurveStrength = 0.757f;

    [Header("Damage")]
    [HideInInspector] public float handDamage = 15f;

    public float damageMultiplier = 1f;

    [Header("Impact VFX")]
    [FormerlySerializedAs("handHitVfxPrefab")]
    public GameObject impactVfxPrefab;
    public float impactVfxDuration = 1f;

    [Header("Animation")]
    public string animTrigger = "Basic";

    [Header("Debug")]

    [Tooltip("Log inventory / orbit visual lifecycle for troubleshooting.")]
    public bool debugLog = true;

    public int CurrentHands { get; private set; }
    public int InventoryMax => IsUpgradeUnlocked("GR-A01") ? Mathf.Max(maxDaggers, cardInventoryMax) : maxDaggers;


    private float _regenTimer;
    private bool _wasPossessed;
    private bool _dumping;
    private readonly List<GameObject> _orbitVisuals = new List<GameObject>();
    private EnemyAbility_GreedGuard _guard;

    private void OnEnable()
    {
        type = AbilityType.BasicAttack;
        abilityName = "念力魔手";
        cooldown = 0f;
        if (damage <= 0f) damage = handDamage;
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Greed.Hands", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Greed.Hands");
    }

    private void Start()
    {
        _wasPossessed = owner != null && owner.isPossessed;
        _guard = owner != null ? owner.GetComponentInChildren<EnemyAbility_GreedGuard>(true) : null;
        if (_wasPossessed)
            CurrentHands = Mathf.Clamp(initialPossessedHands, 0, InventoryMax);
        RefreshOrbitVisuals();
        if (debugLog)
            Debug.Log($"[GreedHands] Start on '{(owner != null ? owner.name : "<no owner>")}' (root '{transform.root.name}'): possessed={_wasPossessed}, hands={CurrentHands}/{InventoryMax}, daggerPrefab={(daggerPrefab != null ? daggerPrefab.name : "NULL -> sphere fallback")}", this);

    }

    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;
        if (CurrentHands <= 0) return false;
        if (_dumping) return false;
        if (_guard == null && owner != null)
            _guard = owner.GetComponentInChildren<EnemyAbility_GreedGuard>(true);
        if (_guard != null && _guard.IsGuarding) return false;
        return true;
    }

    protected override void Update()
    {
        base.Update();
        if (owner == null) return;

        if (_wasPossessed != owner.isPossessed)
        {
            _wasPossessed = owner.isPossessed;
            if (_wasPossessed)
                CurrentHands = Mathf.Clamp(initialPossessedHands, 0, InventoryMax);
            RefreshOrbitVisuals();
        }

        if (!_dumping && CurrentHands < InventoryMax)
        {
            _regenTimer += AbilityDeltaTime;
            if (_regenTimer >= regenInterval)
            {
                _regenTimer = 0f;
                CurrentHands++;
                if (debugLog)
                    Debug.Log($"[GreedHands] '{owner.name}' regen -> hands={CurrentHands}/{InventoryMax}", this);
                RefreshOrbitVisuals();
            }
        }

        UpdateOrbitPositions();
    }

    protected override void OnTrigger()
    {
        if (owner == null || CurrentHands <= 0)
        {
            EndActivationEffect();
            return;
        }

        foreach (Animator animator in owner.GetComponentsInChildren<Animator>(false))
            animator.SetTrigger(animTrigger);
        StartCoroutine(DumpRoutine());

    }

    private IEnumerator DumpRoutine()
    {
        _dumping = true;
        int toRelease = CurrentHands;
        CurrentHands = 0;
        RefreshOrbitVisuals();
        _regenTimer = 0f;
        if (debugLog)
            Debug.Log($"[GreedHands] '{(owner != null ? owner.name : "?")}' dump {toRelease} hand(s)", this);

        bool flank = IsUpgradeUnlocked("GR-A07");
        bool retarget = IsUpgradeUnlocked("GR-A02");
        bool spawnOnKill = IsUpgradeUnlocked("GR-A03");

        for (int i = 0; i < toRelease; i++)
        {
            if (owner == null) break;
            if (_guard != null && _guard.IsGuarding) break;

            Enemy target = FindNearestLegalTarget(owner.transform.position, null);
            if (target == null)
            {
                // No legal target: this hand disappears (consumed from inventory already).
            }
            else
            {
                bool left = (i % 2) == 0;
                FireHand(GetOrbitCenterPosition(), target, retarget, spawnOnKill, flank, left, derived: false);

            }

            if (i < toRelease - 1)
                yield return AbilityWait(launchInterval);

        }

        _dumping = false;
        EndActivationEffect();
    }

    public void AddHands(int count)
    {
        if (count <= 0) return;
        CurrentHands = Mathf.Min(InventoryMax, CurrentHands + count);
        RefreshOrbitVisuals();
    }

    public Enemy FindNearestLegalTarget(Vector3 from, Enemy ignore)
    {
        if (owner == null) return null;
        Enemy best = null;
        float bestSqr = detectRange * detectRange;
        CombatHitboxDebug.DrawSphere(drawHitboxes, from, detectRange, 0f);
        Collider[] hits = Physics.OverlapSphere(from, detectRange, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            Enemy enemy = hits[i] != null ? hits[i].GetComponentInParent<Enemy>() : null;
            if (enemy == null || enemy == ignore || !owner.CanDamage(enemy)) continue;
            float sqr = (enemy.transform.position - from).sqrMagnitude;
            if (sqr >= bestSqr) continue;
            bestSqr = sqr;
            best = enemy;
        }
        return best;
    }

    public void SettleHandHit(Enemy target, float amount)
    {
        if (target == null) return;
        SettleHit(target, amount > 0f ? amount : damage);
    }

    public void SpawnDerivedHandsFromKill(Vector3 origin, GreedHandProjectile parent)
    {
        if (parent == null || parent.isDerived) return;
        int count = owner != null
            ? owner.AiRandomInt(Mathf.Max(1, parent.spawnOnKillMin), Mathf.Max(parent.spawnOnKillMin, parent.spawnOnKillMax) + 1)
            : Random.Range(Mathf.Max(1, parent.spawnOnKillMin), Mathf.Max(parent.spawnOnKillMin, parent.spawnOnKillMax) + 1);
        bool flank = IsUpgradeUnlocked("GR-A07");
        for (int i = 0; i < count; i++)
        {
            Enemy target = FindNearestLegalTarget(origin, null);
            if (target == null) continue;
            FireHand(origin + Vector3.up * 0.4f, target, retarget: false, spawnOnKill: false, flank, left: (i % 2) == 0, derived: true);
        }
    }

    private void FireHand(Vector3 origin, Enemy target, bool retarget, bool spawnOnKill, bool flank, bool left, bool derived)
    {
        GameObject go;
        if (daggerPrefab != null)
            go = VfxPool.Instance.Spawn(daggerPrefab, origin, Quaternion.identity);

        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "GreedHandProjectile";
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * 0.35f;
            Collider col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            Object.Destroy(go.GetComponent<Collider>());
            go.AddComponent<GreedHandProjectile>();
        }

        if (handSpawnVfxPrefab != null)
        {
            GameObject spawnVfx = VfxPool.Instance.Spawn(handSpawnVfxPrefab, origin, Quaternion.identity);
            PlayVfx(spawnVfx);
            ReleaseVfx(spawnVfx, 1f);
        }

        GreedHandProjectile hand = go.GetComponent<GreedHandProjectile>();
        if (hand == null) hand = go.AddComponent<GreedHandProjectile>();
        hand.Launch(
            this,
            owner,
            target,
            (damage > 0f ? damage : handDamage) * damageMultiplier,
            retarget,
            spawnOnKill,
            flank,
            left,
            derived,
            impactVfxPrefab,
            impactVfxDuration,
            homingSpeed,
            homingTurnRate,
            homingCurveStrength);

        if (debugLog)
            Debug.Log($"[GreedHands] '{(owner != null ? owner.name : "?")}' fired hand -> '{(target != null ? target.name : "<none>")}' (flank={flank}, left={left}, derived={derived})", go);
    }

    private void RefreshOrbitVisuals()
    {
        while (_orbitVisuals.Count > CurrentHands)
        {
            int last = _orbitVisuals.Count - 1;
            if (_orbitVisuals[last] != null) Destroy(_orbitVisuals[last]);
            _orbitVisuals.RemoveAt(last);
        }

        while (_orbitVisuals.Count < CurrentHands)
        {
            GameObject vis;
            if (daggerPrefab != null)
                vis = Instantiate(daggerPrefab);

            else
            {
                vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                vis.transform.localScale = Vector3.one * 0.25f;
                Object.Destroy(vis.GetComponent<Collider>());
            }
            // Orbit visuals must not fly as projectiles.
            GreedHandProjectile projectile = vis.GetComponent<GreedHandProjectile>();
            if (projectile != null) Destroy(projectile);
            vis.name = "GreedHandOrbit";
            _orbitVisuals.Add(vis);
            if (debugLog)
                Debug.Log($"[GreedHands] '{(owner != null ? owner.name : "?")}' orbit visual created ({_orbitVisuals.Count}/{CurrentHands}) from '{(daggerPrefab != null ? daggerPrefab.name : "sphere fallback")}'", vis);

        }
    }

    private void UpdateOrbitPositions()
    {
        if (owner == null) return;
        for (int i = 0; i < _orbitVisuals.Count; i++)
        {
            GameObject vis = _orbitVisuals[i];
            if (vis == null) continue;
            float angle = AbilityTime * orbitSpeed + i * (360f / Mathf.Max(1, _orbitVisuals.Count));
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * orbitRadius;
            vis.transform.position = GetOrbitCenterPosition() + offset;
        }
    }

    private Vector3 GetOrbitCenterPosition()
    {
        if (orbitCircleCenterOffset != null) return orbitCircleCenterOffset.position;
        return owner != null ? owner.transform.position + Vector3.up * heightOffset : transform.position;
    }

    protected override void OnDisable()

    {
        for (int i = 0; i < _orbitVisuals.Count; i++)
            if (_orbitVisuals[i] != null) Destroy(_orbitVisuals[i]);
        _orbitVisuals.Clear();
        base.OnDisable();
    }

    public override void ResetForOwnerReuse()
    {
        _dumping = false;
        _wasPossessed = false;
        CurrentHands = 0;
        _regenTimer = 0f;
        base.ResetForOwnerReuse();
    }
}
