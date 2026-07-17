using UnityEngine;
using System.Collections;

/// <summary>
/// Skill (10s CD): 暴怒锁链 - Fury Chain. Lunges forward, throws a chain that grabs the first
/// enemy in a fan-shaped area in front and pulls them back to the owner. Deals 30% HP damage.
/// </summary>
public class EnemyAbility_ChainPull : EnemyAbility
{
    [Header("Chain Throw")]
    public float castRange = 6f;             // how far the chain can reach
    public float fanAngle = 60f;            // total fan width in degrees
    public float lungeDistance = 3f;        // how far the owner lunges forward
    public float lungeDuration = 0.25f;     // seconds to complete lunge
    public float pullSpeed = 12f;           // how fast the target is pulled

    [Header("Damage")]
    public float percentOfTargetMaxHp = 0.30f; // 30% of target's max HP

    [Header("Animation")]
    public string animTrigger = "ChainPull";

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "暴怒锁链";
        cooldown = cooldown <= 0f ? 10f : cooldown; // default 10s CD per spec
    }

    public override bool CanTrigger()
    {
        // When possessed, player manually triggers; use fan detection (always possible).
        // When AI-controlled, need a detected enemy target.
        if (owner.isPossessed)
            return base.CanTrigger();
        return base.CanTrigger() && owner != null && owner.targetEnemy != null;
    }

    protected override void OnTrigger()
    {
        if (owner == null) return;
        StartCoroutine(ChainRoutine());
    }

    IEnumerator ChainRoutine()
    {
        // 1) Find target inside fan in front of owner
        Enemy target = FindTargetInFan();
        if (target == null) yield break;

        Vector3 dir = (target.transform.position - owner.transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) yield break;
        dir.Normalize();

        // 2) Owner lunges forward toward target
        yield return StartCoroutine(Lunge(dir));

        // 3) Deal damage (% of target's max HP) and pull
        float dmg = target.maxHealth * percentOfTargetMaxHp;
        DealDamageTo(target, dmg);
        yield return StartCoroutine(PullTarget(target));
    }

    IEnumerator Lunge(Vector3 dir)
    {
        Vector3 start = owner.transform.position;
        Vector3 end = start + dir * lungeDistance;
        float t = 0f;
        while (t < lungeDuration)
        {
            t += Time.deltaTime;
            float k = t / lungeDuration;
            owner.transform.position = Vector3.Lerp(start, end, k);
            owner.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            yield return null;
        }
    }

    IEnumerator PullTarget(Enemy target)
    {
        if (target == null || owner == null) yield break;
        // Disable target's own movement during pull by freezing rigidbody
        var trb = target.GetComponent<Rigidbody>();
        bool wasKinematic = false;
        if (trb != null) { wasKinematic = trb.isKinematic; trb.isKinematic = true; }

        Vector3 endPos = owner.transform.position;
        while (target != null && Vector3.Distance(target.transform.position, endPos) > 0.3f)
        {
            target.transform.position = Vector3.MoveTowards(target.transform.position, endPos, pullSpeed * Time.deltaTime);
            yield return null;
        }
        if (trb != null) trb.isKinematic = wasKinematic;
    }

    Enemy FindTargetInFan()
    {
        Vector3 origin = owner.transform.position;
        Vector3 forward = owner.transform.forward;
        Enemy best = null;
        float bestDist = Mathf.Infinity;
        foreach (var e in FindObjectsOfType<Enemy>())
        {
            if (e == owner || e.isDowned || e.isPossessed) continue;
            Vector3 to = e.transform.position - origin;
            to.y = 0f;
            float d = to.magnitude;
            if (d > castRange || d < 0.01f) continue;
            float angle = Vector3.Angle(forward, to);
            if (angle > fanAngle * 0.5f) continue;
            if (d < bestDist) { bestDist = d; best = e; }
        }
        return best;
    }

    void OnDrawGizmosSelected()
    {
        if (owner == null) return;
        Vector3 origin = owner.transform.position;
        Vector3 forward = Application.isPlaying ? owner.transform.forward : transform.forward;
        Vector3 left = Quaternion.Euler(0, -fanAngle * 0.5f, 0) * forward;
        Vector3 right = Quaternion.Euler(0, fanAngle * 0.5f, 0) * forward;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
        Gizmos.DrawLine(origin, origin + left * castRange);
        Gizmos.DrawLine(origin, origin + right * castRange);
    }
}
