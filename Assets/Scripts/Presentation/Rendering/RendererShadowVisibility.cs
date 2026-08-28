using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Turns shadow casting off while a dynamic renderer is outside every active camera.
/// The authored shadow mode is restored as soon as the renderer becomes visible again.
/// </summary>
[DisallowMultipleComponent]
public sealed class RendererShadowVisibility : MonoBehaviour
{
    private static Material sharedTransientLineMaterial;
    // Unity objects must not be created from a MonoBehaviour type initializer. Create the
    // block on first runtime use instead (SetSharedColor is only called after scene startup).
    private static MaterialPropertyBlock sharedColorBlock;

    private Renderer targetRenderer;
    private ShadowCastingMode authoredShadowMode;
    private bool authoredReceiveShadows;
    private bool initialized;
    private bool optimizationEnabled = true;

    public static void Ensure(Renderer renderer)
    {
        if (renderer == null) return;
        EnableGpuInstancing(renderer);
        RendererShadowVisibility guard = renderer.GetComponent<RendererShadowVisibility>();
        if (guard == null) guard = renderer.gameObject.AddComponent<RendererShadowVisibility>();
        guard.Initialize(renderer);
        guard.SetOptimizationEnabled(true);
    }

    /// <summary>Enables instancing on shared materials without touching Renderer.material.</summary>
    public static void EnableGpuInstancing(Renderer renderer)
    {
        if (renderer == null || !SystemInfo.supportsInstancing || !GameManager.GpuInstancingEnabled) return;
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null || material.enableInstancing) continue;
            material.enableInstancing = true;
        }
    }

    /// <summary>
    /// Returns one shared unlit line material for transient arcs/telegraphs. Callers set
    /// per-renderer color through <see cref="SetSharedColor"/> instead of cloning a material.
    /// </summary>
    public static Material GetSharedTransientLineMaterial()
    {
        if (sharedTransientLineMaterial != null) return sharedTransientLineMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        sharedTransientLineMaterial = new Material(shader)
        {
            name = "SharedTransientLineMaterial",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = 3001
        };
        SetFloatIfPresent(sharedTransientLineMaterial, "_Surface", 1f);
        SetFloatIfPresent(sharedTransientLineMaterial, "_Blend", 0f);
        SetFloatIfPresent(sharedTransientLineMaterial, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloatIfPresent(sharedTransientLineMaterial, "_DstBlend", (float)BlendMode.One);
        SetFloatIfPresent(sharedTransientLineMaterial, "_ZWrite", 0f);
        SetFloatIfPresent(sharedTransientLineMaterial, "_Cull", 0f);
        if (SystemInfo.supportsInstancing) sharedTransientLineMaterial.enableInstancing = true;
        return sharedTransientLineMaterial;
    }

    /// <summary>Sets a per-renderer color while retaining the shared material instance.</summary>
    public static void SetSharedColor(Renderer renderer, Color color)
    {
        if (renderer == null) return;
        Material material = renderer.sharedMaterial;
        if (material == null) return;

        if (sharedColorBlock == null) sharedColorBlock = new MaterialPropertyBlock();
        sharedColorBlock.Clear();
        renderer.GetPropertyBlock(sharedColorBlock);
        if (material.HasProperty("_BaseColor")) sharedColorBlock.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) sharedColorBlock.SetColor("_Color", color);
        renderer.SetPropertyBlock(sharedColorBlock);
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName)) material.SetFloat(propertyName, value);
    }

    private void Awake()
    {
        Initialize(GetComponent<Renderer>());
    }

    private void OnEnable()
    {
        if (!initialized) Initialize(GetComponent<Renderer>());
        ApplyVisibility(targetRenderer != null && targetRenderer.isVisible);
    }

    private void OnBecameVisible()
    {
        ApplyVisibility(true);
    }

    private void OnBecameInvisible()
    {
        ApplyVisibility(false);
    }

    private void Initialize(Renderer renderer)
    {
        if (initialized || renderer == null) return;
        targetRenderer = renderer;
        authoredShadowMode = renderer.shadowCastingMode;
        authoredReceiveShadows = renderer.receiveShadows;
        initialized = true;
    }

    private void ApplyVisibility(bool visible)
    {
        if (!initialized || targetRenderer == null) return;
        if (!optimizationEnabled || !GameManager.DynamicShadowVisibilityEnabled)
        {
            targetRenderer.shadowCastingMode = authoredShadowMode;
            targetRenderer.receiveShadows = authoredReceiveShadows;
            return;
        }
        if (authoredShadowMode == ShadowCastingMode.Off) return;

        targetRenderer.shadowCastingMode = visible ? authoredShadowMode : ShadowCastingMode.Off;
        targetRenderer.receiveShadows = visible && authoredReceiveShadows;
    }

    /// <summary>
    /// Enables or disables the off-screen shadow policy without losing authored renderer
    /// settings. GameManager calls this when a global rendering switch is changed or when
    /// a pooled object is reused.
    /// </summary>
    public void SetOptimizationEnabled(bool enabled)
    {
        optimizationEnabled = enabled;
        ApplyVisibility(targetRenderer != null && targetRenderer.isVisible);
    }
}
