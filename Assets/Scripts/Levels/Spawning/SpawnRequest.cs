using UnityEngine;

public enum SpawnOrigin
{
    PeriodicPressure,
    KillEcho,
    BossMinion,
    Boss,
}

public enum FatalCause
{
    PlayerDamage,
    Environment,
    BossConsume,
    WaveCleanup,
    Debug,
    Timeout,
}

public readonly struct SpawnRequest
{
    public readonly SpawnOrigin origin;
    public readonly int difficultyTier;
    public readonly Vector3 avoidPosition;
    public readonly float minDistanceFromAvoid;
    public readonly float expiryTime;

    public SpawnRequest(SpawnOrigin origin, int difficultyTier, Vector3 avoidPosition,
        float minDistanceFromAvoid, float expiryTime)
    {
        this.origin = origin;
        this.difficultyTier = difficultyTier;
        this.avoidPosition = avoidPosition;
        this.minDistanceFromAvoid = minDistanceFromAvoid;
        this.expiryTime = expiryTime;
    }
}

public readonly struct MonsterFatalEvent
{
    public readonly MonsterActor actor;
    public readonly MonsterActor killer;
    public readonly FatalCause cause;
    public readonly SpawnOrigin spawnOrigin;
    public readonly long transactionId;

    public MonsterFatalEvent(MonsterActor actor, MonsterActor killer, FatalCause cause,
        SpawnOrigin spawnOrigin, long transactionId)
    {
        this.actor = actor;
        this.killer = killer;
        this.cause = cause;
        this.spawnOrigin = spawnOrigin;
        this.transactionId = transactionId;
    }
}
