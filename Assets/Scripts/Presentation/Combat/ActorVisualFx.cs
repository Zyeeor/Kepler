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
        // Live-apply Inspector tweaks while possessed (Play Mode).
        if (!Application.isPlaying || _block == null) return;
        if (!_possessionEnabled && !_usingHighlightInstances) return;
        ApplyFx();
    }
#endif

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
        if (!enabled)
            RestoreHighlightMaterialInstances();
        ApplyFx();
    }

    private float GetPossessionRimIntensity()
    {
        return _possessionEnabled ? Mathf.Max(0f, possessionRimIntensity) : 0f;
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

                Material inst = new Material(src);
                inst.name = src.name + "_PossessHighlight";
                if (inst.HasProperty(EmissionColorId))
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
                    if (current[m] != null && current[m].name.EndsWith("_PossessHighlight"))
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
        if (src.HasProperty("_BaseMap")) mainTex = src.GetTexture("_BaseMap");
        if (mainTex == null && src.HasProperty("_MainTex")) mainTex = src.GetTexture("_MainTex");
        if (mainTex == null && src.HasProperty("_Maintex")) mainTex = src.GetTexture("_Maintex");
        if (mainTex != null && dst.HasProperty("_BaseMap"))
            dst.SetTexture("_BaseMap", mainTex);

        Color color = Color.white;
        if (src.HasProperty("_MainColor")) color = src.GetColor("_MainColor");
        else if (src.HasProperty(BaseColorId)) color = src.GetColor(BaseColorId);
        else if (src.HasProperty(ColorId)) color = src.GetColor(ColorId);
        if (dst.HasProperty(BaseColorId)) dst.SetColor(BaseColorId, color);
        if (dst.HasProperty(ColorId)) dst.SetColor(ColorId, color);

        if (src.HasProperty("_BumpMap") && dst.HasProperty("_BumpMap"))
        {
            Texture bump = src.GetTexture("_BumpMap");
            if (bump != null) dst.SetTexture("_BumpMap", bump);
        }

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
        Color rimColor = rimIntensity > 0.001f ? possessionRimColor : Color.black;
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
                    // Possession glow is driven only by possessionRimIntensity (0 = no added emission).
                    Color emission = baseEmission;
                    if (rimIntensity > 0.001f)
                        emission = baseEmission + rimColor * (rimIntensity * 0.55f);
                    if (_hitFlash > 0.001f)
                        emission += hitFlashColor * (_hitFlash * 3.5f);
                    _block.SetColor(EmissionColorId, emission);
                }

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
