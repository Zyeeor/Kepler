using System.Collections;
using UnityEngine;

/// <summary>
/// Body-bound Gluttony combat state shared by SmallCat, AbyssMaw, and Devour.
/// Overfed and an unused copied Skill clear whenever this body changes control state.
/// </summary>
[DisallowMultipleComponent]
public class GluttonyBodyState : MonoBehaviour
{
    public const string OverfedEffectTag = "Effect.Combat.GluttonyOverfed";
    public const string OverfedStateTag = "State.Combat.Overfed";

    public GameplayEffectDefinition overfedEffect;

    public bool HasOverfed { get; private set; }
    public bool HasHuntStepEmpower { get; private set; }
    public bool FirstDevourHealUsed { get; private set; }
    public bool IsSmallCatActive { get; private set; }
    public bool HasCopiedSkill => _copiedSkill != null;
    /// <summary>Possessed move-facing turn multiplier while SmallCat + GL-M01 are active.</summary>
    public float SmallCatTurnMult { get; private set; } = 1f;

    private Enemy _owner;
    private bool _wasPossessed;
    private EnemyAbility_GluttonySmallCat _smallCat;
    private EnemyAbility_GluttonyDevour _devour;
    private EnemyAbility _copiedSkill;
    private Coroutine _restoreCopiedSkillRoutine;

    private void Awake()
    {
        _owner = GetComponent<Enemy>();
        _smallCat = GetComponentInChildren<EnemyAbility_GluttonySmallCat>(true);
        _devour = GetComponentInChildren<EnemyAbility_GluttonyDevour>(true);
        _wasPossessed = _owner != null && _owner.isPossessed;
        ResetBodyLifecycle();
    }

    private void LateUpdate()
    {
        if (_owner == null || _wasPossessed == _owner.isPossessed) return;
        _wasPossessed = _owner.isPossessed;
        // Possession init clears Overfed / unused copy / small-cat form.
        // FirstDevourHealUsed stays for this Body lifecycle.
        ClearPossessionBoundState();
    }

    private void OnDisable()
    {
        ResetBodyLifecycle();
    }

    public void GrantOverfed()
    {
        HasOverfed = true;
        if (_owner == null || _owner.Combat == null || overfedEffect == null) return;
        _owner.Combat.ApplyEffect(overfedEffect, _owner.Combat, null, out _);
    }

    public bool TryConsumeOverfed()
    {
        if (!HasOverfed) return false;
        HasOverfed = false;
        if (_owner != null && _owner.Combat != null && overfedEffect != null)
            _owner.Combat.RemoveEffect(overfedEffect);
        return true;
    }

    public void ClearOverfed()
    {
        HasOverfed = false;
        if (_owner != null && _owner.Combat != null && overfedEffect != null)
            _owner.Combat.RemoveEffect(overfedEffect);
    }

    public void ArmHuntStepEmpower()
    {
        HasHuntStepEmpower = true;
    }

    public bool TryConsumeHuntStepEmpower()
    {
        if (!HasHuntStepEmpower) return false;
        HasHuntStepEmpower = false;
        return true;
    }

    public void MarkFirstDevourHealUsed()
    {
        FirstDevourHealUsed = true;
    }

    public void SetSmallCatActive(bool active)
    {
        IsSmallCatActive = active;
        if (!active) SmallCatTurnMult = 1f;
    }

    public void SetSmallCatTurnMult(float mult)
    {
        SmallCatTurnMult = Mathf.Max(0.01f, mult);
    }

    public void ExitSmallCatForAttack()
    {
        if (_smallCat == null)
            _smallCat = GetComponentInChildren<EnemyAbility_GluttonySmallCat>(true);
        _smallCat?.ExitForAttack();
    }

    public void CancelSmallCat()
    {
        if (_smallCat == null)
            _smallCat = GetComponentInChildren<EnemyAbility_GluttonySmallCat>(true);
        _smallCat?.ForceExitForm();
    }

    /// <summary>
    /// Player-only: clones the swallowed enemy's first Skill ability into this body's active Skill slot.
    /// The clone keeps its own source configuration but executes with this Gluttony body as owner.
    /// </summary>
    public bool TryCopySkillFrom(Enemy target, EnemyAbility_GluttonyDevour devour)
    {
        if (_owner == null || !_owner.isPossessed || target == null || devour == null) return false;

        EnemyAbility source = FindFirstSkillAbility(target);
        if (source == null) return false;

        ClearCopiedSkill();
        GameObject copiedRoot = Instantiate(source.gameObject, _owner.transform);
        copiedRoot.name = $"CopiedSkill_{source.abilityName}";
        _copiedSkill = copiedRoot.GetComponent<EnemyAbility>();
        if (_copiedSkill == null)
        {
            Destroy(copiedRoot);
            return false;
        }

        // Instantiated under this body so Awake binds owner via GetComponentInParent.
        // Strip upgrade slots: the copy is a one-shot payload, not a Gluttony card host.
        if (_copiedSkill.upgrades != null)
            _copiedSkill.upgrades.Clear();

        _devour = devour;
        _devour.enabled = false;
        _owner.ReplaceSkillAbility(_devour, _copiedSkill);
        _copiedSkill.Activated += OnCopiedSkillActivated;
        return true;
    }

    private static EnemyAbility FindFirstSkillAbility(Enemy target)
    {
        foreach (MonsterActor.SkillAbilityEntry entry in target.skillAbilities)
            if (entry != null && entry.ability != null) return entry.ability;

        foreach (EnemyAbility ability in target.GetComponentsInChildren<EnemyAbility>(true))
            if (ability.type == EnemyAbility.AbilityType.Skill) return ability;
        return null;
    }

    private void OnCopiedSkillActivated(EnemyAbility ability)
    {
        if (_copiedSkill != ability || _restoreCopiedSkillRoutine != null) return;
        _restoreCopiedSkillRoutine = StartCoroutine(RestoreCopiedSkillAfterActivation(ability));
    }

    private IEnumerator RestoreCopiedSkillAfterActivation(EnemyAbility ability)
    {
        // Let MonsterActor finish iterating the current Skill list before swapping it back.
        yield return null;
        if (_copiedSkill != ability) yield break;

        ability.Activated -= OnCopiedSkillActivated;
        if (_owner != null && _devour != null)
        {
            _owner.RestoreSkillAbility(_devour, ability);
            _devour.enabled = true;
        }

        // Slot is restored; keep the spent copy alive briefly for any in-flight coroutine payload.
        _copiedSkill = null;
        _restoreCopiedSkillRoutine = null;
        if (ability != null)
            Destroy(ability.gameObject, 8f);
    }

    private void ClearCopiedSkill()
    {
        if (_restoreCopiedSkillRoutine != null)
        {
            StopCoroutine(_restoreCopiedSkillRoutine);
            _restoreCopiedSkillRoutine = null;
        }

        if (_copiedSkill != null)
        {
            _copiedSkill.Activated -= OnCopiedSkillActivated;
            if (_owner != null && _devour != null)
                _owner.RestoreSkillAbility(_devour, _copiedSkill);
            Destroy(_copiedSkill.gameObject);
            _copiedSkill = null;
        }

        if (_devour != null) _devour.enabled = true;
    }

    private void ClearPossessionBoundState()
    {
        ClearOverfed();
        HasHuntStepEmpower = false;
        SmallCatTurnMult = 1f;
        CancelSmallCat();
        ClearCopiedSkill();
    }

    private void ResetBodyLifecycle()
    {
        ClearPossessionBoundState();
        FirstDevourHealUsed = false;
    }
}
