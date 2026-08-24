using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gluttony's Attack: a snapshotted ground maw that resolves after a readable telegraph.
/// Overfed turns the next cast into a second bite; GL-A01 changes that into a paired maw cast.
/// </summary>
public class EnemyAbility_GluttonyAbyssMaw : EnemyAbility
{
    [Header("Maw")]
    public float maxAimDistance = 8f;
    public float glA03AimDistance = 12f;
    public float warnDelay = 0.5f;
    public float secondBiteDelay = 0.5f;
    public float blastRadius = 2.2f;
    public float damageAmount = 20f;
    public float pairedOffset = 2.2f;
    public GameObject telegraphPrefab;
    [Tooltip("Local position offset applied to each telegraph Prefab instance after its rotation.")]
    public Vector3 telegraphPositionOffset = Vector3.zero;
    [Tooltip("Euler rotation applied to each telegraph Prefab instance.")]
    public Vector3 telegraphRotation = Vector3.zero;
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
        _state?.ExitSmallCatForAttack();
        if (!TryResolveAimPoint(out Vector3 aimPoint))
        {
            EndActivationEffect();
            return;
        }

        bool consumeOverfed = _state != null && _state.TryConsumeOverfed();
        bool pairedMaws = consumeOverfed && IsUpgradeUnlocked("GL-A01");
        bool enlarged = _state != null && _state.TryConsumeHuntStepEmpower();
        float radius = blastRadius * (enlarged ? 2f : 1f);
        float dmg = damage > 0f ? damage : damageAmount;

        Vector3 forward = owner.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        StartCoroutine(SpawnMawsRoutine(aimPoint, right, pairedMaws, consumeOverfed, radius, dmg));
    }

    private IEnumerator SpawnMawsRoutine(Vector3 center, Vector3 right, bool pairedMaws, bool secondBite, float radius, float dmg)
    {
        if (!secondBite)
        {
            yield return SpawnSingleMawRoutine(center, radius, dmg);
            EndActivationEffect();
            yield break;
        }

        Vector3 firstPoint = center;
        Vector3 secondPoint = center;
        if (pairedMaws)
        {
            float offset = ScaleAbilityRadius(pairedOffset) * Mathf.Max(1f, radius / Mathf.Max(0.01f, blastRadius));
            firstPoint = center - right * (offset * 0.5f);
            secondPoint = center + right * (offset * 0.5f);
        }

        yield return SpawnSingleMawRoutine(firstPoint, radius, dmg);
        yield return AbilityWait(secondBiteDelay);
        yield return SpawnSingleMawRoutine(secondPoint, radius, dmg);
        EndActivationEffect();
    }

    private IEnumerator SpawnSingleMawRoutine(Vector3 point, float radius, float dmg)
    {
        float scaledRadius = ScaleAbilityRadius(radius);
        GameObject telegraph = SpawnTelegraph(point, scaledRadius);
        yield return AbilityWait(warnDelay);

        if (telegraph != null) Destroy(telegraph);

        // Blast VFX is the maw resolve cue at the snapshotted ground point (not per-hit).
        PlayBlastVfx(point, scaledRadius);
        DamageEnemiesInSphere(point, radius, dmg, null, blastVfxDuration);
        TryDamagePlayerInRadius(point, radius, dmg, blastVfxDuration);
    }

    private bool TryResolveAimPoint(out Vector3 aimPoint)
    {
        aimPoint = owner.transform.position;
        if (!IsUpgradeUnlocked("GL-A03"))
            return ProjectToGround(ref aimPoint);

        float maxDistance = ScaleAbilityRadius(GetCardParameter("MawAimDistance", glA03AimDistance));
        if (owner.isPossessed && PlayerController.Instance != null &&
            PlayerController.Instance.TryGetAimPoint(out Vector3 mouseAim))
        {
            aimPoint = mouseAim;
        }
        else if (owner.targetPlayer != null)
        {
            Vector3 velocity = Vector3.zero;
            Rigidbody targetRigidbody = owner.targetPlayer.GetComponent<Rigidbody>();
            if (targetRigidbody != null) velocity = targetRigidbody.velocity;
            aimPoint = owner.targetPlayer.position + velocity * warnDelay;
        }
        else
        {
            aimPoint = owner.transform.position + owner.transform.forward * Mathf.Min(3f, maxAimDistance);
        }

        Vector3 delta = aimPoint - owner.transform.position;
        delta.y = 0f;
        if (delta.magnitude > maxDistance)
            aimPoint = owner.transform.position + delta.normalized * maxDistance;

        return ProjectToGround(ref aimPoint);
    }

    private bool ProjectToGround(ref Vector3 point)
    {
        if (Physics.Raycast(point + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f, groundMask, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
            return true;
        }

        point.y = owner.transform.position.y;
        return true;
    }

    private GameObject SpawnTelegraph(Vector3 position, float radius)
    {
        if (telegraphPrefab != null)
        {
            Quaternion rotation = Quaternion.Euler(telegraphRotation);
            Vector3 spawnPosition = position + rotation * telegraphPositionOffset;
            GameObject telegraph = Instantiate(telegraphPrefab, spawnPosition, rotation);
            float scaleMultiplier = radius / Mathf.Max(0.01f, blastRadius);
            telegraph.transform.localScale *= scaleMultiplier;
            return telegraph;
        }

        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fallback.name = "AbyssMaw_Telegraph";
        Object.Destroy(fallback.GetComponent<Collider>());
        fallback.transform.SetPositionAndRotation(position, Quaternion.identity);
        fallback.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);

        Renderer renderer = fallback.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            if (renderer.material.HasProperty("_BaseColor"))
                renderer.material.SetColor("_BaseColor", new Color(0.85f, 0.1f, 0.1f, 0.45f));
            else
                renderer.material.color = new Color(0.85f, 0.1f, 0.1f, 0.45f);
        }
        return fallback;
    }

    private void PlayBlastVfx(Vector3 position, float radius)
    {
        if (blastVfxPrefab == null && vfxPrefab == null) return;
        GameObject prefab = blastVfxPrefab != null ? blastVfxPrefab : vfxPrefab;
        GameObject vfx = Instantiate(prefab, position, Quaternion.identity);
        vfx.transform.localScale *= radius / Mathf.Max(0.01f, blastRadius);
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
