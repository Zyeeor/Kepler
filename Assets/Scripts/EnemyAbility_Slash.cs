using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Basic Attack: Slash. A forward 90-degree arc that damages the player and all enemies within range.
/// Adapted from PlayerAbility_Slash for enemy usage.
/// </summary>
public class EnemyAbility_Slash : EnemyAbility
{
    [Header("Slash")]
    public float slashRange = 3.5f;
    [Tooltip("Arc angle in degrees, centered on aim direction.")]
    public float slashAngle = 90f;

    [Header("Slash VFX")]
    public GameObject slashEffectPrefab;
    public float slashEffectDuration = 0.3f;
    public int slashEffectCount = 7;
    public float slashEffectRadius = 2f;
    public float slashEffectHeight = 1f;
    public Color slashColor = new Color(0.2f, 0.6f, 1f);
    [ColorUsage(false, true)] public Color slashEmissionColor = new Color(0.2f, 0.6f, 1f);

    [Header("Hit VFX")]
    [Tooltip("VFX prefab spawned at each hit target's position on impact.")]
    public GameObject hitEffectPrefab;
    [Tooltip("Duration before the hit effect is destroyed.")]
    public float hitEffectDuration = 0.5f;

    [Header("Targeting")]
    [Tooltip("Who gets hit when AI-controlled. Use ~0 (Everything) for all layers. When possessed, always hits everything.")]
    public LayerMask targetMask = -1;

    private void OnEnable()
    {
        type = AbilityType.BasicAttack;
        abilityName = "Slash";
        cooldown = cooldown <= 0f ? 0.3f : cooldown;
    }

    public override bool CanTrigger()
    {
        // When possessed, player manually triggers; no target required.
        // When AI-controlled, need a detected player target.
        if (owner.isPossessed)
            return base.CanTrigger();
        return base.CanTrigger() && owner != null && owner.targetPlayer != null;
    }

    protected override void OnTrigger()
    {
        Vector3 forward = owner.transform.forward;
        Vector3 slashOrigin = owner.transform.position + forward * 0.5f;
        CreateSlashArc(slashOrigin, forward, slashRange);

        // Detect all valid targets in the arc
        int layerMask = owner.isPossessed ? ~0 : targetMask;

        // Use OverlapSphere with no angle restriction for reliable detection
        Collider[] hits = Physics.OverlapSphere(slashOrigin, slashRange, layerMask, QueryTriggerInteraction.Collide);
        HashSet<Enemy> hitEnemies = new HashSet<Enemy>();

        foreach (var hit in hits)
        {
            // Skip self
            if (hit.transform.IsChildOf(owner.transform)) continue;

            // Check player
            var ph = hit.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                DealDamageToPlayer(ph, damage);
                SpawnHitEffect(hit.transform.position);
                continue;
            }

            // Check other enemies
            var enemy = hit.GetComponentInParent<Enemy>();
            if (enemy != null && enemy != owner && !enemy.isDowned && !enemy.isPossessed && !hitEnemies.Contains(enemy))
            {
                Debug.Log("[Slash] HITTING " + enemy.name + " for " + damage + " damage (possessed=" + owner.isPossessed + ")");
                enemy.TakeDamage(damage);
                SpawnHitEffect(enemy.transform.position);
                hitEnemies.Add(enemy);
            }
        }
    }

    void CreateSlashArc(Vector3 position, Vector3 direction, float radius)
    {
        float startAngle = -slashAngle / 2f;
        float angleStep = slashAngle / (slashEffectCount - 1);
        GameObject arcParent = new GameObject("SlashArc");
        arcParent.transform.position = position;
        arcParent.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        // Trail line
        GameObject trailObj = new GameObject("SlashTrail");
        trailObj.transform.SetParent(arcParent.transform);
        trailObj.transform.localPosition = Vector3.zero;
        trailObj.transform.localRotation = Quaternion.identity;
        var lineRenderer = trailObj.AddComponent<LineRenderer>();
        int lineSegments = slashEffectCount * 2;
        lineRenderer.positionCount = lineSegments;
        lineRenderer.startWidth = 0.15f;
        lineRenderer.endWidth = 0.02f;
        lineRenderer.useWorldSpace = false;
        lineRenderer.alignment = LineAlignment.View;
        Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (lineShader == null) lineShader = Shader.Find("Standard");
        Material lineMat = new Material(lineShader);
        lineMat.color = new Color(slashColor.r, slashColor.g, slashColor.b, 0.9f);
        lineMat.SetInt("_Surface", 1);
        lineMat.SetInt("_Blend", 0);
        lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        lineMat.SetInt("_ZWrite", 0);
        lineMat.SetInt("_Cull", 0);
        lineMat.renderQueue = 3001;
        lineRenderer.material = lineMat;
        for (int i = 0; i < lineSegments; i++)
        {
            float t = (float)i / (lineSegments - 1);
            float angle = startAngle + slashAngle * t;
            float rad = angle * Mathf.Deg2Rad;
            lineRenderer.SetPosition(i, new Vector3(Mathf.Sin(rad) * slashEffectRadius, slashEffectHeight * 0.5f, Mathf.Cos(rad) * slashEffectRadius));
        }
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 0.5f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        lineRenderer.colorGradient = grad;
        Destroy(trailObj, slashEffectDuration);

        // Segments
        for (int i = 0; i < slashEffectCount; i++)
        {
            float angle = startAngle + angleStep * i;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 localPos = new Vector3(Mathf.Sin(rad) * slashEffectRadius, slashEffectHeight * 0.5f, Mathf.Cos(rad) * slashEffectRadius);
            if (slashEffectPrefab != null)
            {
                GameObject obj = Instantiate(slashEffectPrefab, arcParent.transform);
                obj.transform.localPosition = localPos;
                obj.transform.localRotation = Quaternion.Euler(0, angle, 90);
                obj.transform.localScale = Vector3.one;
                PlayVfx(obj);
                Destroy(obj, slashEffectDuration);
            }
            else
            {
                // No prefab — skip segment VFX entirely
            }
        }
        Destroy(arcParent, slashEffectDuration + 0.2f);
    }

    void SpawnHitEffect(Vector3 position)
    {
        if (hitEffectPrefab == null) return;
        GameObject effect = Instantiate(hitEffectPrefab, position, Quaternion.identity);
        PlayVfx(effect);
        Destroy(effect, hitEffectDuration);
    }
}
