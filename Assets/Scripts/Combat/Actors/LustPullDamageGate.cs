using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LU-S06 isolation registry: blocks Direct / Contact / new Projectile damage from
/// currently pulled sources onto the player's Possessed Body for the pull window + grace.
/// </summary>
public static class LustPullDamageGate
{
    private static MonsterActor _protectedBody;
    private static readonly HashSet<int> PulledSourceIds = new HashSet<int>();
    /// <summary>Negative while the pull window is open; otherwise unscaled expiry time.</summary>
    private static float _expiresAt = -2f;

    public static void BeginWindow(MonsterActor protectedBody, IEnumerable<MonsterActor> pulledSources)
    {
        Clear();
        _protectedBody = protectedBody;
        if (pulledSources != null)
        {
            foreach (MonsterActor source in pulledSources)
            {
                if (source == null) continue;
                PulledSourceIds.Add(source.GetInstanceID());
            }
        }

        _expiresAt = -1f;
    }

    public static void EndWindow(float graceSeconds)
    {
        if (_protectedBody == null) return;
        _expiresAt = Time.unscaledTime + Mathf.Max(0f, graceSeconds);
    }

    public static void Clear()
    {
        _protectedBody = null;
        PulledSourceIds.Clear();
        _expiresAt = -2f;
    }

    public static bool ShouldBlock(MonsterActor attacker, MonsterActor victim)
    {
        if (attacker == null || victim == null) return false;
        if (_protectedBody == null || victim != _protectedBody) return false;
        if (!victim.isPossessed) return false;
        if (_expiresAt < -1.5f) return false;
        if (_expiresAt >= 0f && Time.unscaledTime > _expiresAt)
        {
            Clear();
            return false;
        }

        return PulledSourceIds.Contains(attacker.GetInstanceID());
    }
}
