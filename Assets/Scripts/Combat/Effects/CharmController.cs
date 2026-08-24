using UnityEngine;

/// <summary>Time-bounded allied controller used by the Lust imprint.</summary>
public sealed class CharmController : MonoBehaviour, IController
{
    public float duration = 2.5f;
    public MonsterActor nearestEnemyTarget;
    public bool IsCharmed { get; private set; }
    float expiresAt;
    float nextTargetRefresh;
    MonsterActor host;
    IController previousController;

    public static bool IsCharmedMonster(MonsterActor actor)
    {
        if (actor == null) return false;
        CharmController charm = actor.GetComponent<CharmController>();
        return charm != null && charm.IsCharmed;
    }

    public void Apply(MonsterActor target, float seconds = 2.5f)
    {
        if (target == null || target.isPossessed || !target.isPossessable || target.bodyType == MonsterActor.BodyType.Boss) return;
        host = target;
        if (!IsCharmed) previousController = target.Controller;
        IsCharmed = true;
        expiresAt = Time.unscaledTime + Mathf.Max(0f, seconds);
        nearestEnemyTarget = null;
        nextTargetRefresh = 0f;
        target.SetController(this);
    }
    void Update()
    {
        if (IsCharmed && Time.unscaledTime >= expiresAt) Clear();
    }
    public void Clear()
    {
        MonsterActor actor = host;
        IController restore = previousController;
        IsCharmed = false;
        nearestEnemyTarget = null;
        previousController = null;
        if (actor != null && actor.Controller == this)
        {
            if (restore == null || restore == this) restore = actor.GetComponent<AIController>();
            actor.SetController(restore);
        }
    }

    public void OnAttached(Actor owner)
    {
        host = owner as MonsterActor;
    }

    public void OnDetached()
    {
        IsCharmed = false;
        nearestEnemyTarget = null;
    }

    public void Tick(in ActorContext ctx, ref ControlCommand cmd)
    {
        cmd = ControlCommand.Empty;
        if (!IsCharmed || host == null || host.isDowned) return;
        if (Time.unscaledTime >= nextTargetRefresh || nearestEnemyTarget == null || nearestEnemyTarget.isDowned)
        {
            nextTargetRefresh = Time.unscaledTime + 0.25f;
            nearestEnemyTarget = FindNearestEnemy();
            host.targetEnemy = nearestEnemyTarget as Enemy;
        }
        if (nearestEnemyTarget == null) return;

        Vector3 toTarget = nearestEnemyTarget.transform.position - host.transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        cmd.HasAim = true;
        cmd.AimPoint = nearestEnemyTarget.transform.position;
        if (distance > Mathf.Max(1f, host.basicAttackRange * 0.85f))
        {
            cmd.HasMove = true;
            cmd.MoveDirection = distance > 0.01f ? toTarget / distance : Vector3.zero;
            return;
        }

        bool skillReady = false;
        for (int i = 0; i < host.skillAbilities.Count; i++)
            if (host.skillAbilities[i] != null && host.skillAbilities[i].ability != null
                && host.skillAbilities[i].ability.CanTrigger()) { skillReady = true; break; }
        cmd.Pressed = skillReady && distance <= host.skillAttackRange
            ? CommandButtons.Skill1
            : CommandButtons.Basic;
    }

    MonsterActor FindNearestEnemy()
    {
        MonsterActor best = null;
        float bestDistance = float.PositiveInfinity;
        var all = EnemyRegistry.All;
        for (int i = 0; i < all.Count; i++)
        {
            Enemy candidate = all[i];
            if (candidate == null || candidate == host || candidate.isDowned || candidate.isPossessed
                || candidate.bodyType == MonsterActor.BodyType.Boss || IsCharmedMonster(candidate)) continue;
            float distance = (candidate.transform.position - host.transform.position).sqrMagnitude;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = candidate;
        }
        return best;
    }
}
