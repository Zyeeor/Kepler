using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Short-lived spike hit volume. Damages combatants whose colliders overlap this hazard's collider(s).
/// Configured at spawn time by <see cref="TerrainEffectTile"/>.
/// </summary>
[DisallowMultipleComponent]
public class TerrainSpikeHazard : MonoBehaviour
{
    float _damage = 25f;
    float _lifetime = 0.4f;
    float _despawnAt;
    GameplayEffectDefinition _effect;
    Collider[] _overlapBuffer;
    readonly HashSet<int> _hitIds = new HashSet<int>();

    public void Initialize(float damage, float lifetime, GameplayEffectDefinition effect, int overlapBufferSize = 32)
    {
        _damage = damage;
        _lifetime = Mathf.Max(0.05f, lifetime);
        _effect = effect;
        _despawnAt = Time.time + _lifetime;
        _overlapBuffer = new Collider[Mathf.Max(8, overlapBufferSize)];
        _hitIds.Clear();
    }

    void FixedUpdate()
    {
        if (_overlapBuffer == null)
            _overlapBuffer = new Collider[32];

        ScanAndDamage();

        if (Time.time >= _despawnAt)
            Destroy(gameObject);
    }

    void ScanAndDamage()
    {
        Collider[] ownColliders = GetComponentsInChildren<Collider>();
        if (ownColliders == null || ownColliders.Length == 0) return;

        for (int c = 0; c < ownColliders.Length; c++)
        {
            Collider volume = ownColliders[c];
            if (volume == null || !volume.enabled) continue;

            Bounds bounds = volume.bounds;
            Vector3 halfExtents = bounds.extents;
            if (halfExtents.x < 0.01f) halfExtents.x = 0.01f;
            if (halfExtents.y < 0.01f) halfExtents.y = 0.01f;
            if (halfExtents.z < 0.01f) halfExtents.z = 0.01f;

            int count = Physics.OverlapBoxNonAlloc(
                bounds.center,
                halfExtents,
                _overlapBuffer,
                volume.transform.rotation,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider hit = _overlapBuffer[i];
                if (hit == null) continue;
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (!TryResolveTarget(hit, out CombatAbilityComponent combat, out int id)) continue;
                if (!_hitIds.Add(id)) continue;

                ApplyDamage(combat);
            }
        }
    }

    void ApplyDamage(CombatAbilityComponent combat)
    {
        float damage = _damage;
        if (_effect != null && _effect.damagePerStack > 0f)
            damage = _effect.damagePerStack;

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

    static bool TryResolveTarget(Collider other, out CombatAbilityComponent combat, out int id)
    {
        combat = null;
        id = 0;
        if (other == null) return false;

        combat = other.GetComponentInParent<CombatAbilityComponent>();
        if (combat == null) return false;
        id = combat.GetInstanceID();
        return true;
    }
}
