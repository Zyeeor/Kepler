using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Displays target-side status symbols in screen-facing space. Envy Marks and Lust Links
/// share one layout so a target with both states keeps the symbols adjacent.
/// </summary>
[DisallowMultipleComponent]
public sealed class MonsterStatusMarker : MonoBehaviour
{
    public enum MarkerKind
    {
        Envy = 0,
        Lust = 1
    }

    private const string ShaderName = "Possession/MonsterStatusMarker";
    private const float DefaultMarkerSize = 0.8f;
    private const float DefaultAdjacentSpacing = 0.08f;
    private const float DefaultGlowIntensity = 2.2f;
    private const float DefaultPulseSpeed = 5f;
    private static readonly Vector2 DefaultMarkerOffset = new Vector2(0f, 0.45f);
    private static readonly Color DefaultEnvyColor = new Color(0.25f, 0.9f, 1f, 1f);
    private static readonly Color DefaultLustColor = new Color(1f, 0.3f, 0.8f, 1f);
    private static readonly int MarkerKindId = Shader.PropertyToID("_MarkerKind");
    private static readonly int MarkerColorId = Shader.PropertyToID("_MarkerColor");
    private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
    private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");

    private readonly HashSet<int> _envySources = new HashSet<int>();
    private readonly HashSet<int> _lustSources = new HashSet<int>();
    private readonly Dictionary<MarkerKind, MarkerVisual> _visuals = new Dictionary<MarkerKind, MarkerVisual>();

    private Enemy _host;
    private ActorVisualFx _actorVisualFx;
    private Transform _visualRoot;
    private Renderer[] _hostRenderers;
    private Camera _camera;

    private sealed class MarkerVisual
    {
        public GameObject gameObject;
        public Material material;
    }

    public static void ShowEnvy(Enemy target, Enemy source)
    {
        if (target == null) return;
        MonsterStatusMarker marker = EnsureOn(target);
        marker.SetSource(MarkerKind.Envy, source, true);
    }

    public static void HideEnvy(Enemy target, Enemy source)
    {
        if (target == null) return;
        MonsterStatusMarker marker = target.GetComponent<MonsterStatusMarker>();
        if (marker != null) marker.SetSource(MarkerKind.Envy, source, false);
    }

    public static void ShowLust(Enemy target, Enemy source)
    {
        if (target == null) return;
        MonsterStatusMarker marker = EnsureOn(target);
        marker.SetSource(MarkerKind.Lust, source, true);
    }

    public static void HideLust(Enemy target, Enemy source)
    {
        if (target == null) return;
        MonsterStatusMarker marker = target.GetComponent<MonsterStatusMarker>();
        if (marker != null) marker.SetSource(MarkerKind.Lust, source, false);
    }

    private static MonsterStatusMarker EnsureOn(Enemy target)
    {
        MonsterStatusMarker marker = target.GetComponent<MonsterStatusMarker>();
        if (marker == null) marker = target.gameObject.AddComponent<MonsterStatusMarker>();
        return marker;
    }

    private void Awake()
    {
        _host = GetComponent<Enemy>();
        _actorVisualFx = GetComponent<ActorVisualFx>();
        _hostRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void LateUpdate()
    {
        if (_envySources.Count == 0 && _lustSources.Count == 0)
        {
            if (_visualRoot != null) _visualRoot.gameObject.SetActive(false);
            return;
        }

        if (_camera == null || !_camera.isActiveAndEnabled)
            _camera = Camera.main;
        if (_camera == null) return;

        EnsureVisualRoot();
        _visualRoot.gameObject.SetActive(true);

        if (_actorVisualFx == null)
            _actorVisualFx = GetComponent<ActorVisualFx>();

        Vector3 top = GetHostTopPoint();
        Vector3 facing = -_camera.transform.forward;
        if (facing.sqrMagnitude < 0.0001f) facing = Vector3.forward;
        _visualRoot.SetPositionAndRotation(
            top,
            Quaternion.LookRotation(facing.normalized, _camera.transform.up));

        bool showEnvy = _envySources.Count > 0;
        bool showLust = _lustSources.Count > 0;
        bool showBoth = showEnvy && showLust;
        float markerSize = GetMarkerSize();
        float halfGap = showBoth ? (Mathf.Max(0.05f, markerSize) + GetAdjacentSpacing()) * 0.5f : 0f;

        UpdateVisual(MarkerKind.Envy, showEnvy, showBoth ? -halfGap : 0f, markerSize);
        UpdateVisual(MarkerKind.Lust, showLust, showBoth ? halfGap : 0f, markerSize);
    }

    private void OnDestroy()
    {
        foreach (MarkerVisual visual in _visuals.Values)
        {
            if (visual == null) continue;
            if (visual.material != null) Destroy(visual.material);
        }
        _visuals.Clear();
    }

    private void SetSource(MarkerKind kind, Enemy source, bool active)
    {
        int sourceId = source != null ? source.GetInstanceID() : 0;
        HashSet<int> sources = kind == MarkerKind.Envy ? _envySources : _lustSources;
        if (active) sources.Add(sourceId);
        else sources.Remove(sourceId);
    }

    private void EnsureVisualRoot()
    {
        if (_visualRoot != null) return;
        GameObject root = new GameObject("__MonsterStatusMarkers");
        _visualRoot = root.transform;
        _visualRoot.SetParent(transform, false);
    }

    private void UpdateVisual(MarkerKind kind, bool visible, float horizontalOffset, float markerSize)
    {
        MarkerVisual visual = null;
        if (visible)
        {
            visual = GetOrCreateVisual(kind);
            if (visual == null) return;
            visual.gameObject.SetActive(true);
            RefreshMaterial(kind, visual.material);
            float size = Mathf.Max(0.05f, markerSize);
            Vector2 markerOffset = GetMarkerOffset();
            visual.gameObject.transform.localPosition = new Vector3(
                markerOffset.x + horizontalOffset,
                markerOffset.y,
                0f);
            visual.gameObject.transform.localRotation = Quaternion.identity;
            visual.gameObject.transform.localScale = new Vector3(size, size, 1f);
        }
        else if (_visuals.TryGetValue(kind, out visual) && visual != null)
        {
            visual.gameObject.SetActive(false);
        }
    }

    private void RefreshMaterial(MarkerKind kind, Material material)
    {
        if (material == null) return;
        material.SetColor(MarkerColorId, kind == MarkerKind.Envy ? GetEnvyColor() : GetLustColor());
        material.SetFloat(GlowIntensityId, GetGlowIntensity());
        material.SetFloat(PulseSpeedId, GetPulseSpeed());
    }

    private MarkerVisual GetOrCreateVisual(MarkerKind kind)
    {
        if (_visuals.TryGetValue(kind, out MarkerVisual existing) && existing != null)
            return existing;

        EnsureVisualRoot();
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "__MonsterStatusMarker_" + kind;
        go.transform.SetParent(_visualRoot, false);
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
            shader = Resources.Load<Shader>("MonsterStatus/MonsterStatusMarker");
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (shader == null || renderer == null)
        {
            if (renderer != null) renderer.enabled = false;
            MarkerVisual unavailable = new MarkerVisual { gameObject = go };
            _visuals[kind] = unavailable;
            return unavailable;
        }

        Material material = new Material(shader);
        material.name = "MAT_MONSTER_STATUS_MARKER_" + kind;
        material.SetFloat(MarkerKindId, kind == MarkerKind.Envy ? 0f : 1f);
        material.SetColor(MarkerColorId, kind == MarkerKind.Envy ? GetEnvyColor() : GetLustColor());
        material.SetFloat(GlowIntensityId, GetGlowIntensity());
        material.SetFloat(PulseSpeedId, GetPulseSpeed());
        renderer.sharedMaterial = material;

        MarkerVisual visual = new MarkerVisual
        {
            gameObject = go,
            material = material
        };
        _visuals[kind] = visual;
        return visual;
    }

    private float GetMarkerSize()
    {
        return _actorVisualFx != null
            ? _actorVisualFx.monsterStatusMarkerSize
            : DefaultMarkerSize;
    }

    private Vector2 GetMarkerOffset()
    {
        return _actorVisualFx != null
            ? _actorVisualFx.monsterStatusMarkerOffset
            : DefaultMarkerOffset;
    }

    private float GetAdjacentSpacing()
    {
        return _actorVisualFx != null
            ? _actorVisualFx.monsterStatusMarkerAdjacentSpacing
            : DefaultAdjacentSpacing;
    }

    private Color GetEnvyColor()
    {
        return _actorVisualFx != null
            ? _actorVisualFx.envyStatusMarkerColor
            : DefaultEnvyColor;
    }

    private Color GetLustColor()
    {
        return _actorVisualFx != null
            ? _actorVisualFx.lustStatusMarkerColor
            : DefaultLustColor;
    }

    private float GetGlowIntensity()
    {
        return _actorVisualFx != null
            ? _actorVisualFx.monsterStatusMarkerGlowIntensity
            : DefaultGlowIntensity;
    }

    private float GetPulseSpeed()
    {
        return _actorVisualFx != null
            ? _actorVisualFx.monsterStatusMarkerPulseSpeed
            : DefaultPulseSpeed;
    }

    private Vector3 GetHostTopPoint()
    {
        Vector3 fallback = _host != null ? _host.transform.position : transform.position;
        float highest = fallback.y + 1f;
        bool found = false;
        if (_hostRenderers != null)
        {
            for (int i = 0; i < _hostRenderers.Length; i++)
            {
                Renderer renderer = _hostRenderers[i];
                if (renderer == null || !renderer.enabled) continue;
                if (_visualRoot != null && renderer.transform.IsChildOf(_visualRoot)) continue;
                if (!found || renderer.bounds.max.y > highest)
                {
                    highest = renderer.bounds.max.y;
                    found = true;
                }
            }
        }

        fallback.y = highest;
        return fallback;
    }
}
