using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wrath Body Trait: standing burn aura while WR-B02 is unlocked.
/// Ends on body fatal / owner disable (swap / despawn clears the component lifetime).
/// </summary>
public class EnemyAbility_WrathBurnAura : EnemyAbility
{
    public const string TagBurnAura = "Ability.Monster.Wrath.BurnAura";
    public const string CardBurnAura = "WR-B02";

    public float auraRadius = 2f;
    public float auraDps = 5f;
    public float tickInterval = 0.5f;
    public GameObject auraVfxPrefab;
    public GameplayEffectDefinition burnEffect;

    private float _nextTickAt;
    private GameObject _auraVfx;
    private bool _auraActive;

    private void OnEnable()
    {
        type = AbilityType.Passive;
        abilityName = "以身为薪";
        EnsureTag(TagBurnAura);
        EnsureUpgrade(CardBurnAura);
    }

    protected override void OnTrigger()
    {
        // Passive — no direct trigger.
    }

    protected override void Update()
    {
        base.Update();
        bool wantAura = owner != null && !owner.isDowned && IsUpgradeUnlocked(CardBurnAura);
        if (wantAura != _auraActive)
        {
            _auraActive = wantAura;
            if (_auraActive) BeginAura();
            else EndAura();
        }

        if (!_auraActive || owner == null) return;
        if (Time.time < _nextTickAt) return;
        _nextTickAt = Time.time + Mathf.Max(0.05f, tickInterval);
        float tickDamage = auraDps * tickInterval;
        DamageEnemiesInSphere(owner.transform.position, auraRadius, tickDamage, OnEnemyHit, Mathf.Max(0.08f, tickInterval));
        if (!owner.isPossessed)
            TryDamagePlayerInRadius(owner.transform.position, auraRadius, tickDamage, Mathf.Max(0.08f, tickInterval));
    }

    private void OnEnemyHit(Enemy enemy, Vector3 hitPosition)
    {
        if (enemy == null || burnEffect == null || enemy.Combat == null) return;
        string ignoredReason;
        enemy.Combat.ApplyEffect(burnEffect, owner != null ? owner.Combat : null, abilityTags, out ignoredReason);
    }

    private void BeginAura()
    {
        _nextTickAt = Time.time;
        if (auraVfxPrefab == null || owner == null || _auraVfx != null) return;
        _auraVfx = VfxPool.Instance.Spawn(auraVfxPrefab, owner.transform.position, Quaternion.identity, owner.transform);
        BulletTimeController.MarkVfxOrigin(_auraVfx, IsOwnedByPlayer);
        PlayVfx(_auraVfx);
    }

    private void EndAura()
    {
        if (_auraVfx != null)
        {
            ReleaseVfx(_auraVfx);
            _auraVfx = null;
        }
    }

    protected override void OnDisable()
    {
        EndAura();
        _auraActive = false;
        base.OnDisable();
    }

    public override void ResetForOwnerReuse()
    {
        EndAura();
        _auraActive = false;
        base.ResetForOwnerReuse();
    }

    private void EnsureTag(string tag)
    {
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, tag, System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add(tag);
    }

    private void EnsureUpgrade(string effectId)
    {
        if (upgrades == null) upgrades = new List<UpgradeSlot>();
        if (upgrades.Exists(u => u != null && string.Equals(u.effectId, effectId, System.StringComparison.OrdinalIgnoreCase)))
            return;
        upgrades.Add(new UpgradeSlot { effectId = effectId, unlocked = false });
    }
}
