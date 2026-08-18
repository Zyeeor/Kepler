using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gluttony basic: aim ground point, snapshot on cast, delayed bite with telegraph.
/// Overfed (no GL-X01): next maw coverage ×2 then consume.
/// Overfed + GL-X01: paired maws then consume.
/// GL-A01: one extra synced maw (cap 2). GL-A02: one-shot ×2 after mobility.
/// GL-A03: farther aim. GL-TG01: larger coverage.
/// </summary>
public class EnemyAbility_GluttonyAbyssMaw : EnemyAbility
{
    [Header("Maw")]
    public float maxAimDistance = 8f;
    public float glA03AimDistance = 12f;
    public float warnDelay = 0.7f;
    public float blastRadius = 2.2f;
    public float damageAmount = 20f;
    public float glTg01SizeMult = 1.2f;
    public float pairedOffset = 2.2f;
    public float extraMawOffset = 2.5f;
    public GameObject telegraphPrefab;
    public GameObject blastVfxPrefab;
    public float blastVfxDuration = 1f;
    public LayerMask groundMask = ~0;

    private GluttonyBodyState _state;

    private void OnEnable()
    {
        type = AbilityType.BasicAttack;
        abilityName = "深渊巨口";
        cooldown = cooldown <= 0f ? 2f : cooldown;
        if (damage <= 0f) damage = damageAmount;
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Gluttony.AbyssMaw", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Gluttony.AbyssMaw");
    }

    protected override void OnTrigger()
    {
        if (owner == null) return;
        var anim = owner.GetActiveAnimator();
        if (anim != null) anim.SetTrigger("Basic");
        CacheOwnerState();
        if (!TryResolveAimPoint(out Vector3 aimPoint))
        {
            EndActivationEffect();
            return;
        }

        float sizeMult = 1f;
        int mawCount = 1;
        bool paired = false;

        if (_state != null && _state.TryConsumeOverfed())
        {
            if (IsUpgradeUnlocked("GL-X01"))
            {
                mawCount = 2;
                paired = true;
            }
            else
            {
                sizeMult = 2f;
            }
        }

        if (_state != null && _state.TryConsumeHuntStepEmpower())
            sizeMult = Mathf.Max(sizeMult, 2f);

        if (IsUpgradeUnlocked("GL-TG01"))
            sizeMult *= GetCardParameter("MawSizeMult", glTg01SizeMult);

        if (IsUpgradeUnlocked("GL-A01") && mawCount < 2)
            mawCount = 2;

        float radius = blastRadius * sizeMult;
        float dmg = damage > 0f ? damage : damageAmount;
        Vector3 forward = owner.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        StartCoroutine(SpawnMawsRoutine(aimPoint, forward, right, mawCount, paired, radius, dmg));
    }

    private IEnumerator SpawnMawsRoutine(Vector3 center, Vector3 forward, Vector3 right, int count, bool paired, float radius, float dmg)
    {
        var points = new List<Vector3>(2) { center };
        if (count >= 2)
        {
            float offset = paired ? pairedOffset : extraMawOffset;
            offset *= Mathf.Max(1f, radius / Mathf.Max(0.01f, blastRadius));
            points.Clear();
            points.Add(center - right * (offset * 0.5f));
            points.Add(center + right * (offset * 0.5f));
        }

        var telegraphs = new List<GameObject>(points.Count);
        foreach (Vector3 p in points)
            telegraphs.Add(SpawnTelegraph(p, radius));

        yield return AbilityWait(warnDelay);

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 p = points[i];
            if (i < telegraphs.Count && telegraphs[i] != null) Destroy(telegraphs[i]);
            PlayBlastVfx(p, radius);
            DamageEnemiesInSphere(p, radius, dmg);
            TryDamagePlayerInRadius(p, radius, dmg);
        }

        EndActivationEffect();
    }

    private bool TryResolveAimPoint(out Vector3 aimPoint)
    {
        aimPoint = owner.transform.position + owner.transform.forward * 3f;
        float maxDist = IsUpgradeUnlocked("GL-A03")
            ? GetCardParameter("MawAimDistance", glA03AimDistance)
            : maxAimDistance;

        if (owner.isPossessed && PlayerController.Instance != null &&
            PlayerController.Instance.TryGetAimPoint(out Vector3 mouseAim))
        {
            aimPoint = mouseAim;
        }
        else if (owner.targetPlayer != null)
        {
            // Enemy baseline: place at predicted player position (simple lead).
            Vector3 target = owner.targetPlayer.position;
            Vector3 vel = Vector3.zero;
            var rb = owner.targetPlayer.GetComponent<Rigidbody>();
            if (rb != null) vel = rb.velocity;
            aimPoint = target + vel * warnDelay;
        }

        aimPoint.y = owner.transform.position.y;
        Vector3 delta = aimPoint - owner.transform.position;
        delta.y = 0f;
        if (delta.magnitude > maxDist)
            aimPoint = owner.transform.position + delta.normalized * maxDist;

        if (Physics.Raycast(aimPoint + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f, groundMask, QueryTriggerInteraction.Ignore))
            aimPoint = hit.point;
        else
            aimPoint.y = owner.transform.position.y;

        return true;
    }

    private GameObject SpawnTelegraph(Vector3 pos, float radius)
    {
        GameObject go;
        if (telegraphPrefab != null)
        {
            go = Instantiate(telegraphPrefab, pos, Quaternion.identity);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "AbyssMaw_Telegraph";
            Object.Destroy(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                if (renderer.material.HasProperty("_BaseColor"))
                    renderer.material.SetColor("_BaseColor", new Color(0.85f, 0.1f, 0.1f, 0.45f));
                else
                    renderer.material.color = new Color(0.85f, 0.1f, 0.1f, 0.45f);
            }
        }

        go.transform.position = pos;
        float diameter = radius * 2f;
        go.transform.localScale = new Vector3(diameter, 0.05f, diameter);
        return go;
    }

    private void PlayBlastVfx(Vector3 pos, float radius)
    {
        if (blastVfxPrefab == null && vfxPrefab == null) return;
        GameObject prefab = blastVfxPrefab != null ? blastVfxPrefab : vfxPrefab;
        GameObject vfx = Instantiate(prefab, pos, Quaternion.identity);
        vfx.transform.localScale = Vector3.one * Mathf.Max(0.1f, radius / Mathf.Max(0.01f, blastRadius));
        PlayVfx(vfx);
        StopVfxLooping(vfx);
        Destroy(vfx, Mathf.Max(0.01f, blastVfxDuration));
    }

    private void CacheOwnerState()
    {
        if (owner == null) return;
        _state = owner.GetComponent<GluttonyBodyState>();
        if (_state == null) _state = owner.gameObject.AddComponent<GluttonyBodyState>();
    }
}
