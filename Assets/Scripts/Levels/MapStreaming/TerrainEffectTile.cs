using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gameplay hazard / buff tile. Mount on a Tile prefab (alongside TileVisual).
/// Occupancy uses Physics.OverlapBox (characters move via transform without Rigidbody,
/// so OnTriggerEnter is unreliable).
/// </summary>
[DisallowMultipleComponent]
public class TerrainEffectTile : MonoBehaviour
{
    public enum TerrainEffectKind
    {
        SpeedBoost,
        Slow,
        Spike,
        Lava
    }

    [Header("Kind")]
    public TerrainEffectKind kind = TerrainEffectKind.SpeedBoost;

    [Header("Shared Effect")]
    [Tooltip("Optional GameplayEffect applied on enter / lava refresh. Speed & Slow can omit and use moveSpeedMultiplier only.")]
    public GameplayEffectDefinition effect;

    [Header("Detection Volume")]
    [Tooltip("Local-space center of the occupancy box (Tile root space).")]
    public Vector3 detectionCenter = new Vector3(0f, 1.0f, 0f);
    [Tooltip("Local-space size of the occupancy box. Default covers a 1x1 tile with standing height.")]
    public Vector3 detectionSize = new Vector3(1.4f, 2.5f, 1.4f);
    [Tooltip("Layers that can occupy this tile. Default = everything.")]
    public LayerMask detectionMask = ~0;
    [Min(1)] public int overlapBufferSize = 32;

    [Header("Speed / Slow")]
    [Tooltip("Move speed multiplier while standing on this tile. SpeedBoost default 1.5, Slow default 0.5.")]
    public float moveSpeedMultiplier = 1.5f;

    [Header("Spike")]
    [Tooltip("Instant damage dealt on enter only (staying does not re-trigger).")]
    public float spikeDamage = 25f;

    [Header("Lava")]
    [Tooltip("Baseline damage added per lava stack on each periodic tick.")]
    public float lavaBaseDamagePerStack = 5f;
    [Tooltip("While standing on lava, re-apply / stack every this many seconds.")]
    public float lavaStackInterval = 0.5f;
    [Min(1)] public int lavaMaxStacks = 5;
    [Tooltip("Lava effect duration (seconds). Refreshed on each stack pulse.")]
    public float lavaEffectDuration = 3f;
    [Tooltip("Optional fire VFX parented to the victim while lava effect is active. Falls back to effect.activeVfxPrefab.")]
    public GameObject lavaFireVfxPrefab;

    private readonly HashSet<int> _occupants = new HashSet<int>();
    private readonly HashSet<int> _frameOccupants = new HashSet<int>();
    private readonly Dictionary<int, CombatAbilityComponent> _combatById = new Dictionary<int, CombatAbilityComponent>();
    private readonly Dictionary<int, float> _lavaNextPulseAt = new Dictionary<int, float>();
    private Collider[] _overlapBuffer;
    private BoxCollider _volumeCollider;

    void Awake()
    {
        overlapBufferSize = Mathf.Max(8, overlapBufferSize);
        _overlapBuffer = new Collider[overlapBufferSize];
        EnsureDetectionVolume();
    }

    void OnValidate()
    {
        lavaMaxStacks = Mathf.Max(1, lavaMaxStacks);
        lavaStackInterval = Mathf.Max(0.05f, lavaStackInterval);
        lavaEffectDuration = Mathf.Max(0.1f, lavaEffectDuration);
        detectionSize = new Vector3(
            Mathf.Max(0.1f, detectionSize.x),
            Mathf.Max(0.1f, detectionSize.y),
            Mathf.Max(0.1f, detectionSize.z));
        if (kind == TerrainEffectKind.Slow && Mathf.Approximately(moveSpeedMultiplier, 1.5f))
            moveSpeedMultiplier = 0.5f;
        if (kind == TerrainEffectKind.SpeedBoost && Mathf.Approximately(moveSpeedMultiplier, 0.5f))
            moveSpeedMultiplier = 1.5f;
    }

    void FixedUpdate()
    {
        ScanOccupants();
    }

    /// <summary>
    /// Ensures a corrected trigger box used both as editor gizmo source and OverlapBox volume.
    /// Replaces historically broken auto-fit colliders (rotated thin slabs underground).
    /// </summary>
    public void EnsureDetectionVolume()
    {
        _volumeCollider = GetComponent<BoxCollider>();
        if (_volumeCollider == null)
            _volumeCollider = gameObject.AddComponent<BoxCollider>();

        _volumeCollider.isTrigger = true;
        _volumeCollider.center = detectionCenter;
        _volumeCollider.size = detectionSize;
        _volumeCollider.enabled = true;

        // Kinematic RB helps legacy trigger paths if any other system still relies on them.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeAll;
    }

    private void ScanOccupants()
    {
        if (_overlapBuffer == null || _overlapBuffer.Length != overlapBufferSize)
            _overlapBuffer = new Collider[Mathf.Max(8, overlapBufferSize)];

        Vector3 worldCenter = transform.TransformPoint(detectionCenter);
        Vector3 lossy = transform.lossyScale;
        Vector3 halfExtents = new Vector3(
            detectionSize.x * 0.5f * Mathf.Abs(lossy.x),
            detectionSize.y * 0.5f * Mathf.Abs(lossy.y),
            detectionSize.z * 0.5f * Mathf.Abs(lossy.z));

        // World-aligned box: chunk tiles are grid-placed; art may be yaw-rotated 45°.
        int count = Physics.OverlapBoxNonAlloc(
            worldCenter,
            halfExtents,
            _overlapBuffer,
            Quaternion.identity,
            detectionMask,
            QueryTriggerInteraction.Ignore);

        _frameOccupants.Clear();
        for (int i = 0; i < count; i++)
        {
            Collider hit = _overlapBuffer[i];
            if (hit == null) continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            if (!TryResolveTarget(hit, out CombatAbilityComponent combat, out int id)) continue;

            _frameOccupants.Add(id);
            _combatById[id] = combat;

            if (_occupants.Add(id))
                HandleEnter(combat, id);
            else
                HandleStay(combat, id);
        }

        // Exits
        if (_occupants.Count > 0)
        {
            List<int> exited = null;
            foreach (int id in _occupants)
            {
                if (_frameOccupants.Contains(id)) continue;
                if (exited == null) exited = new List<int>();
                exited.Add(id);
            }

            if (exited != null)
            {
                for (int i = 0; i < exited.Count; i++)
                {
                    int id = exited[i];
                    _combatById.TryGetValue(id, out CombatAbilityComponent combat);
                    HandleExit(combat, id);
                    _occupants.Remove(id);
                    _combatById.Remove(id);
                    _lavaNextPulseAt.Remove(id);
                }
            }
        }
    }

    private void HandleEnter(CombatAbilityComponent combat, int id)
    {
        switch (kind)
        {
            case TerrainEffectKind.SpeedBoost:
            case TerrainEffectKind.Slow:
                ApplySpeed(combat);
                if (effect != null)
                {
                    string reason;
                    combat.ApplyEffect(effect, null, null, out reason, 9999f, 1);
                }
                break;
            case TerrainEffectKind.Spike:
                ApplySpikeDamage(combat);
                break;
            case TerrainEffectKind.Lava:
                _lavaNextPulseAt[id] = Time.time;
                PulseLava(combat);
                break;
        }
    }

    private void HandleStay(CombatAbilityComponent combat, int id)
    {
        if (kind != TerrainEffectKind.Lava) return;

        if (!_lavaNextPulseAt.TryGetValue(id, out float nextAt))
            nextAt = Time.time;
        if (Time.time < nextAt) return;

        _lavaNextPulseAt[id] = Time.time + lavaStackInterval;
        PulseLava(combat);
    }

    private void HandleExit(CombatAbilityComponent combat, int id)
    {
        if (combat == null) return;
        if (kind == TerrainEffectKind.SpeedBoost || kind == TerrainEffectKind.Slow)
        {
            combat.RemoveMoveSpeedMultiplier(this);
            if (effect != null) combat.RemoveEffect(effect);
        }
    }

    private void ApplySpeed(CombatAbilityComponent combat)
    {
        combat.AddMoveSpeedMultiplier(this, moveSpeedMultiplier);
    }

    private void ApplySpikeDamage(CombatAbilityComponent combat)
    {
        float damage = spikeDamage;
        if (effect != null && effect.damagePerStack > 0f)
            damage = effect.damagePerStack;

        MonsterActor monster = combat.GetComponent<MonsterActor>();
        if (monster == null) monster = combat.GetComponentInParent<MonsterActor>();
        if (monster != null)
        {
            monster.TakeEnvironmentalDamage(damage);
            return;
        }

        PlayerHealth soul = combat.GetComponent<PlayerHealth>();
        if (soul == null) soul = combat.GetComponentInParent<PlayerHealth>();
        if (soul != null)
            soul.TakeDamage(damage);
    }

    private void PulseLava(CombatAbilityComponent combat)
    {
        GameplayEffectDefinition def = effect;
        if (def == null)
        {
            Debug.LogWarning($"[TerrainEffectTile] Lava tile '{name}' has no Effect assigned.", this);
            return;
        }

        // Prefer tile VFX if the effect asset has none.
        if (def.activeVfxPrefab == null && lavaFireVfxPrefab != null)
        {
            // Do not mutate the shared asset permanently; spawn via Register after apply if needed.
        }

        float duration = lavaEffectDuration > 0f ? lavaEffectDuration : def.duration;
        int stacks = lavaMaxStacks > 0 ? lavaMaxStacks : def.maxStacks;
        float damage = lavaBaseDamagePerStack > 0f ? lavaBaseDamagePerStack : def.damagePerStack;
        float interval = lavaStackInterval > 0f ? lavaStackInterval : def.periodicInterval;

        string reason;
        bool applied = combat.ApplyEffect(
            def, null, null, out reason,
            duration, stacks, damage, interval,
            GameplayEffectStackPolicy.AddStack);

        if (!applied)
        {
            Debug.LogWarning($"[TerrainEffectTile] Lava apply failed on '{combat.name}': {reason}", this);
            return;
        }

        // Ensure fire VFX exists even when the asset's activeVfx was empty at create time.
        if (lavaFireVfxPrefab != null)
        {
            // If effect already spawned VFX via asset, skip; otherwise attach one.
            // Cheap check: look for a child named from the prefab.
            string marker = "__LavaFireVfx";
            Transform existing = combat.transform.Find(marker);
            if (existing == null)
            {
                GameObject vfx = Instantiate(lavaFireVfxPrefab, combat.transform);
                vfx.name = marker;
                vfx.transform.localPosition = Vector3.up * 0.5f;
                combat.RegisterEffectVfx(def, vfx);
            }
        }
    }

    private static bool TryResolveTarget(Collider other, out CombatAbilityComponent combat, out int id)
    {
        combat = null;
        id = 0;
        if (other == null) return false;

        combat = other.GetComponentInParent<CombatAbilityComponent>();
        if (combat == null) return false;
        id = combat.GetInstanceID();
        return true;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = kind == TerrainEffectKind.Lava
            ? new Color(1f, 0.35f, 0.05f, 0.35f)
            : kind == TerrainEffectKind.Spike
                ? new Color(1f, 0.2f, 0.2f, 0.35f)
                : new Color(0.2f, 0.8f, 1f, 0.25f);
        Matrix4x4 matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.matrix = matrix;
        Gizmos.DrawCube(detectionCenter, detectionSize);
        Gizmos.DrawWireCube(detectionCenter, detectionSize);
    }
#endif
}
