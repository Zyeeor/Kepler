using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;


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
    [FormerlySerializedAs("guardHandVfxPrefab")]
    public GameObject leftGuardHandVfxPrefab;
    public GameObject rightGuardHandVfxPrefab;
    public GameObject absorbVfxPrefab;

    public GameObject convertVfxPrefab;
    public float convertVfxDuration = 0.8f;

    [Header("Guard Hand Presentation")]
    public Transform guardHandCenter;
    public float guardHandSideOffset = 0.55f;
    public float guardHandForwardOffset = 0.6f;
    public float guardHandHeightOffset = 0.8f;
    [FormerlySerializedAs("guardHandSpinSpeed")]
    public float guardHandOrbitSweepAngle = 180f;
    public float guardHandOrbitDuration = 0.75f;


    public float guardHandScaleUpDuration = 0.25f;
    public float guardHandInitialScale = 0.2f;

    public bool IsGuarding { get; private set; }


    private float _guardEndsAt;
    private float _absorbedTotal;
    private float _convertedRemainder;
    private int _handsGranted;
    private readonly GameObject[] _guardHandsVfx = new GameObject[2];
    private readonly Vector3[] _guardHandBaseScales = new Vector3[2];
    private readonly Vector3[] _guardHandEndOffsets = new Vector3[2];

    private float _guardStartedAt;
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
        // 附身代价致死宽限期：耐久已归零，不得再起新技能（含 GR-S04 的续护盾分支）。
        if (owner != null && owner.IsAbilityCostDeathPending) return false;
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
        _guardStartedAt = AbilityTime;
        _guardEndsAt = AbilityTime + duration;
        _earlyCancelArmed = IsUpgradeUnlocked("GR-S04");


        if (guardEffect != null && owner.Combat != null)
            owner.Combat.ApplyEffect(guardEffect, owner.Combat, abilityTags, out _);

        if (leftGuardHandVfxPrefab != null || rightGuardHandVfxPrefab != null)
        {
            Transform guardCenter = guardHandCenter != null ? guardHandCenter : owner.transform;
            for (int i = 0; i < _guardHandsVfx.Length; i++)
            {
                GameObject guardHandPrefab = i == 0 ? leftGuardHandVfxPrefab : rightGuardHandVfxPrefab;
                if (guardHandPrefab == null) continue;
                float side = i == 0 ? -1f : 1f;
                GameObject guardHand = VfxPool.Instance.Spawn(
                    guardHandPrefab,
                    guardCenter.position,
                    guardCenter.rotation,
                    guardCenter);
                _guardHandEndOffsets[i] = new Vector3(
                    side * guardHandSideOffset,
                    guardHandHeightOffset,
                    guardHandForwardOffset) * OwnerCombatScaleMultiplier;
                guardHand.transform.localPosition = Quaternion.Euler(
                    0f,
                    -side * guardHandOrbitSweepAngle,
                    0f) * _guardHandEndOffsets[i];

                guardHand.transform.localRotation = Quaternion.identity;
                _guardHandsVfx[i] = guardHand;
                _guardHandBaseScales[i] = guardHand.transform.localScale * OwnerCombatScaleMultiplier;
                guardHand.transform.localScale = _guardHandBaseScales[i] * Mathf.Max(0f, guardHandInitialScale);
                PlayVfx(guardHand);
            }
        }

        while (owner != null && IsGuarding && AbilityTime < _guardEndsAt)
        {
            float scaleT = Mathf.Clamp01((AbilityTime - _guardStartedAt) / Mathf.Max(0.01f, guardHandScaleUpDuration));
            float orbitT = Mathf.Clamp01((AbilityTime - _guardStartedAt) / Mathf.Max(0.01f, guardHandOrbitDuration));
            for (int i = 0; i < _guardHandsVfx.Length; i++)

            {
                GameObject guardHand = _guardHandsVfx[i];
                if (guardHand == null) continue;
                float side = i == 0 ? -1f : 1f;
                guardHand.transform.localPosition = Quaternion.Euler(
                    0f,
                    Mathf.Lerp(-side * guardHandOrbitSweepAngle, 0f, orbitT),

                    0f) * _guardHandEndOffsets[i];
                guardHand.transform.localRotation = Quaternion.identity;
                guardHand.transform.localScale = Vector3.Lerp(

                    _guardHandBaseScales[i] * Mathf.Max(0f, guardHandInitialScale),
                    _guardHandBaseScales[i],
                    scaleT);
            }
            yield return null;
        }


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
            GameObject vfx = VfxPool.Instance.Spawn(absorbVfxPrefab, owner.transform.position + Vector3.up, Quaternion.identity);
            ScaleAbilityObject(vfx);
            PlayVfx(vfx);
            ReleaseVfx(vfx, 0.6f);
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
            GameObject vfx = VfxPool.Instance.Spawn(convertVfxPrefab, owner.transform.position + Vector3.up * 1.2f, Quaternion.identity);
            ScaleAbilityObject(vfx);
            PlayVfx(vfx);
            ReleaseVfx(vfx, Mathf.Max(0.05f, convertVfxDuration));
        }
    }

    private void EndGuard(bool applyCooldown)
    {
        bool hasGuardHands = _guardHandsVfx[0] != null || _guardHandsVfx[1] != null;
        if (!IsGuarding && !hasGuardHands && guardEffect == null)
        {
            if (applyCooldown) currentCooldown = EffectiveCooldown;
            return;
        }

        IsGuarding = false;
        if (owner != null && owner.Combat != null && guardEffect != null)
            owner.Combat.RemoveEffect(guardEffect);
        for (int i = 0; i < _guardHandsVfx.Length; i++)
        {
            if (_guardHandsVfx[i] != null)
                ReleaseVfx(_guardHandsVfx[i]);
            _guardHandsVfx[i] = null;
            _guardHandBaseScales[i] = Vector3.zero;
            _guardHandEndOffsets[i] = Vector3.zero;

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
