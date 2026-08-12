using UnityEngine;

/// <summary>Central opt-in visualization for combat physics queries.</summary>
public static class CombatHitboxDebug
{
    public static bool Enabled;
    public static Color Color = new Color(1f, 0.2f, 0.1f, 0.9f);
    private const float Duration = 0.05f;

    public static void DrawSphere(bool abilityEnabled, Vector3 center, float radius)
    {
        if (!Enabled || !abilityEnabled) return;
        Debug.DrawLine(center + Vector3.left * radius, center + Vector3.right * radius, Color, Duration);
        Debug.DrawLine(center + Vector3.forward * radius, center + Vector3.back * radius, Color, Duration);
        Debug.DrawLine(center + Vector3.up * radius, center + Vector3.down * radius, Color, Duration);
    }

    public static void DrawBox(bool abilityEnabled, Vector3 center, Vector3 halfExtents, Quaternion rotation)
    {
        if (!Enabled || !abilityEnabled) return;
        Vector3 c0 = center + rotation * new Vector3(-halfExtents.x, -halfExtents.y, -halfExtents.z);
        Vector3 c1 = center + rotation * new Vector3(halfExtents.x, -halfExtents.y, -halfExtents.z);
        Vector3 c2 = center + rotation * new Vector3(-halfExtents.x, halfExtents.y, -halfExtents.z);
        Vector3 c3 = center + rotation * new Vector3(halfExtents.x, halfExtents.y, -halfExtents.z);
        Vector3 c4 = center + rotation * new Vector3(-halfExtents.x, -halfExtents.y, halfExtents.z);
        Vector3 c5 = center + rotation * new Vector3(halfExtents.x, -halfExtents.y, halfExtents.z);
        Vector3 c6 = center + rotation * new Vector3(-halfExtents.x, halfExtents.y, halfExtents.z);
        Vector3 c7 = center + rotation * new Vector3(halfExtents.x, halfExtents.y, halfExtents.z);
        DrawEdge(c0, c1); DrawEdge(c0, c2); DrawEdge(c0, c4); DrawEdge(c7, c6); DrawEdge(c7, c5); DrawEdge(c7, c3);
        DrawEdge(c1, c3); DrawEdge(c1, c5); DrawEdge(c2, c3); DrawEdge(c2, c6); DrawEdge(c4, c5); DrawEdge(c4, c6);
    }

    public static void DrawCapsule(bool abilityEnabled, Vector3 start, Vector3 end, float radius)
    {
        if (!Enabled || !abilityEnabled) return;
        Debug.DrawLine(start, end, Color, Duration);
        DrawSphere(abilityEnabled, start, radius);
        DrawSphere(abilityEnabled, end, radius);
    }

    public static void DrawArc(bool abilityEnabled, Vector3 origin, Vector3 forward, float range, float angle)
    {
        if (!Enabled || !abilityEnabled) return;
        const int segments = 12;
        Vector3 previous = origin + Quaternion.Euler(0f, -angle * 0.5f, 0f) * forward * range;
        Debug.DrawLine(origin, previous, Color, Duration);
        for (int i = 1; i <= segments; i++)
        {
            Vector3 next = origin + Quaternion.Euler(0f, Mathf.Lerp(-angle * 0.5f, angle * 0.5f, i / (float)segments), 0f) * forward * range;
            Debug.DrawLine(previous, next, Color, Duration);
            previous = next;
        }
        Debug.DrawLine(origin, previous, Color, Duration);
    }

    private static void DrawEdge(Vector3 from, Vector3 to)
    {
        Debug.DrawLine(from, to, Color, Duration);
    }
}
