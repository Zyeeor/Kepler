using System.Collections;
using UnityEngine;

/// <summary>
/// Reusable arrival-only part of the Boss Void Walk presentation.
/// It drives the same material properties as BossSpatialDistortionController, while using
/// the actor's existing ActorVisualFx dissolve material for the body so Elite art materials
/// are never replaced by a shared asset.
/// </summary>
[DisallowMultipleComponent]
public sealed class VoidWalkArrivalVisualFx : MonoBehaviour
{
    [Header("Arrival")]
    [Tooltip("Time used to reform the Elite from the Void Walk landing effect.")]
    [Min(0.01f)] public float arrivalDuration = 0.85f;
    [Tooltip("Strength of the temporary vertex and chromatic distortion on shaders that expose the Boss Void Walk properties.")]
    [Range(0f, 1f)] public float distortionStrength = 0.25f;
    [Tooltip("Lifetime of the landing rift copied from the Boss Void Walk effect.")]
    [Min(0.05f)] public float arrivalRiftLifetime = 1.05f;
    [Tooltip("World-space size of the landing rift. Kept below the Boss rift scale so an Elite never creates a screen-filling flash.")]
    [Min(0.1f)] public float arrivalRiftScale = 2.4f;

    static readonly int SinChannel = Shader.PropertyToID("_SinChannel");

    Renderer[] renderers;
    MaterialPropertyBlock block;
    ActorVisualFx actorVisualFx;
    Renderer riftRenderer;
    Transform riftTransform;
    Material riftMaterial;
    Coroutine arrivalRoutine;
    float materialDissolve;
    float materialChromatic;
    float riftExpiresAt;
    bool riftShrinking;
    bool playing;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        block = new MaterialPropertyBlock();
        actorVisualFx = GetComponent<ActorVisualFx>();
        CreateLandingRift();
    }

    void OnDisable()
    {
        if (arrivalRoutine != null)
        {
            StopCoroutine(arrivalRoutine);
            arrivalRoutine = null;
        }
        playing = false;
        HideLandingRift();
        ResetBodyMaterial();
        ResetMaterialProperties();
    }

    void OnDestroy()
    {
        if (riftTransform != null) Destroy(riftTransform.gameObject);
        if (riftMaterial != null) Destroy(riftMaterial);
    }

    void LateUpdate()
    {
        UpdateMaterialProperties();
        UpdateLandingRift();
    }

    /// <summary>Starts the arrival phase; repeated calls replace an unfinished phase cleanly.</summary>
    public void PlayArrival()
    {
        if (!isActiveAndEnabled) return;
        if (arrivalRoutine != null) StopCoroutine(arrivalRoutine);
        ResetBodyMaterial();
        materialDissolve = 1f;
        materialChromatic = 1f;
        playing = true;
        ConfigureRiftChannel();
        ActivateLandingRift();
        arrivalRoutine = StartCoroutine(ArrivalRoutine());
    }

    IEnumerator ArrivalRoutine()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, arrivalDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Match BossSpatialDistortionController's landing direction: invisible inside
            // the rift first, then reform into the normal actor material.
            materialDissolve = 1f - t;
            materialChromatic = 1f - t;
            if (actorVisualFx != null) actorVisualFx.SetDissolve(t);
            yield return null;
        }

        ResetBodyMaterial();
        materialDissolve = 0f;
        materialChromatic = 0f;
        playing = false;
        HideLandingRift();
        arrivalRoutine = null;
    }

    void UpdateMaterialProperties()
    {
        if (!playing || renderers == null || block == null) return;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI * 1.1f);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            renderer.GetPropertyBlock(block);
            block.SetFloat(BossSpatialDistortionController.DistortionStrengthId, distortionStrength);
            block.SetFloat(BossSpatialDistortionController.VertexWarpId, pulse * distortionStrength);
            block.SetFloat(BossSpatialDistortionController.RimPulseId, pulse);
            block.SetFloat(BossSpatialDistortionController.DissolveAmountId, materialDissolve);
            block.SetFloat(BossSpatialDistortionController.ChromaticSplitId, materialChromatic);
            renderer.SetPropertyBlock(block);
        }
    }

    void CreateLandingRift()
    {
        Shader shader = Shader.Find("Possession/BossSevenfoldDistortion");
        if (shader == null) return;

        riftMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
        riftMaterial.SetColor("_BaseColor", new Color(0.008f, 0.001f, 0.02f, 1f));
        riftMaterial.SetColor("_RimColor", new Color(1.1f, 0.08f, 2.4f, 1f));
        riftMaterial.SetFloat("_RimPower", 1.1f);

        GameObject rift = GameObject.CreatePrimitive(PrimitiveType.Quad);
        rift.name = "Elite Void Walk Arrival Rift";
        rift.layer = 2; // Ignore Raycast: presentation only.
        Collider collider = rift.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        riftTransform = rift.transform;
        riftTransform.SetParent(transform, false);
        riftTransform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        riftRenderer = rift.GetComponent<Renderer>();
        riftRenderer.sharedMaterial = riftMaterial;
        rift.SetActive(false);
    }

    void ConfigureRiftChannel()
    {
        if (riftMaterial == null) return;
        MonsterActor monster = GetComponent<MonsterActor>();
        float channel = monster != null ? Mathf.Clamp((int)monster.sinType - 1, 0f, 6f) : 0f;
        if (riftMaterial.HasProperty(SinChannel)) riftMaterial.SetFloat(SinChannel, channel);
    }

    void ActivateLandingRift()
    {
        if (riftTransform == null) return;
        riftTransform.SetParent(null, true);
        riftTransform.SetPositionAndRotation(transform.position + Vector3.up * 0.04f,
            Quaternion.Euler(-90f, 0f, 0f));
        riftTransform.localScale = Vector3.one * Mathf.Max(0.1f, arrivalRiftScale);
        riftExpiresAt = Time.unscaledTime + Mathf.Max(0.05f, arrivalRiftLifetime);
        riftShrinking = true;
        riftTransform.gameObject.SetActive(true);
    }

    void UpdateLandingRift()
    {
        if (riftTransform == null || riftRenderer == null || !riftTransform.gameObject.activeSelf) return;
        float remaining = riftExpiresAt - Time.unscaledTime;
        if (remaining <= 0f)
        {
            HideLandingRift();
            return;
        }

        float pulse = 0.8f + Mathf.Sin(Time.unscaledTime * 14f) * 0.2f;
        float scale = riftShrinking
            ? Mathf.Clamp01(remaining / 0.18f)
            : 1f;
        riftTransform.localScale = Vector3.one * (Mathf.Max(0.1f, arrivalRiftScale) * pulse * scale);
        riftRenderer.GetPropertyBlock(block);
        block.SetFloat(BossSpatialDistortionController.PortalPulseId, 1f);
        block.SetFloat(BossSpatialDistortionController.RimPulseId, 1.4f + pulse * 1.6f);
        block.SetFloat(BossSpatialDistortionController.ChromaticSplitId, 0.06f * pulse);
        block.SetFloat(BossSpatialDistortionController.DissolveAmountId, 0f);
        riftRenderer.SetPropertyBlock(block);
    }

    void HideLandingRift()
    {
        if (riftTransform != null) riftTransform.gameObject.SetActive(false);
    }

    void ResetBodyMaterial()
    {
        if (actorVisualFx != null) actorVisualFx.SetDissolve(1f);
    }

    void ResetMaterialProperties()
    {
        if (renderers == null || block == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            renderer.GetPropertyBlock(block);
            block.SetFloat(BossSpatialDistortionController.DistortionStrengthId, 0f);
            block.SetFloat(BossSpatialDistortionController.VertexWarpId, 0f);
            block.SetFloat(BossSpatialDistortionController.RimPulseId, 0f);
            block.SetFloat(BossSpatialDistortionController.DissolveAmountId, 0f);
            block.SetFloat(BossSpatialDistortionController.ChromaticSplitId, 0f);
            renderer.SetPropertyBlock(block);
        }
    }
}
