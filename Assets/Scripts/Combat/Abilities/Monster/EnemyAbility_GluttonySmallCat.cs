using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gluttony mobility: shrink, speed up, light form. Special (Devour) cancels this form.
/// Card GL-M01: extra speed while in form. Card GL-A02 armed on successful use.
/// Card GL-TG01: permanent body move-speed bonus while unlocked.
/// </summary>
public class EnemyAbility_GluttonySmallCat : EnemyAbility
{
    public float formDuration = 3f;
    [Tooltip("Uniform scale multiplier while in small-cat form (CSV: 0.5).")]
    public float scaleMultiplier = 0.5f;
    [Tooltip("Move-speed multiplier while in form (CSV: +100% => 2).")]
    public float speedMultiplier = 2f;
    [Tooltip("Extra speed mult when GL-M01 is unlocked.")]
    public float glM01SpeedBonus = 1.25f;
    [Tooltip("Permanent body move-speed mult when GL-TG01 is unlocked.")]
    public float glTg01MoveSpeedBonus = 1.15f;

    private GluttonyBodyState _state;
    private Coroutine _formRoutine;
    private bool _formActive;
    private Vector3 _baseScale = Vector3.one;
    private float _baseMoveSpeed;
    private bool _capturedBase;
    private bool _tg01Applied;

    private void OnEnable()
    {
        type = AbilityType.Mobility;
        abilityName = "小猫化";
        if (cooldown < 0f) cooldown = 0f;
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Gluttony.SmallCat", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Gluttony.SmallCat");
    }

    private void Start()
    {
        CacheOwnerState();
        CaptureBaseStats();
    }

    protected override void Update()
    {
        base.Update();
        ApplyTg01MoveSpeedIfNeeded();
    }

    protected override void OnTrigger()
    {
        if (owner == null) return;
        CacheOwnerState();
        CaptureBaseStats();
        if (_formRoutine != null) StopCoroutine(_formRoutine);
        _formRoutine = StartCoroutine(FormRoutine());
    }

    /// <summary>Called by Devour / GluttonyBodyState when Special starts.</summary>
    public void ForceExitForm()
    {
        bool wasRunning = _formRoutine != null || _formActive;
        if (_formRoutine != null)
        {
            StopCoroutine(_formRoutine);
            _formRoutine = null;
        }
        ExitForm();
        if (wasRunning) EndActivationEffect();
    }

    private IEnumerator FormRoutine()
    {
        EnterForm();
        if (_state != null && IsUpgradeUnlocked("GL-A02"))
            _state.ArmHuntStepEmpower();

        float duration = GetCardParameter("TransformDuration", formDuration);
        yield return AbilityWait(duration);
        ExitForm();
        _formRoutine = null;
        EndActivationEffect();
    }

    private void EnterForm()
    {
        if (owner == null) return;
        CaptureBaseStats();
        _formActive = true;
        _state?.SetSmallCatActive(true);

        float speedMult = speedMultiplier;
        if (IsUpgradeUnlocked("GL-M01"))
            speedMult *= GetCardParameter("SmallCatSpeedMult", glM01SpeedBonus);

        owner.transform.localScale = _baseScale * Mathf.Max(0.05f, scaleMultiplier);
        owner.moveSpeed = _baseMoveSpeed * speedMult * Tg01Mult();
    }

    private void ExitForm()
    {
        if (!_formActive) return;
        _formActive = false;
        _state?.SetSmallCatActive(false);
        if (owner == null) return;
        owner.transform.localScale = _baseScale;
        owner.moveSpeed = _baseMoveSpeed * Tg01Mult();
    }

    private float Tg01Mult()
    {
        return IsUpgradeUnlocked("GL-TG01")
            ? GetCardParameter("BodyMoveSpeedMult", glTg01MoveSpeedBonus)
            : 1f;
    }

    private void ApplyTg01MoveSpeedIfNeeded()
    {
        if (owner == null || _formActive) return;
        CaptureBaseStats();
        bool want = IsUpgradeUnlocked("GL-TG01");
        if (want == _tg01Applied) return;
        _tg01Applied = want;
        owner.moveSpeed = _baseMoveSpeed * Tg01Mult();
    }

    private void CaptureBaseStats()
    {
        if (owner == null || _capturedBase) return;
        _baseScale = owner.transform.localScale;
        _baseMoveSpeed = owner.moveSpeed > 0f ? owner.moveSpeed : 4f;
        _capturedBase = true;
    }

    private void CacheOwnerState()
    {
        if (owner == null) return;
        _state = owner.GetComponent<GluttonyBodyState>();
        if (_state == null) _state = owner.gameObject.AddComponent<GluttonyBodyState>();
    }

    protected override void OnDisable()
    {
        ForceExitForm();
        base.OnDisable();
    }
}
