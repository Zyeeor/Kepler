using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lust Space: short dash that leaves a main Anchor, or swaps with an existing Anchor.
/// LU-M03: after a successful swap, blast at the old Anchor position after 0.15s.
/// </summary>
public class EnemyAbility_LustAnchorSwap : EnemyAbility
{
    public float dashDistance = 4f;
    public float dashDuration = 0.18f;
    public float anchorLifetime = 8f;
    public float aimTurnSpeed = 720f;

    [Header("LU-M03")]
    public float m03BlastDelay = 0.15f;
    public float m03BlastDamage = 30f;
    public float m03BlastRadius = 2.5f;
    public GameObject m03BlastVfx;

    private LustBodyState _state;

    private void OnEnable()
    {
        type = AbilityType.Mobility;
        abilityName = "魅影换位";
        cooldown = cooldown <= 0f ? 1f : cooldown;
        if (abilityTags == null) abilityTags = new List<string>();
        EnsureTag("Ability.Monster.Lust");
        EnsureTag("Ability.Monster.Lust.Anchor");
        EnsureUpgradeSlot("LU-M03");
        EnsureUpgradeSlot("LU-TG01");
    }

    private void EnsureTag(string tag)
    {
        if (!abilityTags.Exists(t => string.Equals(t, tag, System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add(tag);
    }

    private void EnsureUpgradeSlot(string effectId)
    {
        if (upgrades == null) upgrades = new List<UpgradeSlot>();
        if (upgrades.Exists(u => u != null && string.Equals(u.effectId, effectId, System.StringComparison.OrdinalIgnoreCase)))
            return;
        upgrades.Add(new UpgradeSlot { effectId = effectId, unlocked = false });
    }

    protected override void OnTrigger()
    {
        CacheState();
        StartCoroutine(AnchorRoutine());
    }

    private IEnumerator AnchorRoutine()
    {
        if (owner == null)
        {
            EndActivationEffect();
            yield break;
        }

        Vector3 direction = owner.transform.forward;
        if (owner.isPossessed && TryGetPossessedMouseDirection(out Vector3 aim))
        {
            yield return StartCoroutine(RotatePossessedOwnerTowards(aim, aimTurnSpeed));
            direction = aim;
        }
        else if (!owner.isPossessed && owner.targetPlayer != null)
        {
            direction = owner.targetPlayer.position - owner.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) direction.Normalize();
            else direction = owner.transform.forward;
            owner.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = owner.transform.forward;
        direction.Normalize();

        CacheState();
        bool hadAnchor = _state != null && _state.HasValidAnchor;
        if (!hadAnchor)
        {
            Vector3 start = owner.transform.position;
            Vector3 end = start + direction * dashDistance;
            yield return MoveOwner(start, end, dashDuration);
            if (owner == null)
            {
                EndActivationEffect();
                yield break;
            }

            _state?.PlaceOrReplaceAnchor(start, owner.transform.rotation,
                GetCardParameter("AnchorLifetime", anchorLifetime));
        }
        else
        {
            LustAnchorMarker anchor = _state.ActiveAnchor;
            Vector3 ownerPos = owner.transform.position;
            Vector3 anchorPos = anchor != null ? anchor.transform.position : ownerPos;
            Vector3 oldAnchorPos = anchorPos;

            if (_state.anchorSwapVfx != null)
            {
                Object.Instantiate(_state.anchorSwapVfx, ownerPos, Quaternion.identity);
                Object.Instantiate(_state.anchorSwapVfx, anchorPos, Quaternion.identity);
            }

            owner.transform.position = new Vector3(anchorPos.x, ownerPos.y, anchorPos.z);
            if (anchor != null)
            {
                anchor.transform.position = new Vector3(ownerPos.x, anchorPos.y, ownerPos.z);
                anchor.RefreshLifetime(GetCardParameter("AnchorLifetime", anchorLifetime));
            }

            if (IsUpgradeUnlocked("LU-M03"))
                StartCoroutine(M03BlastRoutine(oldAnchorPos));
        }

        EndActivationEffect();
    }

    private IEnumerator M03BlastRoutine(Vector3 blastPos)
    {
        float delay = GetCardParameter("BlastDelay", m03BlastDelay);
        yield return AbilityWait(delay);
        if (owner == null) yield break;

        float dmg = GetCardParameter("Dmg", m03BlastDamage);
        float radius = GetCardParameter("R", m03BlastRadius);
        if (m03BlastVfx != null)
            Object.Instantiate(m03BlastVfx, blastPos, Quaternion.identity);
        DamageEnemiesInSphere(blastPos, radius, dmg, null, -1f);
        if (!owner.isPossessed)
            TryDamagePlayerInRadius(blastPos, radius, dmg, -1f);
    }

    private IEnumerator MoveOwner(Vector3 start, Vector3 end, float duration)
    {
        if (owner == null) yield break;
        owner.IsAbilityFacingLocked = true;
        if (duration <= 0.0001f)
        {
            owner.transform.position = end;
            owner.IsAbilityFacingLocked = false;
            yield break;
        }

        float elapsed = 0f;
        while (owner != null && elapsed < duration)
        {
            elapsed += AbilityDeltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            owner.transform.position = Vector3.Lerp(start, end, u);
            yield return null;
        }

        if (owner != null)
        {
            owner.transform.position = end;
            owner.IsAbilityFacingLocked = false;
        }
    }

    private void CacheState()
    {
        if (owner == null) return;
        _state = owner.GetComponent<LustBodyState>();
        if (_state == null) _state = owner.gameObject.AddComponent<LustBodyState>();
    }

    protected override void OnDisable()
    {
        if (owner != null) owner.IsAbilityFacingLocked = false;
        base.OnDisable();
    }

    public override void ResetForOwnerReuse()
    {
        if (owner != null) owner.IsAbilityFacingLocked = false;
        CacheState();
        _state?.ClearBodyBoundState();
        base.ResetForOwnerReuse();
    }
}
