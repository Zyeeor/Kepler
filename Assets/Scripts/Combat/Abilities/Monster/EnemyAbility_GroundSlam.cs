using UnityEngine;
using System.Collections;

/// <summary>
/// Basic Attack: 山崩地裂 - Ground Slam. Slams the ground, dealing damage in a radius.
/// A short delay later, a second wave of damage triggers (secondary aftershock).
/// </summary>
public class EnemyAbility_GroundSlam : EnemyAbility
{
    [Header("Slam Shape")]
    public float radius = 4f;              // damage radius around enemy
    public LayerMask targetMask;            // who gets hit
    public float firstHitDelay = 0.1f;      // delay between trigger and first hit
    public float secondHitDelay = 0.5f;     // delay to second hit (aftershock)
    public float secondHitMultiplier = 0.6f;

    [Header("Animation")]
    public string animTrigger = "GroundSlam";

    private void OnEnable()
    {
        type = AbilityType.BasicAttack;
        abilityName = "山崩地裂";
    }

    public override bool CanTrigger()
    {
        // When possessed, player manually triggers; no target required.
        // When AI-controlled, need a detected player target.
        if (owner.isPossessed)
            return base.CanTrigger();
        return base.CanTrigger() && owner != null && owner.targetPlayer != null;
    }

    protected override void OnTrigger()
    {
        if (owner == null) return;
        var anim = owner.GetComponent<Animator>();
        if (anim != null) anim.SetTrigger("Basic");
        StartCoroutine(SlamRoutine());
    }

    IEnumerator SlamRoutine()
    {
        // First impact (immediate radius hit)
        yield return AbilityWait(firstHitDelay);
        DoRadiusHit(damage, 1f);

        // Aftershock (delayed)
        yield return AbilityWait(secondHitDelay - firstHitDelay);
        DoRadiusHit(damage, secondHitMultiplier);
    }

    void DoRadiusHit(float baseDmg, float multiplier)
    {
        Vector3 center = owner != null ? owner.transform.position : transform.position;
        float hitRadius = ScaleAbilityRadius(radius);
        int layerMask = owner.isPossessed ? ~0 : targetMask;
        CombatHitboxDebug.DrawSphere(drawHitboxes, center, hitRadius, Mathf.Max(secondHitDelay, 0.4f));
        Collider[] hits = Physics.OverlapSphere(center, hitRadius, layerMask, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
        {
            var ph = h.GetComponentInParent<PlayerHealth>();
            if (ph != null) DealDamageToPlayer(ph, baseDmg * multiplier);
            var enemy = h.GetComponentInParent<Enemy>();
            if (owner.CanDamage(enemy)) DealDamageTo(enemy, baseDmg * multiplier);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
        Vector3 c = Application.isPlaying && owner != null ? owner.transform.position : transform.position;
        Gizmos.DrawWireSphere(c, radius);
    }
}
