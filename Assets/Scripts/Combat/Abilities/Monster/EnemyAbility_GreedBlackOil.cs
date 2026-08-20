using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Greed Movement: spray short-lived black oil forward.
/// Base oil accelerates player / Possessed Greed. Enemy slow only with GR-M01.
/// GR-M02: while standing on own normal oil, ignore terrain damage.
/// </summary>
public class EnemyAbility_GreedBlackOil : EnemyAbility
{
    public float pathLength = 4f;
    public float pathLengthWithCard = 7f;
    public float oilWidth = 1.5f;
    public float oilWidthWithCard = 2.4f;
    public float oilLifetime = 4f;
    public float allySpeedMultiplier = 1.5f;
    public float enemySlowMultiplier = 0.5f;
    public float segmentSpacing = 1.1f;
    public GameObject oilZonePrefab;
    public GameObject oilTrailVfxPrefab;
    public GameObject burningOilVfxPrefab;

    private readonly List<GreedBlackOilZone> _ownedZones = new List<GreedBlackOilZone>();
    private bool _wasPossessed;

    private void OnEnable()
    {
        type = AbilityType.Mobility;
        abilityName = "铺黑油";
        cooldown = 0f;
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Greed.BlackOil", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Greed.BlackOil");
    }

    private void Start()
    {
        _wasPossessed = owner != null && owner.isPossessed;
    }

    protected override void Update()
    {
        base.Update();
        if (owner == null) return;
        if (_wasPossessed != owner.isPossessed)
            _wasPossessed = owner.isPossessed;
        PruneZones();
    }

    protected override void OnTrigger()
    {
        if (owner == null)
        {
            EndActivationEffect();
            return;
        }

        Vector3 direction = owner.transform.forward;
        if (owner.isPossessed && TryGetPossessedMouseDirection(out Vector3 aim))
            direction = aim;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = owner.transform.forward;
        direction.Normalize();
        owner.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        bool expand = IsUpgradeUnlocked("GR-M01");
        float length = expand ? pathLengthWithCard : pathLength;
        float width = expand ? oilWidthWithCard : oilWidth;
        bool enemySlow = expand;

        int segments = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(0.25f, segmentSpacing)));
        for (int i = 0; i < segments; i++)
        {
            float dist = (i + 1) * (length / segments);
            Vector3 pos = owner.transform.position + direction * dist;
            pos.y = owner.transform.position.y;
            SpawnOil(pos, width, enemySlow);
        }

        EndActivationEffect();
    }

    private void SpawnOil(Vector3 pos, float width, bool enemySlow)
    {
        GameObject go;
        if (oilZonePrefab != null)
            go = Instantiate(oilZonePrefab, pos, Quaternion.identity);
        else
        {
            go = new GameObject("GreedBlackOilZone");
            go.transform.position = pos;
            go.AddComponent<GreedBlackOilZone>();
        }

        GreedBlackOilZone zone = go.GetComponent<GreedBlackOilZone>();
        if (zone == null) zone = go.AddComponent<GreedBlackOilZone>();
        zone.Initialize(
            owner,
            oilLifetime,
            width,
            allySpeedMultiplier,
            enemySlowMultiplier,
            enemySlow,
            oilTrailVfxPrefab,
            burningOilVfxPrefab);
        _ownedZones.Add(zone);
    }

    private void PruneZones()
    {
        for (int i = _ownedZones.Count - 1; i >= 0; i--)
        {
            if (_ownedZones[i] == null)
                _ownedZones.RemoveAt(i);
        }
    }

    /// <summary>GR-M02: ignore terrain damage while standing on own normal oil.</summary>
    public bool ShouldIgnoreTerrainDamage()
    {
        if (!IsUpgradeUnlocked("GR-M02") || owner == null) return false;
        PruneZones();
        Vector3 pos = owner.transform.position;
        for (int i = 0; i < _ownedZones.Count; i++)
        {
            GreedBlackOilZone zone = _ownedZones[i];
            if (zone == null || !zone.IsNormalOil || !zone.IsOwnedBy(owner)) continue;
            float half = zone.width * 0.5f + 0.15f;
            Vector3 delta = pos - zone.transform.position;
            delta.y = 0f;
            if (Mathf.Abs(delta.x) <= half && Mathf.Abs(delta.z) <= half)
                return true;
        }
        return false;
    }
}
