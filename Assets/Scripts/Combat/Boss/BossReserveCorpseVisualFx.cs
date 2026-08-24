using UnityEngine;

/// <summary>
/// Runtime-only material layer for the seven permanent Boss reserve corpses.
/// It never changes a source monster prefab or shared material.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossReserveCorpseVisualFx : MonoBehaviour
{
    static readonly int PortalPulse = Shader.PropertyToID("_PortalPulse");
    static readonly int RimPulse = Shader.PropertyToID("_RimPulse");
    static readonly int ChromaticSplit = Shader.PropertyToID("_ChromaticSplit");
    static readonly int SinChannel = Shader.PropertyToID("_SinChannel");

    MonsterActor owner;
    Transform[] rings;
    Renderer[] ringRenderers;
    Material ringMaterial;
    MaterialPropertyBlock propertyBlock;
    float orbitSpeed;
    float phaseOffset;
    float baseRadius;

    public static void EnsureFor(MonsterActor actor)
    {
        if (actor == null) return;
        BossReserveCorpseVisualFx fx = actor.GetComponent<BossReserveCorpseVisualFx>();
        if (fx == null) fx = actor.gameObject.AddComponent<BossReserveCorpseVisualFx>();
        fx.Configure(actor);
    }

    public void Deactivate()
    {
        if (rings == null) return;
        for (int i = 0; i < rings.Length; i++)
            if (rings[i] != null) rings[i].gameObject.SetActive(false);
    }

    void Configure(MonsterActor actor)
    {
        owner = actor;
        ResolveSinStyle(owner.sinType, out Color rimColor, out float sinChannel, out orbitSpeed, out phaseOffset, out baseRadius);
        if (ringMaterial == null)
        {
            Shader shader = Shader.Find("Possession/BossSevenfoldDistortion");
            if (shader == null) return;
            ringMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            propertyBlock = new MaterialPropertyBlock();
        }

        ringMaterial.SetColor("_BaseColor", Color.Lerp(Color.black, rimColor, 0.035f));
        ringMaterial.SetColor("_RimColor", rimColor);
        ringMaterial.SetFloat("_RimPower", 1.2f);
        if (rings == null) CreateRings();

        for (int i = 0; i < rings.Length; i++)
        {
            if (rings[i] == null) continue;
            rings[i].gameObject.SetActive(true);
            ringRenderers[i].GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(PortalPulse, 0.9f);
            propertyBlock.SetFloat(RimPulse, 1.2f);
            propertyBlock.SetFloat(ChromaticSplit, 0.025f);
            propertyBlock.SetFloat(SinChannel, sinChannel);
            ringRenderers[i].SetPropertyBlock(propertyBlock);
        }
    }

    void CreateRings()
    {
        const int ringCount = 3;
        rings = new Transform[ringCount];
        ringRenderers = new Renderer[ringCount];
        for (int i = 0; i < ringCount; i++)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ring.name = "Boss Reserve Sin Ring " + (i + 1);
            ring.layer = 2;
            Collider collider = ring.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            ring.transform.SetParent(transform, false);
            ring.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            Renderer renderer = ring.GetComponent<Renderer>();
            renderer.sharedMaterial = ringMaterial;
            rings[i] = ring.transform;
            ringRenderers[i] = renderer;
        }
    }

    void LateUpdate()
    {
        if (owner == null || !owner.IsBossBattleReserveBody || rings == null) return;
        float now = Time.unscaledTime;
        for (int i = 0; i < rings.Length; i++)
        {
            Transform ring = rings[i];
            Renderer renderer = ringRenderers[i];
            if (ring == null || renderer == null) continue;

            float phase = now * (orbitSpeed + i * 0.11f) + phaseOffset + i * 2.09f;
            float radius = baseRadius + i * 0.22f;
            ring.localPosition = new Vector3(Mathf.Cos(phase) * radius, 0.08f + i * 0.035f,
                Mathf.Sin(phase) * radius);
            ring.localRotation = Quaternion.Euler(-90f, -phase * Mathf.Rad2Deg * 1.35f, 0f);
            float pulse = 0.78f + Mathf.Sin(now * 5.8f + i + phaseOffset) * 0.22f;
            ring.localScale = Vector3.one * ((0.82f + i * 0.16f) * pulse);
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(PortalPulse, pulse);
            propertyBlock.SetFloat(RimPulse, 0.9f + pulse);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    void OnDestroy()
    {
        if (ringMaterial != null) Destroy(ringMaterial);
    }

    static void ResolveSinStyle(SinType sin, out Color rimColor, out float sinChannel,
        out float speed, out float phase, out float radius)
    {
        sinChannel = (float)sin;
        speed = 0.52f;
        phase = (float)sin * 0.71f;
        radius = 0.72f;
        switch (sin)
        {
            case SinType.Pride: rimColor = new Color(1.8f, 0.24f, 0.86f); speed = 0.68f; radius = 0.92f; break;
            case SinType.Wrath: rimColor = new Color(2f, 0.16f, 0.05f); speed = 0.82f; radius = 0.84f; break;
            case SinType.Gluttony: rimColor = new Color(0.78f, 1.4f, 0.1f); speed = 0.48f; radius = 0.76f; break;
            case SinType.Greed: rimColor = new Color(2.0f, 1.18f, 0.08f); speed = 0.58f; radius = 0.8f; break;
            case SinType.Envy: rimColor = new Color(0.1f, 1.7f, 1.5f); speed = 0.74f; radius = 0.86f; break;
            case SinType.Lust: rimColor = new Color(1.95f, 0.06f, 0.42f); speed = 0.64f; radius = 0.9f; break;
            case SinType.Sloth: rimColor = new Color(0.36f, 0.28f, 1.9f); speed = 0.4f; radius = 0.74f; break;
            default: rimColor = new Color(1.25f, 0.08f, 2f); break;
        }
    }
}
