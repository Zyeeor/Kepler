using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Captures authored rendering settings once and applies the runtime performance policy
/// selected on GameManager. The component is attached to pooled roots and monster roots,
/// so a reuse path does not repeatedly scan the hierarchy or create material instances.
/// </summary>
[DisallowMultipleComponent]
public sealed class RenderingOptimizationState : MonoBehaviour
{
    private struct RendererSnapshot
    {
        public Renderer renderer;
        public ShadowCastingMode shadowCastingMode;
        public bool receiveShadows;
    }

    private struct TrailSnapshot
    {
        public TrailRenderer renderer;
        public float minVertexDistance;
        public float time;
    }

    private struct AnimatorSnapshot
    {
        public Animator animator;
        public AnimatorCullingMode cullingMode;
    }

    private struct LightSnapshot
    {
        public Light light;
        public bool enabled;
        public LightShadows shadows;
    }

    private RendererSnapshot[] rendererSnapshots;
    private TrailSnapshot[] trailSnapshots;
    private AnimatorSnapshot[] animatorSnapshots;
    private LightSnapshot[] lightSnapshots;
    private bool initialized;
    private bool containsMonster;
    private bool containsParticles;
    private bool isStaticChunk;
    private bool isTransientVisual;

    public static void ApplyTo(GameObject root)
    {
        if (root == null) return;

        RenderingOptimizationState state = root.GetComponent<RenderingOptimizationState>();
        if (state == null) state = root.AddComponent<RenderingOptimizationState>();
        state.InitializeIfNeeded();
        state.ApplySettings();
    }

    private void InitializeIfNeeded()
    {
        if (initialized) return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        rendererSnapshots = new RendererSnapshot[renderers.Length];
        trailSnapshots = new TrailSnapshot[CountTrails(renderers)];
        int trailIndex = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            rendererSnapshots[i] = new RendererSnapshot
            {
                renderer = renderer,
                shadowCastingMode = renderer != null ? renderer.shadowCastingMode : ShadowCastingMode.Off,
                receiveShadows = renderer != null && renderer.receiveShadows
            };

            TrailRenderer trail = renderer as TrailRenderer;
            if (trail != null)
            {
                trailSnapshots[trailIndex++] = new TrailSnapshot
                {
                    renderer = trail,
                    minVertexDistance = trail.minVertexDistance,
                    time = trail.time
                };
            }
        }

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        animatorSnapshots = new AnimatorSnapshot[animators.Length];
        for (int i = 0; i < animators.Length; i++)
        {
            animatorSnapshots[i] = new AnimatorSnapshot
            {
                animator = animators[i],
                cullingMode = animators[i] != null
                    ? animators[i].cullingMode
                    : AnimatorCullingMode.AlwaysAnimate
            };
        }

        Light[] lights = GetComponentsInChildren<Light>(true);
        lightSnapshots = new LightSnapshot[lights.Length];
        for (int i = 0; i < lights.Length; i++)
        {
            lightSnapshots[i] = new LightSnapshot
            {
                light = lights[i],
                enabled = lights[i] != null && lights[i].enabled,
                shadows = lights[i] != null ? lights[i].shadows : LightShadows.None
            };
        }

        containsMonster = GetComponentInChildren<MonsterActor>(true) != null;
        containsParticles = GetComponentInChildren<ParticleSystem>(true) != null;
        isStaticChunk = gameObject.name.StartsWith("ChunkVisual_", System.StringComparison.OrdinalIgnoreCase);
        isTransientVisual = GetComponent<PooledObject>() != null
            || GetComponent<DestroyOnOwnerDeath>() != null
            || gameObject.name.IndexOf("vfx", System.StringComparison.OrdinalIgnoreCase) >= 0
            || gameObject.name.IndexOf("effect", System.StringComparison.OrdinalIgnoreCase) >= 0
            || gameObject.name.IndexOf("telegraph", System.StringComparison.OrdinalIgnoreCase) >= 0
            || gameObject.name.IndexOf("projectile", System.StringComparison.OrdinalIgnoreCase) >= 0;
        initialized = true;
    }

    private static int CountTrails(Renderer[] renderers)
    {
        int count = 0;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] is TrailRenderer) count++;
        return count;
    }

    private void ApplySettings()
    {
        if (!initialized) return;

        bool optimizeShadows = GameManager.ShadowOptimizationEnabled;
        bool dynamicShadowVisibility = GameManager.DynamicShadowVisibilityEnabled;
        bool optimizeTrails = GameManager.TrailOptimizationEnabled;
        bool optimizeAnimators = containsMonster && GameManager.AnimatorCullingEnabled;
        bool optimizeLights = (containsMonster || containsParticles) && GameManager.ImportedLightOptimizationEnabled;
        bool enableInstancing = GameManager.GpuInstancingEnabled;

        for (int i = 0; i < rendererSnapshots.Length; i++)
        {
            RendererSnapshot snapshot = rendererSnapshots[i];
            Renderer renderer = snapshot.renderer;
            if (renderer == null) continue;

            if (enableInstancing)
                RendererShadowVisibility.EnableGpuInstancing(renderer);

            bool effectRenderer = IsEffectRenderer(renderer);
            RendererShadowVisibility guard = renderer.GetComponent<RendererShadowVisibility>();
            if (!optimizeShadows)
            {
                if (guard != null) guard.SetOptimizationEnabled(false);
                renderer.shadowCastingMode = snapshot.shadowCastingMode;
                renderer.receiveShadows = snapshot.receiveShadows;
            }
            else if (effectRenderer || isTransientVisual || (containsParticles && !containsMonster && !isStaticChunk))
            {
                if (guard != null) guard.SetOptimizationEnabled(false);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            else if (dynamicShadowVisibility)
            {
                RendererShadowVisibility.Ensure(renderer);
            }
            else
            {
                if (guard != null) guard.SetOptimizationEnabled(false);
                renderer.shadowCastingMode = snapshot.shadowCastingMode;
                renderer.receiveShadows = snapshot.receiveShadows;
            }
        }

        for (int i = 0; i < trailSnapshots.Length; i++)
        {
            TrailSnapshot snapshot = trailSnapshots[i];
            TrailRenderer trail = snapshot.renderer;
            if (trail == null) continue;

            if (!optimizeTrails)
            {
                trail.minVertexDistance = snapshot.minVertexDistance;
                trail.time = snapshot.time;
            }
            else
            {
                float configuredDistance = Mathf.Max(0f, GameManager.OptimizedTrailMinVertexDistance);
                if (configuredDistance > 0f)
                    trail.minVertexDistance = Mathf.Max(snapshot.minVertexDistance, configuredDistance);

                float configuredTime = Mathf.Max(0f, GameManager.OptimizedTrailTime);
                if (snapshot.time > 0f && configuredTime > 0f)
                    trail.time = Mathf.Min(snapshot.time, configuredTime);
            }
        }

        for (int i = 0; i < animatorSnapshots.Length; i++)
        {
            AnimatorSnapshot snapshot = animatorSnapshots[i];
            if (snapshot.animator == null) continue;
            snapshot.animator.cullingMode = optimizeAnimators
                ? GameManager.OptimizedAnimatorCullingMode
                : snapshot.cullingMode;
        }

        for (int i = 0; i < lightSnapshots.Length; i++)
        {
            LightSnapshot snapshot = lightSnapshots[i];
            Light light = snapshot.light;
            if (light == null) continue;

            if (optimizeLights && light.type != LightType.Directional)
            {
                light.enabled = false;
                light.shadows = LightShadows.None;
            }
            else
            {
                light.enabled = snapshot.enabled;
                light.shadows = snapshot.shadows;
            }
        }
    }

    private static bool IsEffectRenderer(Renderer renderer)
    {
        if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
            return true;

        Transform current = renderer.transform;
        while (current != null)
        {
            if (current.GetComponent<EnemyAbility>() != null)
                return true;

            string objectName = current.name;
            if (objectName.IndexOf("vfx", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("effect", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("telegraph", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("projectile", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("trail", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (current == renderer.transform.root) break;
            current = current.parent;
        }

        return false;
    }
}
