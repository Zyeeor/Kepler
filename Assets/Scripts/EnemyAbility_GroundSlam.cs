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
        StartCoroutine(SlamRoutine());
    }

    IEnumerator SlamRoutine()
    {
        // First impact (immediate radius hit)
        yield return new WaitForSeconds(firstHitDelay);
        DoRadiusHit(damage, 1f);

        // Aftershock (delayed)
        yield return new WaitForSeconds(secondHitDelay - firstHitDelay);
        DoRadiusHit(damage, secondHitMultiplier);
    }

    void DoRadiusHit(float baseDmg, float multiplier)
    {
        Vector3 center = owner != null ? owner.transform.position : transform.position;
        // Use ~0 to detect all layers — ensures possessed enemy can hit other enemies
        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
        {
            var ph = h.GetComponentInParent<PlayerHealth>();
            if (ph != null) DealDamageToPlayer(ph, baseDmg * multiplier);
            var enemy = h.GetComponentInParent<Enemy>();
            if (enemy != null && enemy != owner && !enemy.isDowned && !enemy.isPossessed) DealDamageTo(enemy, baseDmg * multiplier);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
        Vector3 c = Application.isPlaying && owner != null ? owner.transform.position : transform.position;
        Gizmos.DrawWireSphere(c, radius);
    }
}
