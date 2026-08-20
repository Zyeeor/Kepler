using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Greed Attack: orbiting hand inventory. Regen 1/sec, dump all on LMB (0.2s spacing, 20 HP once).
/// Cards: GR-A01 cap, GR-A02 retarget, GR-A03 spawn-on-kill, GR-A07 flank arc.
/// </summary>
public class EnemyAbility_GreedHands : EnemyAbility
{
    public int baseInventoryMax = 6;
    public int cardInventoryMax = 10;
    public int initialPossessedHands = 2;
    public float regenInterval = 1f;
    public float releaseInterval = 0.2f;
    public float detectRange = 10f;
    public float handDamage = 15f;
    public GameObject handProjectilePrefab;
    public GameObject handSpawnVfxPrefab;
    public GameObject handHitVfxPrefab;
    public float orbitRadius = 1.4f;
    public float orbitHeight = 1.1f;
    [Header("Debug")]
    [Tooltip("Log inventory / orbit visual lifecycle for troubleshooting.")]
    public bool debugLog = true;

    public int CurrentHands { get; private set; }
    public int InventoryMax => IsUpgradeUnlocked("GR-A01") ? Mathf.Max(baseInventoryMax, cardInventoryMax) : baseInventoryMax;

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
            Debug.Log($"[GreedHands] Start on '{(owner != null ? owner.name : "<no owner>")}' (root '{transform.root.name}'): possessed={_wasPossessed}, hands={CurrentHands}/{InventoryMax}, handProjectilePrefab={(handProjectilePrefab != null ? handProjectilePrefab.name : "NULL -> sphere fallback")}", this);
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
                FireHand(owner.transform.position + Vector3.up * orbitHeight, target, retarget, spawnOnKill, flank, left, derived: false);
            }

            if (i < toRelease - 1)
                yield return AbilityWait(releaseInterval);
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
        if (handProjectilePrefab != null)
            go = VfxPool.Instance.Spawn(handProjectilePrefab, origin, Quaternion.identity);
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
        hand.Launch(this, owner, target, damage > 0f ? damage : handDamage, retarget, spawnOnKill, flank, left, derived, handHitVfxPrefab);
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
            if (handProjectilePrefab != null)
                vis = Instantiate(handProjectilePrefab);
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
                Debug.Log($"[GreedHands] '{(owner != null ? owner.name : "?")}' orbit visual created ({_orbitVisuals.Count}/{CurrentHands}) from '{(handProjectilePrefab != null ? handProjectilePrefab.name : "sphere fallback")}'", vis);
        }
    }

    private void UpdateOrbitPositions()
    {
        if (owner == null) return;
        for (int i = 0; i < _orbitVisuals.Count; i++)
        {
            GameObject vis = _orbitVisuals[i];
            if (vis == null) continue;
            float angle = AbilityTime * 90f + i * (360f / Mathf.Max(1, _orbitVisuals.Count));
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * orbitRadius;
            vis.transform.position = owner.transform.position + Vector3.up * orbitHeight + offset;
        }
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
