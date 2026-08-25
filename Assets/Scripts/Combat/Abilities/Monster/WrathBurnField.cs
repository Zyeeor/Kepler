using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ground burn zone spawned by Wrath slam. Refreshes duration on re-apply; DPS does not stack.
/// Invokes optional oil-ignite hooks for Greed ordinary black oil when present.
/// </summary>
public class WrathBurnField : MonoBehaviour
{
    public float radius = 2.5f;
    public float dps = 5f;
    public float duration = 3f;
    public float tickInterval = 0.5f;
    public Enemy owner;
    public EnemyAbility sourceAbility;
    public GameObject burnVfxPrefab;
    public GameplayEffectDefinition burnEffect;

    private float _expiresAt;
    private float _nextTickAt;
    private GameObject _vfx;
    private Vector3 _vfxAuthoredScale = Vector3.one;
    private float _ownerScaleMultiplier = 1f;

    public void Configure(Enemy fieldOwner, EnemyAbility ability, float fieldRadius, float fieldDps, float fieldDuration, GameObject vfxPrefab, GameplayEffectDefinition effect)
    {
        owner = fieldOwner;
        sourceAbility = ability;
        MonsterActor ownerMonster = fieldOwner as MonsterActor;
        _ownerScaleMultiplier = ownerMonster != null ? ownerMonster.CombatScaleMultiplier : 1f;
        radius = Mathf.Max(0.1f, fieldRadius);
        dps = Mathf.Max(0f, fieldDps);
        duration = Mathf.Max(0.1f, fieldDuration);
        burnVfxPrefab = vfxPrefab;
        burnEffect = effect;
        _expiresAt = Time.time + duration;
        _nextTickAt = Time.time;
        EnsureVfx();
        if (_vfx != null) _vfx.transform.localScale = _vfxAuthoredScale * Mathf.Max(1f, _ownerScaleMultiplier);
        TryIgniteOilsInRadius();
    }

    public void RefreshDuration(float fieldDuration)
    {
        duration = Mathf.Max(0.1f, fieldDuration);
        _expiresAt = Time.time + duration;
    }

    private void EnsureVfx()
    {
        if (_vfx != null || burnVfxPrefab == null) return;
        _vfx = Instantiate(burnVfxPrefab, transform.position, Quaternion.identity, transform);
        _vfxAuthoredScale = _vfx.transform.localScale;
        foreach (var ps in _vfx.GetComponentsInChildren<ParticleSystem>(true))
            ps.Play(true);
    }

    private void Update()
    {
        if (Time.time >= _expiresAt)
        {
            Destroy(gameObject);
            return;
        }

        if (Time.time < _nextTickAt) return;
        _nextTickAt = Time.time + Mathf.Max(0.05f, tickInterval);
        float tickDamage = dps * tickInterval;
        ApplyTick(tickDamage);
    }

    private void ApplyTick(float tickDamage)
    {
        float effectiveRadius = radius * Mathf.Max(1f, _ownerScaleMultiplier);
        Collider[] hits = Physics.OverlapSphere(transform.position, effectiveRadius, ~0, QueryTriggerInteraction.Collide);
        CombatHitboxDebug.DrawSphere(true, transform.position, effectiveRadius, Mathf.Max(0.08f, tickInterval));
        HashSet<int> seen = new HashSet<int>();
        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy != null && owner != null && owner.CanDamage(enemy) && seen.Add(enemy.GetInstanceID()))
            {
                if (sourceAbility != null)
                    sourceAbility.SettleHit(enemy, tickDamage);
                else
                    owner.ApplyOffensiveDamage(enemy, tickDamage);
                ApplyBurnState(enemy.Combat);
                continue;
            }

            PlayerHealth player = hit.GetComponentInParent<PlayerHealth>();
            if (player != null && owner != null && owner.CanDamageSoul() && seen.Add(player.GetInstanceID()))
            {
                if (sourceAbility != null)
                    sourceAbility.SettleHit(player, tickDamage);
                else
                    player.TakeDamage(tickDamage);
                ApplyBurnState(player.GetComponent<CombatAbilityComponent>());
            }
        }
    }

    private void ApplyBurnState(CombatAbilityComponent combat)
    {
        if (combat == null || burnEffect == null) return;
        combat.ApplyEffect(burnEffect, owner != null ? owner.Combat : null, null, out _);
    }

    private void TryIgniteOilsInRadius()
    {
        float effectiveRadius = radius * Mathf.Max(1f, _ownerScaleMultiplier);
        Collider[] hits = Physics.OverlapSphere(transform.position, effectiveRadius, ~0, QueryTriggerInteraction.Collide);
        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null) continue;
                string typeName = behaviour.GetType().Name;
                if (typeName.IndexOf("Oil", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                behaviour.SendMessage("Ignite", SendMessageOptions.DontRequireReceiver);
                behaviour.SendMessage("IgniteOil", SendMessageOptions.DontRequireReceiver);
                behaviour.SendMessage("OnIgnitedByFire", SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    private void OnDestroy()
    {
        if (_vfx != null) Destroy(_vfx);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.05f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}
