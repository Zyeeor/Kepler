using System.Collections;
using UnityEngine;

/// <summary>Boss-only visual state driver using MaterialPropertyBlock; shared materials remain untouched.</summary>
public sealed class BossSpatialDistortionController : MonoBehaviour
{
    static readonly int DistortionStrength = Shader.PropertyToID("_DistortionStrength");
    static readonly int VertexWarp = Shader.PropertyToID("_VertexWarp");
    static readonly int RimPulse = Shader.PropertyToID("_RimPulse");
    static readonly int DissolveAmount = Shader.PropertyToID("_DissolveAmount");
    static readonly int ChromaticSplit = Shader.PropertyToID("_ChromaticSplit");
    public float idleAmplitude = 0.18f;
    public float idleFrequency = 1.1f;
    public float distortionStrength = 0.25f;

    Renderer[] renderers;
    MaterialPropertyBlock block;
    Vector3 baseLocalPosition;
    float teleportDissolve;
    float teleportChromatic;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        block = new MaterialPropertyBlock();
        baseLocalPosition = transform.localPosition;
    }

    void LateUpdate()
    {
        if (renderers == null || block == null) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * idleFrequency * Mathf.PI * 2f);
        transform.localPosition = baseLocalPosition + Vector3.up
            * (Mathf.Sin(Time.unscaledTime * idleFrequency * Mathf.PI * 2f) * idleAmplitude);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            renderer.GetPropertyBlock(block);
            block.SetFloat(DistortionStrength, distortionStrength);
            block.SetFloat(VertexWarp, pulse * distortionStrength);
            block.SetFloat(RimPulse, pulse);
            block.SetFloat(DissolveAmount, teleportDissolve);
            block.SetFloat(ChromaticSplit, teleportChromatic);
            renderer.SetPropertyBlock(block);
        }
    }

    public IEnumerator PlayTeleport(Vector3 destination)
    {
        const float phaseDuration = 0.22f;
        float elapsed = 0f;
        while (elapsed < phaseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / phaseDuration);
            teleportDissolve = t;
            teleportChromatic = t;
            yield return null;
        }

        transform.position = destination;
        baseLocalPosition = transform.localPosition;
        elapsed = 0f;
        while (elapsed < phaseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Clamp01(elapsed / phaseDuration);
            teleportDissolve = t;
            teleportChromatic = t;
            yield return null;
        }
        teleportDissolve = 0f;
        teleportChromatic = 0f;
    }
}
