using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation FX over original body materials.
/// Normal state keeps authoring materials untouched.
/// Possession = emission rim highlight; Hit = brief flash; Dissolve = temporary FX-shader swap only while fading.
/// </summary>
[DisallowMultipleComponent]
public class ActorVisualFx : MonoBehaviour
{
    public static readonly int CorpseFadeId = Shader.PropertyToID("_CorpseFade");
    public static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
    public static readonly int DissolveEdgeColorId = Shader.PropertyToID("_DissolveEdgeColor");
    public static readonly int DissolveEdgeIntensityId = Shader.PropertyToID("_DissolveEdgeIntensity");
    public static readonly int RimIntensityId = Shader.PropertyToID("_RimIntensity");
    public static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    public static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
    public static readonly int HitFlashAmountId = Shader.PropertyToID("_HitFlashAmount");
    public static readonly int HitFlashColorId = Shader.PropertyToID("_HitFlashColor");
    public static readonly int MainColorId = Shader.PropertyToID("_MainColor");
    public static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    public static readonly int ColorId = Shader.PropertyToID("_Color");
    public static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    public static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");

    [Header("Possession Highlight")]
    [Tooltip("Emission tint while possessed.")]
    public Color possessionRimColor = new Color(0.55f, 0.25f, 1f, 1f);
    [Tooltip("0 = identical to unpossessed look. Higher = brighter possession emission glow.")]
    [Range(0f, 8f)] public float possessionRimIntensity = 1.8f;

    [Header("Possessable Corpse Highlight")]
    [Tooltip("Emission tint for a corpse that is currently legal to possess. This uses a runtime material instance and never changes shared art materials.")]
    [ColorUsage(true, true)] public Color corpseRimColor = new Color(0.2f, 0.95f, 1.25f, 1f);
    [Tooltip("A restrained rim intensity so available corpses stay readable without competing with possessed bodies or Elites.")]
    [Range(0f, 8f)] public float corpseRimIntensity = 0.75f;

    [Header("Elite Highlight")]
    [Tooltip("HDR rim/emission tint for Elite bodies. Original renderer materials are cloned at runtime; shared assets stay unchanged.")]
    [ColorUsage(true, true)] public Color eliteRimColor = new Color(0.65f, 0.15f, 0.9f, 1f);
    [Tooltip("Base intensity of the Elite rim/emission pulse.")]
    [Range(0f, 8f)] public float eliteRimIntensity = 0.8f;
    [Tooltip("Elite rim pulse frequency in Hz.")]
    [Min(0f)] public float elitePulseSpeed = 1.8f;
    [Tooltip("Elite pulse amplitude as a fraction of the base intensity.")]
    [Range(0f, 1f)] public float elitePulseAmount = 0.25f;
    [Tooltip("Optional runtime material template for Elite bodies. Defaults to Possession/CharacterFX, which preserves the source albedo while adding a real Fresnel rim.")]
    public Material eliteMaterialTemplate;
    [Tooltip("Fresnel edge sharpness used by the Elite material.")]
    [Range(0.5f, 8f)] public float eliteRimPower = 3.0f;
    [Tooltip("Metallic contribution used by the fallback Elite material.")]
    [Range(0f, 1f)] public float eliteMetallic = 0.2f;

    [Header("Hit Flash")]
    public Color hitFlashColor = new Color(1f, 0.92f, 0.92f, 1f);
    [Min(0.01f)] public float hitFlashDuration = 0.12f;
    [Range(0f, 1f)] public float hitFlashPeak = 0.75f;

    [Header("Dissolve (corpse fade only)")]
    [Tooltip("HDR burn color on the dissolve hole frontier. Configure per monster for differentiation.")]
    [ColorUsage(true, true)]
    public Color dissolveEdgeColor = new Color(1.4f, 0.35f, 2.2f, 1f);
    [Tooltip("Strength of the dissolve edge glow.")]
    [Range(0f, 12f)] public float dissolveEdgeIntensity = 4.5f;
    [Tooltip("Template for dissolve-only temporary materials. Original materials are restored if fade is cancelled.")]
    public Material fxMaterialTemplate;

    private Renderer[] _renderers;
    private MaterialPropertyBlock _block;
    private readonly Dictionary<int, Color> _baseColors = new Dictionary<int, Color>();
    private readonly Dictionary<int, Color> _baseEmission = new Dictionary<int, Color>();
    private Material[][] _originalSharedMaterials;
    private bool _usingDissolveMaterials;
    private Material[][] _preHighlightSharedMaterials;
    private bool _usingHighlightInstances;
    private Coroutine _flashRoutine;
    private float _dissolve = 1f;
    private bool _possessionEnabled;
    private bool _corpseEnabled;
    private bool _eliteEnabled;
    private float _hitFlash;

    void Awake()
    {
        EnsureCache();
        CaptureLook();
        ClearPropertyBlocks();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Live-apply Inspector tweaks while possessed or while an Elite highlight is active.
        if (!Application.isPlaying || _block == null) return;
        if (!_possessionEnabled && !_corpseEnabled && !_eliteEnabled && !_usingHighlightInstances) return;
        ApplyFx();
    }
#endif

    void Update()
    {
        if (_eliteEnabled) ApplyFx();
    }

    public void RefreshRenderers()
    {
        RestoreOriginalMaterials();
        _renderers = GetComponentsInChildren<Renderer>(true);
        CaptureLook();
        ClearPropertyBlocks();
        ApplyFx();
    }

    /// <summary>1 = fully visible / normal, 0 = fully dissolved.</summary>
    public void SetDissolve(float visibleAmount)
    {
        float next = Mathf.Clamp01(visibleAmount);
        if (next < 0.999f)
            EnsureDissolveMaterials();
        else if (_usingDissolveMaterials)
            RestoreOriginalMaterials();

        _dissolve = next;
        ApplyFx();
    }

    public void SetPossessionHighlight(bool enabled)
    {
        _possessionEnabled = enabled;
        if (!enabled && !_corpseEnabled && !_eliteEnabled)
            RestoreHighlightMaterialInstances();
        ApplyFx();
    }

    public void SetCorpseHighlight(bool enabled)
    {
        _corpseEnabled = enabled;
        if (!enabled && !_possessionEnabled && !_eliteEnabled)
            RestoreHighlightMaterialInstances();
        ApplyFx();
    }

    public void SetEliteHighlight(bool enabled)
    {
        if (enabled && !_eliteEnabled && _usingHighlightInstances)
            RestoreHighlightMaterialInstances();
        _eliteEnabled = enabled;
        if (!enabled && !_possessionEnabled && !_corpseEnabled)
            RestoreHighlightMaterialInstances();
        ApplyFx();
    }

    /// <summary>
    /// Selects a restrained Elite palette from the monster's existing sin identity.
    /// The source albedo/normal textures are still copied into the runtime FX material.
    /// </summary>
    public void ConfigureEliteStyle(SinType sinType)
    {
        switch (sinType)
        {
            case SinType.Pride:
                eliteRimColor = new Color(1.0f, 0.45f, 0.06f, 1f);
                eliteRimIntensity = 0.8f;
                elitePulseSpeed = 1.7f;
                elitePulseAmount = 0.25f;
                eliteRimPower = 3.2f;
                eliteMetallic = 0.55f;
                break;
            case SinType.Envy:
                eliteRimColor = new Color(0.05f, 0.75f, 1.0f, 1f);
                eliteRimIntensity = 0.9f;
                elitePulseSpeed = 2.8f;
                elitePulseAmount = 0.3f;
                eliteRimPower = 3.4f;
                eliteMetallic = 0.35f;
                break;
            case SinType.Gluttony:
                eliteRimColor = new Color(1.0f, 0.12f, 0.02f, 1f);
                eliteRimIntensity = 0.75f;
                elitePulseSpeed = 1.15f;
                elitePulseAmount = 0.22f;
                eliteRimPower = 2.9f;
                eliteMetallic = 0.2f;
                break;
            case SinType.Greed:
                eliteRimColor = new Color(0.55f, 0.7f, 0.06f, 1f);
                eliteRimIntensity = 0.65f;
                elitePulseSpeed = 1.7f;
                elitePulseAmount = 0.18f;
                eliteRimPower = 3.8f;
                eliteMetallic = 0.4f;
                break;
            case SinType.Lust:
                eliteRimColor = new Color(1.0f, 0.04f, 0.45f, 1f);
                eliteRimIntensity = 0.9f;
                elitePulseSpeed = 2.45f;
                elitePulseAmount = 0.28f;
                eliteRimPower = 3.1f;
                eliteMetallic = 0.3f;
                break;
            case SinType.Sloth:
                eliteRimColor = new Color(0.06f, 0.35f, 0.8f, 1f);
                eliteRimIntensity = 0.55f;
                elitePulseSpeed = 0.7f;
                elitePulseAmount = 0.13f;
                eliteRimPower = 4.2f;
                eliteMetallic = 0.3f;
                break;
            case SinType.Wrath:
                eliteRimColor = new Color(1.0f, 0.04f, 0.01f, 1f);
                eliteRimIntensity = 1.0f;
                elitePulseSpeed = 3.2f;
                elitePulseAmount = 0.32f;
                eliteRimPower = 2.7f;
                eliteMetallic = 0.25f;
                break;
            default:
                break;
        }
    }

    private float GetPossessionRimIntensity()
    {
        float possessionIntensity = _possessionEnabled ? Mathf.Max(0f, possessionRimIntensity) : 0f;
        if (_eliteEnabled)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * Mathf.Max(0f, elitePulseSpeed))
                * Mathf.Clamp01(elitePulseAmount);
            float eliteIntensity = Mathf.Max(0f, eliteRimIntensity) * Mathf.Max(0f, pulse);
            return Mathf.Max(possessionIntensity, eliteIntensity);
        }

        return Mathf.Max(possessionIntensity, _corpseEnabled ? Mathf.Max(0f, corpseRimIntensity) : 0f);
    }

    private Color GetRimColor()
    {
        if (_eliteEnabled) return eliteRimColor;
        if (_possessionEnabled) return possessionRimColor;
        if (_corpseEnabled) return corpseRimColor;
        return possessionRimColor;
    }

    /// <summary>
    /// possessionRimIntensity == 0 must match the unpossessed look: no material swap, no emission keyword.
    /// Only create highlight instances when intensity is actually above zero.
    /// </summary>
    private void SyncPossessionHighlightMaterials(float rimIntensity)
    {
        if (_usingDissolveMaterials) return;

        bool wantHighlight = rimIntensity > 0.001f;
        if (wantHighlight)
            EnsureHighlightMaterialInstances();
        else if (_usingHighlightInstances)
            RestoreHighlightMaterialInstances();
    }

    public void PlayHitFlash()
    {
        if (!isActiveAndEnabled) return;
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, hitFlashDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Clamp01(elapsed / duration);
            _hitFlash = hitFlashPeak * (t * t * (3f - 2f * t));
            ApplyFx();
            yield return null;
        }

        _hitFlash = 0f;
        ApplyFx();
        _flashRoutine = null;
    }

    private void EnsureCache()
    {
        if (_block == null) _block = new MaterialPropertyBlock();
        if (_renderers == null || _renderers.Length == 0)
            _renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void CaptureLook()
    {
        _baseColors.Clear();
        _baseEmission.Clear();
        EnsureCache();
        for (int r = 0; r < _renderers.Length; r++)
        {
            Renderer renderer = _renderers[r];
            if (renderer == null || ShouldSkipRenderer(renderer)) continue;
            Material[] materials = renderer.sharedMaterials;
            for (int m = 0; m < materials.Length; m++)
            {
                Material mat = materials[m];
                if (mat == null) continue;
                int key = PackKey(r, m);
                if (mat.HasProperty(MainColorId))
                    _baseColors[key] = mat.GetColor(MainColorId);
                else if (mat.HasProperty(BaseColorId))
                    _baseColors[key] = mat.GetColor(BaseColorId);
                else if (mat.HasProperty(ColorId))
                    _baseColors[key] = mat.GetColor(ColorId);

                if (mat.HasProperty(EmissionColorId))
                    _baseEmission[key] = mat.GetColor(EmissionColorId);
                else
                    _baseEmission[key] = Color.black;
            }
        }
    }

    private void ClearPropertyBlocks()
    {
        EnsureCache();
        for (int r = 0; r < _renderers.Length; r++)
        {
            Renderer renderer = _renderers[r];
            if (renderer == null) continue;
            Material[] materials = renderer.sharedMaterials;
            for (int m = 0; m < materials.Length; m++)
                renderer.SetPropertyBlock(null, m);
        }
    }


    /// <summary>
    /// Temporary material instances with emission enabled so possession rim shows on URP Lit
    /// without permanently mutating shared art materials.
    /// </summary>
    private void EnsureHighlightMaterialInstances()
    {
        if (_usingHighlightInstances || _usingDissolveMaterials) return;
        EnsureCache();

        bool useEliteMaterial = _eliteEnabled;
        Shader eliteShader = null;
        if (useEliteMaterial)
        {
            eliteShader = eliteMaterialTemplate != null
                ? eliteMaterialTemplate.shader
                : Shader.Find("Possession/CharacterFX");
            useEliteMaterial = eliteShader != null;
        }

        _preHighlightSharedMaterials = new Material[_renderers.Length][];
        for (int r = 0; r < _renderers.Length; r++)
        {
            Renderer renderer = _renderers[r];
            if (renderer == null || ShouldSkipRenderer(renderer))
            {
                _preHighlightSharedMaterials[r] = null;
                continue;
            }

            Material[] shared = renderer.sharedMaterials;
            _preHighlightSharedMaterials[r] = shared;
            if (shared == null || shared.Length == 0) continue;

            Material[] instances = new Material[shared.Length];
            for (int m = 0; m < shared.Length; m++)
            {
                Material src = shared[m];
                if (src == null)
                {
                    instances[m] = null;
                    continue;
                }

                Material inst;
                if (useEliteMaterial)
                {
                    inst = eliteMaterialTemplate != null
                        ? new Material(eliteMaterialTemplate)
                        : new Material(eliteShader);
                    inst.name = src.name + "_EliteHighlight";
                    CopyLook(src, inst);
                    if (inst.HasProperty(RimColorId))
                        inst.SetColor(RimColorId, eliteRimColor);
                    if (inst.HasProperty(RimPowerId))
                        inst.SetFloat(RimPowerId, eliteRimPower);
                    if (inst.HasProperty("_Metallic"))
                        inst.SetFloat("_Metallic", eliteMetallic);
                }
                else
                {
                    inst = new Material(src);
                    inst.name = src.name + "_PossessHighlight";
                }

                if (!useEliteMaterial && inst.HasProperty(EmissionColorId))
                {
                    // Emission starts black; ApplyFx writes rimColor * intensity via MPB.
                    // Avoids revealing authored emission just by flipping the keyword on.
                    inst.EnableKeyword("_EMISSION");
                    inst.SetColor(EmissionColorId, Color.black);
                    inst.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                instances[m] = inst;
            }
            renderer.sharedMaterials = instances;
        }

        _usingHighlightInstances = true;
        CaptureLook();
    }

    private void RestoreHighlightMaterialInstances()
    {
        if (!_usingHighlightInstances || _preHighlightSharedMaterials == null) return;
        EnsureCache();
        for (int r = 0; r < _renderers.Length; r++)
        {
            Renderer renderer = _renderers[r];
            if (renderer == null || _preHighlightSharedMaterials[r] == null) continue;
            Material[] current = renderer.sharedMaterials;
            renderer.sharedMaterials = _preHighlightSharedMaterials[r];
            if (current != null)
            {
                for (int m = 0; m < current.Length; m++)
                {
                    if (current[m] != null &&
                        (current[m].name.EndsWith("_PossessHighlight") || current[m].name.EndsWith("_EliteHighlight")))
                        Destroy(current[m]);
                }
            }
        }
        _usingHighlightInstances = false;
        _preHighlightSharedMaterials = null;
        CaptureLook();
        ClearPropertyBlocks();
    }

    /// <summary>
    /// Swap to dissolve FX materials only while corpse is fading.
    /// Does not run in normal / possessed / hit states.
    /// </summary>
    private void EnsureDissolveMaterials()
    {
        if (_usingDissolveMaterials) return;
        // Dissolve owns materials; drop highlight instances first (originals are under them).
        if (_usingHighlightInstances)
            RestoreHighlightMaterialInstances();
        EnsureCache();

        Shader fxShader = fxMaterialTemplate != null ? fxMaterialTemplate.shader : null;
        if (fxShader == null) fxShader = Shader.Find("Possession/CharacterFX");
        if (fxShader == null) return;

        _originalSharedMaterials = new Material[_renderers.Length][];
        for (int r = 0; r < _renderers.Length; r++)
        {
            Renderer renderer = _renderers[r];
            if (renderer == null || ShouldSkipRenderer(renderer))
            {
                _originalSharedMaterials[r] = null;
                continue;
            }

            Material[] shared = renderer.sharedMaterials;
            _originalSharedMaterials[r] = shared;
            if (shared == null || shared.Length == 0) continue;

            Material[] dissolveMats = new Material[shared.Length];
            for (int m = 0; m < shared.Length; m++)
            {
                Material src = shared[m];
                if (src == null)
                {
                    dissolveMats[m] = null;
                    continue;
                }

                if (src.HasProperty(CorpseFadeId) && src.HasProperty(DissolveAmountId))
                {
                    dissolveMats[m] = src;
                    continue;
                }

                Material dst = fxMaterialTemplate != null
                    ? new Material(fxMaterialTemplate)
                    : new Material(fxShader);
                dst.name = src.name + "_DissolveFX";
                CopyLook(src, dst);
                dissolveMats[m] = dst;
            }

            renderer.sharedMaterials = dissolveMats;
        }

        _usingDissolveMaterials = true;
        CaptureLook();
    }

    private void RestoreOriginalMaterials()
    {
        if (_usingHighlightInstances)
            RestoreHighlightMaterialInstances();
        if (!_usingDissolveMaterials || _originalSharedMaterials == null)
            return;

        EnsureCache();
        for (int r = 0; r < _renderers.Length; r++)
        {
            Renderer renderer = _renderers[r];
            if (renderer == null || _originalSharedMaterials[r] == null) continue;
            renderer.sharedMaterials = _originalSharedMaterials[r];
        }

        _usingDissolveMaterials = false;
        _originalSharedMaterials = null;
        CaptureLook();
        ClearPropertyBlocks();
    }

    private static void CopyLook(Material src, Material dst)
    {
        if (src == null || dst == null) return;

        // Cartoon/Amplify mats often expose _Maintex while still declaring empty _BaseMap/_MainTex.
        Texture mainTex = null;
        string mainTexProperty = null;
        if (src.HasProperty("_BaseMap"))
        {
            mainTexProperty = "_BaseMap";
            mainTex = src.GetTexture(mainTexProperty);
        }
        if (mainTex == null && src.HasProperty("_MainTex"))
        {
            mainTexProperty = "_MainTex";
            mainTex = src.GetTexture(mainTexProperty);
        }
        if (mainTex == null && src.HasProperty("_Maintex"))
        {
            mainTexProperty = "_Maintex";
            mainTex = src.GetTexture(mainTexProperty);
        }
        if (mainTex == null && src.HasProperty("_BASE_COLOR_MAP"))
        {
            mainTexProperty = "_BASE_COLOR_MAP";
            mainTex = src.GetTexture(mainTexProperty);
        }
        if (mainTex != null && dst.HasProperty("_BaseMap"))
        {
            dst.SetTexture("_BaseMap", mainTex);
            if (mainTexProperty != null)
            {
                dst.SetTextureScale("_BaseMap", src.GetTextureScale(mainTexProperty));
                dst.SetTextureOffset("_BaseMap", src.GetTextureOffset(mainTexProperty));
            }
        }

        Color color = Color.white;
        if (src.HasProperty("_MainColor")) color = src.GetColor("_MainColor");
        else if (src.HasProperty(BaseColorId)) color = src.GetColor(BaseColorId);
        else if (src.HasProperty(ColorId)) color = src.GetColor(ColorId);
        else if (src.HasProperty("_BASE_COLOR")) color = src.GetColor("_BASE_COLOR");
        color = new Color(
            Mathf.Clamp01(color.r),
            Mathf.Clamp01(color.g),
            Mathf.Clamp01(color.b),
            Mathf.Clamp01(color.a));
        if (dst.HasProperty(BaseColorId)) dst.SetColor(BaseColorId, color);
        if (dst.HasProperty(ColorId)) dst.SetColor(ColorId, color);

        string bumpProperty = null;
        if (src.HasProperty("_BumpMap")) bumpProperty = "_BumpMap";
        else if (src.HasProperty("_BUMP_MAP")) bumpProperty = "_BUMP_MAP";
        if (bumpProperty != null && dst.HasProperty("_BumpMap"))
        {
            Texture bump = src.GetTexture(bumpProperty);
            if (bump != null) dst.SetTexture("_BumpMap", bump);
        }
        if (src.HasProperty("_BumpScale") && dst.HasProperty("_BumpScale"))
            dst.SetFloat("_BumpScale", src.GetFloat("_BumpScale"));
        else if (src.HasProperty("_BUMP_MAP_STRENGTH") && dst.HasProperty("_BumpScale"))
            dst.SetFloat("_BumpScale", src.GetFloat("_BUMP_MAP_STRENGTH"));

        if (dst.HasProperty(CorpseFadeId)) dst.SetFloat(CorpseFadeId, 1f);
        if (dst.HasProperty(DissolveAmountId)) dst.SetFloat(DissolveAmountId, 0f);
        if (dst.HasProperty(RimIntensityId)) dst.SetFloat(RimIntensityId, 0f);
        if (dst.HasProperty(HitFlashAmountId)) dst.SetFloat(HitFlashAmountId, 0f);
    }

    private void ApplyFx()
    {
        EnsureCache();
        // Always read Inspector fields live so prefab / Play Mode tweaks take effect immediately.
        float rimIntensity = GetPossessionRimIntensity();
        Color rimColor = rimIntensity > 0.001f ? GetRimColor() : Color.black;
        SyncPossessionHighlightMaterials(rimIntensity);

        bool anyFx = _dissolve < 0.999f || rimIntensity > 0.001f || _hitFlash > 0.001f;

        for (int r = 0; r < _renderers.Length; r++)
        {
            Renderer renderer = _renderers[r];
            if (renderer == null || ShouldSkipRenderer(renderer)) continue;
            Material[] materials = renderer.sharedMaterials;
            for (int m = 0; m < materials.Length; m++)
            {
                Material mat = materials[m];
                if (mat == null) continue;

                if (!anyFx)
                {
                    renderer.SetPropertyBlock(null, m);
                    continue;
                }

                renderer.GetPropertyBlock(_block, m);
                _block.Clear();

                // Dedicated dissolve shader path (only while fading).
                if (_usingDissolveMaterials)
                {
                    if (mat.HasProperty(CorpseFadeId))
                        _block.SetFloat(CorpseFadeId, _dissolve);
                    if (mat.HasProperty(DissolveAmountId))
                        _block.SetFloat(DissolveAmountId, 1f - _dissolve);
                    if (mat.HasProperty(DissolveEdgeColorId))
                        _block.SetColor(DissolveEdgeColorId, dissolveEdgeColor);
                    if (mat.HasProperty(DissolveEdgeIntensityId))
                        _block.SetFloat(DissolveEdgeIntensityId, dissolveEdgeIntensity);
                    if (mat.HasProperty(RimIntensityId))
                        _block.SetFloat(RimIntensityId, rimIntensity);
                    if (mat.HasProperty(RimColorId))
                        _block.SetColor(RimColorId, rimColor);
                    if (mat.HasProperty(HitFlashAmountId))
                        _block.SetFloat(HitFlashAmountId, _hitFlash);
                    if (mat.HasProperty(HitFlashColorId))
                        _block.SetColor(HitFlashColorId, hitFlashColor);
                }

                // Original materials: emission highlight / flash only. Never rewrite albedo in normal state.
                int key = PackKey(r, m);
                _baseEmission.TryGetValue(key, out Color baseEmission);

                bool canUseEmission = mat.HasProperty(EmissionColorId) &&
                    (_usingHighlightInstances || _usingDissolveMaterials || mat.IsKeywordEnabled("_EMISSION"));

                if (canUseEmission)
                {
                    // Possession and Elite glow are driven by their runtime rim intensities (0 = no added emission).
                    Color emission = baseEmission;
                    if (rimIntensity > 0.001f)
                        emission = baseEmission + rimColor * (rimIntensity * 0.55f);
                    if (_hitFlash > 0.001f)
                        emission += hitFlashColor * (_hitFlash * 3.5f);
                    _block.SetColor(EmissionColorId, emission);
                }

                // Custom body shaders may expose a Fresnel rim directly. Keep this path
                // property-driven so those shaders receive the same Elite pulse without
                // changing their shared material assets.
                if (mat.HasProperty(RimIntensityId))
                    _block.SetFloat(RimIntensityId, rimIntensity);
                if (mat.HasProperty(RimColorId))
                    _block.SetColor(RimColorId, rimColor);
                if (mat.HasProperty(RimPowerId))
                    _block.SetFloat(RimPowerId, eliteRimPower);

                // Hit flash on original materials: lerp albedo so it works without _EMISSION.
                if (_hitFlash > 0.001f && !_usingDissolveMaterials && _baseColors.TryGetValue(key, out Color baseColor))
                {
                    Color c = Color.Lerp(baseColor, hitFlashColor, _hitFlash);
                    if (mat.HasProperty(MainColorId)) _block.SetColor(MainColorId, c);
                    if (mat.HasProperty(BaseColorId)) _block.SetColor(BaseColorId, c);
                    if (mat.HasProperty(ColorId)) _block.SetColor(ColorId, c);
                }

                renderer.SetPropertyBlock(_block, m);
            }
        }
    }

    /// <summary>
    /// Skip particle systems and authored VFX meshes (e.g. pride headfire) so possession rim
    /// only hits body materials and remains controllable via possessionRimIntensity.
    /// </summary>
    private bool ShouldSkipRenderer(Renderer renderer)
    {
        if (renderer is ParticleSystemRenderer) return true;

        Transform t = renderer.transform;
        Transform stop = transform;
        while (t != null)
        {
            // Soul is parented under the possessed body; body hit-flash must not tint the soul.
            if (t.GetComponent<SoulActor>() != null && t != stop)
                return true;

            string n = t.name;
            if (n.IndexOf("headfire", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("VFX", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (t == stop) break;
            t = t.parent;
        }
        return false;
    }

    private static int PackKey(int rendererIndex, int materialIndex)
    {
        return (rendererIndex << 8) ^ materialIndex;
    }

    void OnDisable()
    {
        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }
        RestoreOriginalMaterials();
        ClearPropertyBlocks();
    }
}
