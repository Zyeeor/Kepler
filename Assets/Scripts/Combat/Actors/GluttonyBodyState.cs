using UnityEngine;

/// <summary>
/// Body-bound Gluttony combat flags shared by SmallCat / AbyssMaw / Devour.
/// Overfed clears on possess / unpossess (Canonical: 换身清除 + init 0).
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

    private MonsterActor _owner;
    private bool _wasPossessed;
    private EnemyAbility_GluttonySmallCat _smallCat;

    private void Awake()
    {
        _owner = GetComponent<MonsterActor>();
        _smallCat = GetComponentInChildren<EnemyAbility_GluttonySmallCat>(true);
        _wasPossessed = _owner != null && _owner.isPossessed;
        ClearOverfed();
    }

    private void LateUpdate()
    {
        if (_owner == null) return;
        if (_wasPossessed == _owner.isPossessed) return;
        _wasPossessed = _owner.isPossessed;
        ClearOverfed();
        FirstDevourHealUsed = false;
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
    }

    public void CancelSmallCat()
    {
        if (_smallCat == null)
            _smallCat = GetComponentInChildren<EnemyAbility_GluttonySmallCat>(true);
        _smallCat?.ForceExitForm();
    }
}
