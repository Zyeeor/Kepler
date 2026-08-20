using System.Collections;
using UnityEngine;

/// <summary>
/// Greed Special: full-body Guard absorb. Rooted, cannot dump hands.
/// Converts absorbed damage into hands (100→1, GR-S01: 60→1). GR-S04: 3s + early cancel.
/// </summary>
public class EnemyAbility_GreedGuard : EnemyAbility
{
    public float baseDuration = 1f;
    public float extendedDuration = 3f;
    public float absorbPerHand = 100f;
    public float absorbPerHandWithCard = 60f;
    public GameplayEffectDefinition guardEffect;
    public GameObject guardHandVfxPrefab;
    public GameObject absorbVfxPrefab;
    public GameObject convertVfxPrefab;
    public float convertVfxDuration = 0.8f;

    public bool IsGuarding { get; private set; }

    private float _guardEndsAt;
    private float _absorbedTotal;
    private float _convertedRemainder;
    private int _handsGranted;
    private GameObject _guardVfx;
    private EnemyAbility_GreedHands _hands;
    private Coroutine _guardRoutine;
    private bool _earlyCancelArmed;

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "大手Guard";
        cooldown = cooldown <= 0f ? 4f : cooldown;
        if (abilityTags == null) abilityTags = new System.Collections.Generic.List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Greed.Guard", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Greed.Guard");
    }

    private void Start()
    {
        _hands = owner != null ? owner.GetComponentInChildren<EnemyAbility_GreedHands>(true) : null;
    }

    public override bool CanTrigger()
    {
        if (IsGuarding && IsUpgradeUnlocked("GR-S04"))
            return owner != null && !owner.isDowned;
        return base.CanTrigger();
    }

    public override void Trigger()
    {
        // GR-S04 early cancel must not start a second activation window.
        if (IsGuarding && IsUpgradeUnlocked("GR-S04"))
        {
            EndGuard(applyCooldown: true);
            return;
        }

        base.Trigger();
    }

    protected override void OnTrigger()
    {
        if (owner == null)
        {
            EndActivationEffect();
            return;
        }

        if (_hands == null)
            _hands = owner.GetComponentInChildren<EnemyAbility_GreedHands>(true);

        float duration = IsUpgradeUnlocked("GR-S04") ? extendedDuration : baseDuration;
        if (_guardRoutine != null) StopCoroutine(_guardRoutine);
        _guardRoutine = StartCoroutine(GuardRoutine(duration));
    }

    private IEnumerator GuardRoutine(float duration)
    {
        IsGuarding = true;
        _absorbedTotal = 0f;
        _convertedRemainder = 0f;
        _handsGranted = 0;
        _guardEndsAt = AbilityTime + duration;
        _earlyCancelArmed = IsUpgradeUnlocked("GR-S04");

        if (guardEffect != null && owner.Combat != null)
            owner.Combat.ApplyEffect(guardEffect, owner.Combat, abilityTags, out _);

        if (guardHandVfxPrefab != null)
        {
            _guardVfx = Instantiate(guardHandVfxPrefab, owner.transform);
            _guardVfx.transform.localPosition = Vector3.forward * 0.6f + Vector3.up * 0.8f;
        }

        while (owner != null && IsGuarding && AbilityTime < _guardEndsAt)
            yield return null;

        EndGuard(applyCooldown: true);
        EndActivationEffect();
        _guardRoutine = null;
    }

    /// <summary>
    /// Absorb incoming body damage while guarding. Returns true if fully absorbed (no HP loss).
    /// </summary>
    public bool TryAbsorb(float amount, bool environmental, out float absorbed)
    {
        absorbed = 0f;
        if (!IsGuarding || amount <= 0f) return false;

        absorbed = amount;
        _absorbedTotal += amount;
        ConvertAbsorbed();

        if (absorbVfxPrefab != null && owner != null)
        {
            GameObject vfx = Instantiate(absorbVfxPrefab, owner.transform.position + Vector3.up, Quaternion.identity);
            Destroy(vfx, 0.6f);
        }

        return true;
    }

    private void ConvertAbsorbed()
    {
        float perHand = IsUpgradeUnlocked("GR-S01") ? absorbPerHandWithCard : absorbPerHand;
        if (perHand <= 0f) return;

        float pool = _absorbedTotal - _handsGranted * perHand + _convertedRemainder;
        // Simpler: track remainder from total.
        float convertible = _absorbedTotal;
        int shouldHave = Mathf.FloorToInt(convertible / perHand);
        int toGrant = shouldHave - _handsGranted;
        if (toGrant <= 0) return;

        _handsGranted += toGrant;
        if (_hands != null)
            _hands.AddHands(toGrant);

        if (convertVfxPrefab != null && owner != null)
        {
            GameObject vfx = Instantiate(convertVfxPrefab, owner.transform.position + Vector3.up * 1.2f, Quaternion.identity);
            Destroy(vfx, Mathf.Max(0.05f, convertVfxDuration));
        }
    }

    private void EndGuard(bool applyCooldown)
    {
        if (!IsGuarding && _guardVfx == null && guardEffect == null)
        {
            if (applyCooldown) currentCooldown = EffectiveCooldown;
            return;
        }

        IsGuarding = false;
        if (owner != null && owner.Combat != null && guardEffect != null)
            owner.Combat.RemoveEffect(guardEffect);
        if (_guardVfx != null)
        {
            Destroy(_guardVfx);
            _guardVfx = null;
        }

        // Zero absorb: no convert success feedback (already gated in ConvertAbsorbed).
        if (applyCooldown)
            currentCooldown = EffectiveCooldown;
    }

    protected override void OnDisable()
    {
        EndGuard(applyCooldown: false);
        base.OnDisable();
    }

    public override void ResetForOwnerReuse()
    {
        EndGuard(applyCooldown: false);
        base.ResetForOwnerReuse();
    }
}
