using System;
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
    static readonly int PortalPulse = Shader.PropertyToID("_PortalPulse");

    // Shared by arrival-only consumers so the Elite presentation uses the exact same
    // material contract as Boss Void Walk without duplicating shader property definitions.
    public static readonly int DistortionStrengthId = DistortionStrength;
    public static readonly int VertexWarpId = VertexWarp;
    public static readonly int RimPulseId = RimPulse;
    public static readonly int DissolveAmountId = DissolveAmount;
    public static readonly int ChromaticSplitId = ChromaticSplit;
    public static readonly int PortalPulseId = PortalPulse;
    public float idleAmplitude = 0.18f;
    public float idleFrequency = 1.1f;
    public float distortionStrength = 0.25f;
    public float teleportOutDuration = 0.75f;
    public float teleportInDuration = 0.85f;
    public float hitboxRestoreDelay = 0.60f;
    public float departureRiftLifetime = 8f;

    Renderer[] renderers;
    MaterialPropertyBlock block;
    ActorVisualFx actorVisualFx;
    Transform visualRoot;
    Vector3 baseVisualLocalPosition;
    float teleportDissolve;
    float teleportChromatic;
    PortalSlot[] portals;
    PortalSlot[] auraShards;
    Material portalMaterial;

    sealed class PortalSlot
    {
        public Transform transform;
        public Renderer renderer;
        public float expiresAt;
        public float baseScale;
        public bool shrinking;
    }

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        block = new MaterialPropertyBlock();
        actorVisualFx = GetComponent<ActorVisualFx>();
        visualRoot = transform.Find("VisualRoot");
        if (visualRoot == null) visualRoot = transform;
        baseVisualLocalPosition = visualRoot.localPosition;
        CreatePortals();
    }

    void OnDestroy()
    {
        ClearRifts(destroy: true);
        ClearAura(destroy: true);
        if (portalMaterial != null) Destroy(portalMaterial);
    }

    void LateUpdate()
    {
        if (renderers == null || block == null) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * idleFrequency * Mathf.PI * 2f);
        if (visualRoot != null)
            visualRoot.localPosition = baseVisualLocalPosition + Vector3.up
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
        UpdatePortals();
        UpdateAura();
    }

    public IEnumerator PlayTeleport(Vector3 destination, Action onHitboxRestore)
    {
        ActivatePortal(0, transform.position, departureRiftLifetime, false);
        float elapsed = 0f;
        float outDuration = Mathf.Max(0.01f, teleportOutDuration);
        while (elapsed < outDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / outDuration);
            teleportDissolve = t;
            teleportChromatic = t;
            if (actorVisualFx != null) actorVisualFx.SetDissolve(1f - t);
            yield return null;
        }

        transform.position = destination;
        baseVisualLocalPosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
        ActivatePortal(1, destination, teleportInDuration + 0.1f, true);
        elapsed = 0f;
        bool restored = false;
        float inDuration = Mathf.Max(0.01f, teleportInDuration);
        while (elapsed < inDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // Arrival is the inverse of departure: begin fully dissolved inside the
            // destination rift, then reform into a solid Boss over the recovery.
            float t = Mathf.Clamp01(elapsed / inDuration);
            teleportDissolve = 1f - t;
            teleportChromatic = 1f - t;
            if (actorVisualFx != null) actorVisualFx.SetDissolve(t);
            if (!restored && elapsed >= hitboxRestoreDelay)
            {
                restored = true;
                onHitboxRestore?.Invoke();
            }
            yield return null;
        }
        if (!restored) onHitboxRestore?.Invoke();
        teleportDissolve = 0f;
        teleportChromatic = 0f;
        if (actorVisualFx != null) actorVisualFx.SetDissolve(1f);
    }

    public void ClearRifts(bool destroy = false)
    {
        if (portals == null) return;
        for (int i = 0; i < portals.Length; i++)
        {
            PortalSlot portal = portals[i];
            if (portal == null || portal.transform == null) continue;
            if (destroy) Destroy(portal.transform.gameObject);
            else portal.transform.gameObject.SetActive(false);
        }
    }

    void CreatePortals()
    {
        Shader shader = Shader.Find("Possession/BossSevenfoldDistortion");
        if (shader == null) return;

        portalMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
        portalMaterial.SetColor("_BaseColor", new Color(0.008f, 0.001f, 0.02f, 1f));
        portalMaterial.SetColor("_RimColor", new Color(1.8f, 0.08f, 3.2f, 1f));
        portalMaterial.SetFloat("_RimPower", 1.1f);
        portals = new PortalSlot[2];
        for (int i = 0; i < portals.Length; i++)
        {
            GameObject portal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            portal.name = "Sevenfold Void Rift " + (i + 1);
            portal.layer = 2; // Ignore Raycast: it can never invalidate a teleport plan.
            Collider collider = portal.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            portal.transform.SetParent(transform, false);
            portal.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            Renderer renderer = portal.GetComponent<Renderer>();
            renderer.sharedMaterial = portalMaterial;
            portal.SetActive(false);
            portals[i] = new PortalSlot { transform = portal.transform, renderer = renderer };
        }
        CreateAura();
    }

    void CreateAura()
    {
        Transform parent = visualRoot != null ? visualRoot : transform;
        auraShards = new PortalSlot[6];
        for (int i = 0; i < auraShards.Length; i++)
        {
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Quad);
            shard.name = "Sevenfold Dark Aura " + (i + 1);
            shard.layer = 2;
            Collider collider = shard.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            shard.transform.SetParent(parent, false);
            shard.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            Renderer renderer = shard.GetComponent<Renderer>();
            renderer.sharedMaterial = portalMaterial;
            auraShards[i] = new PortalSlot { transform = shard.transform, renderer = renderer, baseScale = 0.7f + (i % 2) * 0.16f };
        }
    }

    void ActivatePortal(int index, Vector3 position, float lifetime, bool shrinking)
    {
        if (portals == null || index < 0 || index >= portals.Length || portals[index] == null) return;
        PortalSlot portal = portals[index];
        portal.transform.SetParent(null, true);
        portal.transform.SetPositionAndRotation(position + Vector3.up * 0.04f, Quaternion.Euler(-90f, 0f, 0f));
        portal.baseScale = shrinking ? 4.8f : 4.2f;
        portal.expiresAt = Time.unscaledTime + lifetime;
        portal.shrinking = shrinking;
        portal.transform.gameObject.SetActive(true);
    }

    void UpdatePortals()
    {
        if (portals == null) return;
        for (int i = 0; i < portals.Length; i++)
        {
            PortalSlot portal = portals[i];
            if (portal == null || portal.transform == null || !portal.transform.gameObject.activeSelf) continue;
            float remaining = portal.expiresAt - Time.unscaledTime;
            if (remaining <= 0f)
            {
                portal.transform.gameObject.SetActive(false);
                continue;
            }

            float pulse = 0.8f + Mathf.Sin(Time.unscaledTime * 14f + i * 1.7f) * 0.2f;
            float scale = portal.shrinking ? Mathf.Clamp01(remaining / 0.18f) : 1f;
            portal.transform.localScale = Vector3.one * (portal.baseScale * pulse * scale);
            portal.renderer.GetPropertyBlock(block);
            block.SetFloat(PortalPulse, 1f);
            block.SetFloat(RimPulse, 1.4f + pulse * 1.6f);
            block.SetFloat(ChromaticSplit, 0.06f * pulse);
            block.SetFloat(DissolveAmount, 0f);
            portal.renderer.SetPropertyBlock(block);
        }
    }

    void UpdateAura()
    {
        if (auraShards == null) return;
        for (int i = 0; i < auraShards.Length; i++)
        {
            PortalSlot shard = auraShards[i];
            if (shard == null || shard.transform == null || shard.renderer == null) continue;
            float phase = Time.unscaledTime * (0.55f + i * 0.035f) + i * (Mathf.PI * 2f / auraShards.Length);
            float radius = 1.4f + (i % 3) * 0.32f;
            shard.transform.localPosition = new Vector3(Mathf.Cos(phase) * radius, 0.55f + Mathf.Sin(phase * 1.7f) * 0.42f, Mathf.Sin(phase) * radius);
            shard.transform.localRotation = Quaternion.Euler(-90f, -phase * Mathf.Rad2Deg * 1.6f, 0f);
            float pulse = 0.82f + Mathf.Sin(Time.unscaledTime * 6.5f + i) * 0.18f;
            shard.transform.localScale = Vector3.one * (shard.baseScale * pulse);
            shard.renderer.GetPropertyBlock(block);
            block.SetFloat(PortalPulse, 1f);
            block.SetFloat(RimPulse, 0.8f + pulse * 0.65f);
            block.SetFloat(ChromaticSplit, 0.025f * pulse);
            block.SetFloat(DissolveAmount, 0f);
            shard.renderer.SetPropertyBlock(block);
        }
    }

    void ClearAura(bool destroy)
    {
        if (auraShards == null) return;
        for (int i = 0; i < auraShards.Length; i++)
        {
            PortalSlot shard = auraShards[i];
            if (shard == null || shard.transform == null) continue;
            if (destroy) Destroy(shard.transform.gameObject);
            else shard.transform.gameObject.SetActive(false);
        }
    }
}
