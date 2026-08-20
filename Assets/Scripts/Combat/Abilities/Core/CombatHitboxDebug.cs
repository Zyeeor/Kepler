using UnityEngine;

/// <summary>
/// Registers combat physics queries for visualization.
/// Actual drawing is done by <see cref="CombatHitboxDebugSettings"/> via URP end-camera GL lines
/// (real OverlapBox / OverlapSphere bounds — not LineRenderer).
/// </summary>
public static class CombatHitboxDebug
{
    public static bool Enabled;
    public static Color Color = new Color(1f, 0.25f, 0.1f, 0.85f);

    /// <summary>Default lifetime for per-frame / continuous queries.</summary>
    public const float DefaultDuration = 0.2f;

    /// <summary>Fallback lifetime for one-shot melee queries when no VFX duration is supplied.</summary>
    public const float MeleeFallbackDuration = 0.4f;

    private static float _nextSkipLogAt;
    private static float _nextDrawLogAt;
    private static int _drawCallsSinceLog;
    private static int _skipCallsSinceLog;

    public static void DrawSphere(bool abilityEnabled, Vector3 center, float radius, float duration = -1f)
    {
        if (!Enabled)
        {
            LogSkip("Sphere");
            return;
        }
        float d = ResolveDuration(duration);
        LogDraw("Sphere", center, radius, d);
        CombatHitboxDebugSettings.RegisterSphere(center, radius, d);
    }

    public static void DrawBox(bool abilityEnabled, Vector3 center, Vector3 halfExtents, Quaternion rotation, float duration = -1f)
    {
        if (!Enabled)
        {
            LogSkip("Box");
            return;
        }
        float d = ResolveDuration(duration);
        LogDraw("Box", center, halfExtents.magnitude, d);
        CombatHitboxDebugSettings.RegisterBox(center, halfExtents, rotation, d);
    }

    public static void DrawCapsule(bool abilityEnabled, Vector3 start, Vector3 end, float radius, float duration = -1f)
    {
        if (!Enabled)
        {
            LogSkip("Capsule");
            return;
        }
        float d = ResolveDuration(duration);
        LogDraw("Capsule", (start + end) * 0.5f, radius, d);
        CombatHitboxDebugSettings.RegisterCapsule(start, end, radius, d);
    }

    public static void DrawArc(bool abilityEnabled, Vector3 origin, Vector3 forward, float range, float angle, float duration = -1f)
    {
        if (!Enabled)
        {
            LogSkip("Arc");
            return;
        }
        float d = ResolveDuration(duration);
        LogDraw("Arc", origin, range, d);
        CombatHitboxDebugSettings.RegisterArc(origin, forward, range, angle, d);
    }

    public static void DrawRay(bool abilityEnabled, Vector3 origin, Vector3 direction, float distance, float duration = -1f)
    {
        if (!Enabled)
        {
            LogSkip("Ray");
            return;
        }
        Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        float d = ResolveDuration(duration);
        LogDraw("Ray", origin, distance, d);
        CombatHitboxDebugSettings.RegisterRay(origin, dir, distance, d);
    }

    private static float ResolveDuration(float duration)
    {
        if (duration > 0f) return duration;
        if (duration < 0f) return MeleeFallbackDuration;
        return DefaultDuration;
    }

    private static void LogSkip(string kind)
    {
        _skipCallsSinceLog++;
        float now = Time.unscaledTime;
        if (now < _nextSkipLogAt) return;
        _nextSkipLogAt = now + 1f;
        Debug.LogWarning(
            $"[HitboxDebug] SKIP {kind} x{_skipCallsSinceLog} — Enabled=false. " +
            "Check GameManager → CombatHitboxDebugSettings.enableHitboxDebug.");
        _skipCallsSinceLog = 0;
    }

    private static void LogDraw(string kind, Vector3 pos, float size, float duration)
    {
        // Quiet by default; Settings.enableDiagnosticLogs drives heartbeat instead.
        var settings = GameManager.Instance != null
            ? GameManager.Instance.GetComponent<CombatHitboxDebugSettings>()
            : null;
        if (settings == null || !settings.enableDiagnosticLogs) return;

        _drawCallsSinceLog++;
        float now = Time.unscaledTime;
        if (now < _nextDrawLogAt) return;
        _nextDrawLogAt = now + 1f;
        Debug.Log(
            $"[HitboxDebug] DRAW {kind} x{_drawCallsSinceLog} pos={pos} size={size:F2} dur={duration:F2}s");
        _drawCallsSinceLog = 0;
    }
}
