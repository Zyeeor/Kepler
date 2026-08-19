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
    [Tooltip("Extra turn responsiveness reserved for GL-M01. Movement turning remains owned by the controller.")]
    public float glM01TurnResponsiveness = 1.25f;

    [Header("Model Swap")]
    [Tooltip("Default body model, pre-placed as a child of this enemy, with its own Animator + Controller (must expose the same param names as every other monster: \"Basic\"/\"Skill\"/\"IsDowned\"/\"Speed\"). Hidden while small-cat form is active.")]
    public GameObject normalModelRoot;
    [Tooltip("Small-cat body model, pre-placed as a child of this enemy, with its own Animator + Controller (different skeleton, so it needs a separate Animator from the normal model; must expose the same param names). Shown only while small-cat form is active.")]
    public GameObject smallCatModelRoot;

    private GluttonyBodyState _state;
    private Coroutine _formRoutine;
    private bool _formActive;
    private Vector3 _baseScale = Vector3.one;
    private float _baseMoveSpeed;
    private bool _capturedBase;

    private void OnEnable()
    {
        type = AbilityType.Mobility;
        abilityName = "小猫化";
        if (cooldown < 0f) cooldown = 0f;
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Gluttony.SmallCat", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Gluttony.SmallCat");
        SetModelSwap(false);
    }

    private void Start()
    {
        CacheOwnerState();
        CaptureBaseStats();
    }

    protected override void Update()
    {
        base.Update();
    }

    protected override void OnTrigger()
    {
        if (owner == null) return;
        CacheOwnerState();
        CaptureBaseStats();
        if (_formActive)
        {
            ForceExitForm();
            return;
        }

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
        if (_state != null && CardManager.Instance != null && CardManager.Instance.IsEffectUnlocked("GL-A02"))
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
        owner.moveSpeed = _baseMoveSpeed * speedMult;
        SetModelSwap(true);
    }

    private void ExitForm()
    {
        if (!_formActive) return;
        _formActive = false;
        _state?.SetSmallCatActive(false);
        SetModelSwap(false);
        if (owner == null) return;
        owner.transform.localScale = _baseScale;
        owner.moveSpeed = _baseMoveSpeed;
    }

    /// <summary>Restores the normal body when a Basic attack is used unless GL-M01 is unlocked.</summary>
    public void ExitForAttack()
    {
        if (!IsUpgradeUnlocked("GL-M01")) ForceExitForm();
    }

    private void SetModelSwap(bool catActive)
    {
        // Each model child carries its own Animator + Controller (different skeleton per
        // form), so switching forms is a plain SetActive — no controller swap needed.
        // Enemy.GetActiveAnimator() picks up whichever child is active for "Basic"/"Skill"/etc.
        if (normalModelRoot != null) normalModelRoot.SetActive(!catActive);
        if (smallCatModelRoot != null) smallCatModelRoot.SetActive(catActive);
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
