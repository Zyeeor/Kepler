using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Short-lived Greed black-oil patch. Base: player / Possessed Greed speed boost.
/// GR-M01: Enemy slow. Burning oil: neither boost nor slow. Shared BurningOil hook for Wrath.
/// </summary>
[DisallowMultipleComponent]
public class GreedBlackOilZone : MonoBehaviour
{
    public const string BurningOilStateTag = "State.Combat.BurningOil";

    public Enemy owner;
    public float lifetime = 4f;
    public float width = 1.5f;
    public float allySpeedMultiplier = 1.5f;
    public float enemySlowMultiplier = 0.5f;
    public bool applyEnemySlow;
    public bool isBurning;
    public GameObject normalVfxPrefab;
    public GameObject burningVfxPrefab;

    private readonly Dictionary<int, CombatAbilityComponent> _occupants = new Dictionary<int, CombatAbilityComponent>();
    private readonly HashSet<int> _frameOccupants = new HashSet<int>();
    private Collider[] _overlapBuffer;
    private float _expiresAt;
    private GameObject _vfxInstance;
    private BoxCollider _volume;

    public bool IsNormalOil => !isBurning;
    public bool IsOwnedBy(Enemy actor) => owner != null && actor != null && owner == actor;

    public void Initialize(
        Enemy oilOwner,
        float life,
        float oilWidth,
        float allyMult,
        float enemyMult,
        bool enemySlow,
        GameObject normalVfx,
        GameObject burningVfx)
    {
        owner = oilOwner;
        lifetime = Mathf.Max(0.1f, life);
        width = Mathf.Max(0.2f, oilWidth);
        allySpeedMultiplier = Mathf.Max(0.01f, allyMult);
        enemySlowMultiplier = Mathf.Clamp(enemyMult, 0.01f, 1f);
        applyEnemySlow = enemySlow;
        normalVfxPrefab = normalVfx;
        burningVfxPrefab = burningVfx;
        _expiresAt = Time.time + lifetime;
        EnsureVolume();
        RefreshVisual();
    }

    public void Ignite()
    {
        if (isBurning) return;
        isBurning = true;
        ClearAllOccupantModifiers();
        RefreshVisual();
    }

    private void Awake()
    {
        _overlapBuffer = new Collider[32];
        EnsureVolume();
    }

    private void OnDisable()
    {
        ClearAllOccupantModifiers();
    }

    private void Update()
    {
        if (Time.time >= _expiresAt)
        {
            Destroy(gameObject);
            return;
        }

        ScanOccupants();
    }

    private void EnsureVolume()
    {
        _volume = GetComponent<BoxCollider>();
        if (_volume == null) _volume = gameObject.AddComponent<BoxCollider>();
        _volume.isTrigger = true;
        _volume.center = new Vector3(0f, 0.5f, 0f);
        _volume.size = new Vector3(width, 1.5f, width);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeAll;
    }

    private void RefreshVisual()
    {
        if (_vfxInstance != null)
        {
            Destroy(_vfxInstance);
            _vfxInstance = null;
        }

        GameObject prefab = isBurning ? burningVfxPrefab : normalVfxPrefab;
        if (prefab == null) return;
        _vfxInstance = Instantiate(prefab, transform.position, Quaternion.identity, transform);
    }

    private void ScanOccupants()
    {
        Vector3 half = new Vector3(width * 0.5f, 0.75f, width * 0.5f);
        int count = Physics.OverlapBoxNonAlloc(
            transform.position + Vector3.up * 0.5f,
            half,
            _overlapBuffer,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Ignore);

        _frameOccupants.Clear();
        for (int i = 0; i < count; i++)
        {
            Collider hit = _overlapBuffer[i];
            if (hit == null) continue;
            CombatAbilityComponent combat = hit.GetComponentInParent<CombatAbilityComponent>();
            if (combat == null) continue;
            int id = combat.GetInstanceID();
            _frameOccupants.Add(id);
            if (!_occupants.ContainsKey(id))
            {
                _occupants[id] = combat;
                ApplyTo(combat);
            }
            else
                RefreshHighestModifiers(combat);
        }

        List<int> exited = null;
        foreach (var pair in _occupants)
        {
            if (_frameOccupants.Contains(pair.Key)) continue;
            if (exited == null) exited = new List<int>();
            exited.Add(pair.Key);
        }

        if (exited == null) return;
        for (int i = 0; i < exited.Count; i++)
        {
            int id = exited[i];
            if (_occupants.TryGetValue(id, out CombatAbilityComponent combat) && combat != null)
                combat.RemoveMoveSpeedMultiplier(this);
            _occupants.Remove(id);
        }
    }

    private void ApplyTo(CombatAbilityComponent combat)
    {
        if (combat == null || isBurning) return;

        MonsterActor monster = combat.GetComponent<MonsterActor>();
        PlayerHealth soul = combat.GetComponent<PlayerHealth>();

        // Ally boost: soul player, or Possessed Greed body.
        if (soul != null || (monster != null && monster.isPossessed && IsGreedBody(monster)))
        {
            combat.AddMoveSpeedMultiplier(this, allySpeedMultiplier);
            return;
        }

        // Enemy slow only with GR-M01, and only for legal enemies vs the oil owner.
        if (!applyEnemySlow || owner == null || monster == null) return;
        if (!owner.CanDamage(monster)) return;
        combat.AddMoveSpeedMultiplier(this, enemySlowMultiplier);
    }

    private void RefreshHighestModifiers(CombatAbilityComponent combat)
    {
        // Multi-oil: AddMoveSpeedMultiplier replaces same source; other oils use their own sources.
        // Highest slow / boost across oils is approximated by multiplicative stacking of distinct sources.
        // Spec: "多片只取最高" — re-apply this zone's intended multiplier only.
        if (combat == null || isBurning) return;
        combat.RemoveMoveSpeedMultiplier(this);
        ApplyTo(combat);
    }

    private void ClearAllOccupantModifiers()
    {
        foreach (var pair in _occupants)
        {
            if (pair.Value != null)
                pair.Value.RemoveMoveSpeedMultiplier(this);
        }
        _occupants.Clear();
    }

    private static bool IsGreedBody(MonsterActor monster)
    {
        if (monster == null) return false;
        return monster.GetComponentInChildren<EnemyAbility_GreedBlackOil>(true) != null
            || monster.GetComponentInChildren<EnemyAbility_GreedHands>(true) != null;
    }

    /// <summary>Wrath / fire sources call this on overlapping normal oil.</summary>
    public static void IgniteOilsInSphere(Vector3 center, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(center, Mathf.Max(0.1f, radius), ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            GreedBlackOilZone oil = hits[i] != null ? hits[i].GetComponentInParent<GreedBlackOilZone>() : null;
            if (oil != null && oil.IsNormalOil) oil.Ignite();
        }
    }
}
