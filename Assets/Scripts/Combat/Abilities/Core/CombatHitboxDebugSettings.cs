using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Global hitbox debug toggle on GameManager.
/// Draws thin GL wireframes after camera render (URP-safe) so Game view shows real OverlapBox/Sphere
/// bounds without LineRenderer clutter and without relying on DDOL OnDrawGizmos.
/// </summary>
public class CombatHitboxDebugSettings : MonoBehaviour
{
    public enum ShapeKind
    {
        Sphere,
        Box,
        Capsule,
        Arc,
        Ray
    }

    public struct Shape
    {
        public ShapeKind kind;
        public Vector3 a;
        public Vector3 b;
        public Quaternion rotation;
        public float radius;
        public float range;
        public float angle;
        public float expiresAt;
    }

    [Header("Hitbox Debug")]
    [Tooltip("Draw combat physics hitboxes as thin GL wireframes in Game / Scene view.")]
    public bool enableHitboxDebug = true;

    [Tooltip("Wire color. Keep alpha modest so skill VFX stay readable.")]
    public Color gizmoColor = new Color(1f, 0.25f, 0.1f, 0.85f);

    [Header("Diagnostics")]
    public bool enableDiagnosticLogs = false;
    public bool showStatusOverlay = false;

    public static bool HasInstance => _instance != null;

    private static CombatHitboxDebugSettings _instance;
    private readonly List<Shape> _shapes = new List<Shape>(32);

    private bool _lastAllow;
    private float _nextHeartbeatAt;
    private float _nextRegisterSkipLogAt;
    private int _registerSkipCount;
    private int _registeredSinceHeartbeat;
    private int _glDrawsSinceHeartbeat;
    private string _lastDrawSummary = "none";
    private float _lastDrawAt;

    private Material _lineMaterial;
    private bool _subscribed;

    public static void EnsureOnGameManager()
    {
        if (GameManager.Instance == null)
        {
            if (Application.isPlaying)
                Debug.LogWarning("[HitboxDebug] EnsureOnGameManager: GameManager.Instance is null.");
            return;
        }

        var existing = GameManager.Instance.GetComponent<CombatHitboxDebugSettings>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        _instance = GameManager.Instance.gameObject.AddComponent<CombatHitboxDebugSettings>();
    }

    private void OnEnable()
    {
        _instance = this;
        EnsureMaterial();
        SubscribeRender();
        SyncEnabled(forceLog: true);
    }

    private void OnDisable()
    {
        UnsubscribeRender();
        if (_instance == this) _instance = null;
        CombatHitboxDebug.Enabled = false;
        _shapes.Clear();
    }

    private void OnDestroy()
    {
        UnsubscribeRender();
        if (_lineMaterial != null)
        {
            Destroy(_lineMaterial);
            _lineMaterial = null;
        }
    }

    private void Update()
    {
        SyncEnabled(forceLog: false);
        if (!enableHitboxDebug || GameManager.IsFormalFlow)
        {
            if (_shapes.Count > 0) _shapes.Clear();
            MaybeHeartbeat();
            return;
        }

        float now = Time.unscaledTime;
        for (int i = _shapes.Count - 1; i >= 0; i--)
        {
            if (_shapes[i].expiresAt <= now)
                _shapes.RemoveAt(i);
        }

        const int maxShapes = 12;
        if (_shapes.Count > maxShapes)
            _shapes.RemoveRange(0, _shapes.Count - maxShapes);

        MaybeHeartbeat();
    }

    private void SubscribeRender()
    {
        if (_subscribed) return;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        _subscribed = true;
    }

    private void UnsubscribeRender()
    {
        if (!_subscribed) return;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        _subscribed = false;
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (!Application.isPlaying || !CombatHitboxDebug.Enabled) return;
        if (camera == null) return;
        if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
        if (_shapes.Count == 0) return;

        DrawShapesGL(camera);
        _glDrawsSinceHeartbeat++;
    }

    private void SyncEnabled(bool forceLog)
    {
        bool allow = enableHitboxDebug && !GameManager.IsFormalFlow;
        CombatHitboxDebug.Enabled = false;
        CombatHitboxDebug.Color = gizmoColor;
        if (_instance == null) _instance = this;

        if (forceLog || allow != _lastAllow)
        {
            _lastAllow = allow;
            if (enableDiagnosticLogs || forceLog)
            {
                Debug.Log(
                    $"[HitboxDebug] SyncEnabled allow={allow} (enable={enableHitboxDebug}, formal={GameManager.IsFormalFlow})");
            }
        }
    }

    private void MaybeHeartbeat()
    {
        if (!enableDiagnosticLogs) return;
        float now = Time.unscaledTime;
        if (now < _nextHeartbeatAt) return;
        _nextHeartbeatAt = now + 2f;
        Debug.Log(
            $"[HitboxDebug] HEARTBEAT enable={enableHitboxDebug} Enabled={CombatHitboxDebug.Enabled} " +
            $"shapes={_shapes.Count} registeredΔ={_registeredSinceHeartbeat} glDrawsΔ={_glDrawsSinceHeartbeat}");
        _registeredSinceHeartbeat = 0;
        _glDrawsSinceHeartbeat = 0;
    }

    public static void RegisterSphere(Vector3 center, float radius, float duration)
    {
        if (!TryBeginRegister("Sphere", out CombatHitboxDebugSettings inst)) return;
        inst.AddShape(new Shape
        {
            kind = ShapeKind.Sphere,
            a = center,
            radius = Mathf.Max(0.01f, radius),
            expiresAt = Time.unscaledTime + Mathf.Max(0.01f, duration)
        }, duration);
    }

    public static void RegisterBox(Vector3 center, Vector3 halfExtents, Quaternion rotation, float duration)
    {
        if (!TryBeginRegister("Box", out CombatHitboxDebugSettings inst)) return;
        inst.AddShape(new Shape
        {
            kind = ShapeKind.Box,
            a = center,
            b = halfExtents,
            rotation = rotation,
            expiresAt = Time.unscaledTime + Mathf.Max(0.01f, duration)
        }, duration);
    }

    public static void RegisterCapsule(Vector3 start, Vector3 end, float radius, float duration)
    {
        if (!TryBeginRegister("Capsule", out CombatHitboxDebugSettings inst)) return;
        inst.AddShape(new Shape
        {
            kind = ShapeKind.Capsule,
            a = start,
            b = end,
            radius = Mathf.Max(0.01f, radius),
            expiresAt = Time.unscaledTime + Mathf.Max(0.01f, duration)
        }, duration);
    }

    public static void RegisterArc(Vector3 origin, Vector3 forward, float range, float angle, float duration)
    {
        if (!TryBeginRegister("Arc", out CombatHitboxDebugSettings inst)) return;
        inst.AddShape(new Shape
        {
            kind = ShapeKind.Arc,
            a = origin,
            b = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward,
            range = Mathf.Max(0.01f, range),
            angle = angle,
            expiresAt = Time.unscaledTime + Mathf.Max(0.01f, duration)
        }, duration);
    }

    public static void RegisterRay(Vector3 origin, Vector3 direction, float distance, float duration)
    {
        if (!TryBeginRegister("Ray", out CombatHitboxDebugSettings inst)) return;
        Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        inst.AddShape(new Shape
        {
            kind = ShapeKind.Ray,
            a = origin,
            b = dir,
            range = Mathf.Max(0.01f, distance),
            expiresAt = Time.unscaledTime + Mathf.Max(0.01f, duration)
        }, duration);
    }

    private void AddShape(Shape shape, float duration)
    {
        // Continuous projectile queries keep a single live outline per kind.
        bool continuous = duration <= CombatHitboxDebug.DefaultDuration + 0.02f;
        if (continuous)
        {
            for (int i = _shapes.Count - 1; i >= 0; i--)
            {
                if (_shapes[i].kind == shape.kind)
                    _shapes.RemoveAt(i);
            }
        }
        _shapes.Add(shape);
    }

    private static bool TryBeginRegister(string kind, out CombatHitboxDebugSettings inst)
    {
        inst = _instance;
        if (inst == null)
        {
            LogRegisterSkip(kind, "instance=NULL");
            return false;
        }
        if (!CombatHitboxDebug.Enabled)
        {
            LogRegisterSkip(kind, "Enabled=false");
            return false;
        }
        inst._registeredSinceHeartbeat++;
        inst._lastDrawSummary = $"{kind} @{Time.frameCount}";
        inst._lastDrawAt = Time.unscaledTime;
        return true;
    }

    private static void LogRegisterSkip(string kind, string reason)
    {
        if (_instance == null || !_instance.enableDiagnosticLogs) return;
        _instance._registerSkipCount++;
        float now = Time.unscaledTime;
        if (now < _instance._nextRegisterSkipLogAt) return;
        _instance._nextRegisterSkipLogAt = now + 1f;
        Debug.LogWarning($"[HitboxDebug] REGISTER SKIP {kind} x{_instance._registerSkipCount} — {reason}");
        _instance._registerSkipCount = 0;
    }

    private void EnsureMaterial()
    {
        if (_lineMaterial != null) return;
        // Internal-Colored supports vertex colors + controllable ZTest for thin debug lines.
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        _lineMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        _lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        _lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        _lineMaterial.SetInt("_Cull", (int)CullMode.Off);
        // LEqual: occluded by opaque geometry; does not paint over VFX as aggressively as Always.
        _lineMaterial.SetInt("_ZWrite", 0);
        _lineMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
        _lineMaterial.renderQueue = 3000;
    }

    private void DrawShapesGL(Camera camera)
    {
        EnsureMaterial();
        _lineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.LoadProjectionMatrix(GL.GetGPUProjectionMatrix(camera.projectionMatrix, false));
        GL.modelview = camera.worldToCameraMatrix;
        GL.Begin(GL.LINES);
        GL.Color(gizmoColor);

        for (int i = 0; i < _shapes.Count; i++)
            EmitShape(_shapes[i]);

        GL.End();
        GL.PopMatrix();
    }

    private static void EmitShape(Shape shape)
    {
        switch (shape.kind)
        {
            case ShapeKind.Sphere:
                EmitWireSphere(shape.a, shape.radius);
                break;
            case ShapeKind.Box:
                EmitWireBox(shape.a, shape.b, shape.rotation);
                break;
            case ShapeKind.Capsule:
                EmitWireCapsule(shape.a, shape.b, shape.radius);
                break;
            case ShapeKind.Arc:
                EmitArc(shape.a, shape.b, shape.range, shape.angle);
                break;
            case ShapeKind.Ray:
                EmitLine(shape.a, shape.a + shape.b * shape.range);
                break;
        }
    }

    private static void EmitWireBox(Vector3 center, Vector3 halfExtents, Quaternion rotation)
    {
        Vector3 c0 = center + rotation * new Vector3(-halfExtents.x, -halfExtents.y, -halfExtents.z);
        Vector3 c1 = center + rotation * new Vector3(halfExtents.x, -halfExtents.y, -halfExtents.z);
        Vector3 c2 = center + rotation * new Vector3(-halfExtents.x, halfExtents.y, -halfExtents.z);
        Vector3 c3 = center + rotation * new Vector3(halfExtents.x, halfExtents.y, -halfExtents.z);
        Vector3 c4 = center + rotation * new Vector3(-halfExtents.x, -halfExtents.y, halfExtents.z);
        Vector3 c5 = center + rotation * new Vector3(halfExtents.x, -halfExtents.y, halfExtents.z);
        Vector3 c6 = center + rotation * new Vector3(-halfExtents.x, halfExtents.y, halfExtents.z);
        Vector3 c7 = center + rotation * new Vector3(halfExtents.x, halfExtents.y, halfExtents.z);

        EmitLine(c0, c1); EmitLine(c1, c3); EmitLine(c3, c2); EmitLine(c2, c0);
        EmitLine(c4, c5); EmitLine(c5, c7); EmitLine(c7, c6); EmitLine(c6, c4);
        EmitLine(c0, c4); EmitLine(c1, c5); EmitLine(c2, c6); EmitLine(c3, c7);
    }

    private static void EmitWireSphere(Vector3 center, float radius)
    {
        // Three rings = standard wire sphere matching OverlapSphere volume.
        EmitCircle(center, Vector3.up, radius, 32);
        EmitCircle(center, Vector3.right, radius, 24);
        EmitCircle(center, Vector3.forward, radius, 24);
    }

    private static void EmitWireCapsule(Vector3 start, Vector3 end, float radius)
    {
        EmitLine(start, end);
        EmitCircle(start, Vector3.up, radius, 20);
        EmitCircle(end, Vector3.up, radius, 20);
    }

    private static void EmitArc(Vector3 origin, Vector3 forward, float range, float angle)
    {
        const int segments = 16;
        Vector3 previous = origin + Quaternion.Euler(0f, -angle * 0.5f, 0f) * forward * range;
        EmitLine(origin, previous);
        for (int i = 1; i <= segments; i++)
        {
            Vector3 next = origin + Quaternion.Euler(0f, Mathf.Lerp(-angle * 0.5f, angle * 0.5f, i / (float)segments), 0f) * forward * range;
            EmitLine(previous, next);
            previous = next;
        }
        EmitLine(origin, previous);
    }

    private static void EmitCircle(Vector3 center, Vector3 normal, float radius, int segments)
    {
        Vector3 n = normal.normalized;
        Vector3 t = Vector3.Cross(n, Vector3.up);
        if (t.sqrMagnitude < 0.0001f) t = Vector3.Cross(n, Vector3.right);
        t.Normalize();
        Vector3 bitangent = Vector3.Cross(n, t);
        Vector3 previous = center + t * radius;
        for (int i = 1; i <= segments; i++)
        {
            float ang = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 next = center + (Mathf.Cos(ang) * t + Mathf.Sin(ang) * bitangent) * radius;
            EmitLine(previous, next);
            previous = next;
        }
    }

    private static void EmitLine(Vector3 from, Vector3 to)
    {
        GL.Vertex(from);
        GL.Vertex(to);
    }

    private void OnGUI()
    {
        if (!showStatusOverlay || !Application.isPlaying || GameManager.IsFormalFlow) return;

        float age = _lastDrawAt > 0f ? Time.unscaledTime - _lastDrawAt : -1f;
        string last = age < 0f ? "none" : $"{_lastDrawSummary} ({age:F1}s ago)";
        string text =
            $"[HitboxDebug] enable={enableHitboxDebug} Enabled={CombatHitboxDebug.Enabled} " +
            $"shapes={_shapes.Count} last={last}";

        var style = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13,
            normal = { textColor = CombatHitboxDebug.Enabled ? Color.green : Color.yellow }
        };
        float w = Mathf.Min(920f, Screen.width - 20f);
        GUI.Box(new Rect((Screen.width - w) * 0.5f, 8f, w, 28f), text, style);
    }
}
