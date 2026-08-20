using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sloth mobility: wind-up then a short hop. Base landing has no damage.
/// Card Sloth.LandingBlast: landing explosion VFX on self + AoE damage.
/// Card Sloth.LandingMine: place a mine at the takeoff point before jumping.
/// </summary>
public class EnemyAbility_SlothLaunch : EnemyAbility
{
    public float windupDuration = 0.25f;
    public float launchDistance = 5f;
    public float jumpHeight = 2.4f;
    public float jumpDuration = 0.45f;
    public float landingRadius = 2.5f;
    public float landingDamage = 20f;
    [Tooltip("Landing explosion VFX lifetime before it is destroyed.")]
    public float landingVfxDuration = 1f;

    [Header("Upgrade - Sloth.LandingMine")]
    public GameObject minePrefab;
    public float mineDuration = 10f;
    public int maxMines = 3;
    public GameObject mineBlastVfxPrefab;
    [Tooltip("Mine explosion VFX lifetime before it is destroyed.")]
    public float mineBlastVfxDuration = 1f;
    public float mineDamage = 20f;

    private readonly List<MineBehaviour> activeMines = new List<MineBehaviour>();

    private void OnEnable()
    {
        type = AbilityType.Mobility;
        abilityName = "弹射起步";
        cooldown = cooldown <= 0f ? 1f : cooldown;
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Sloth.Launch", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Sloth.Launch");
        if (appliedEffectTags != null)
            appliedEffectTags.Clear();
    }

    protected override GameObject SpawnVfx()
    {
        SpawnWeaponVfx();
        return null;
    }

    protected override void OnTrigger()
    {
        StartCoroutine(LaunchRoutine());
    }

    private IEnumerator LaunchRoutine()
    {
        if (owner == null)
        {
            EndActivationEffect();
            yield break;
        }

        owner.IsAbilityFacingLocked = true;
        if (IsUpgradeUnlocked("Sloth.LandingMine"))
            PlaceMine(owner.transform.position);

        yield return AbilityWait(windupDuration);
        if (owner == null)
        {
            EndActivationEffect();
            yield break;
        }

        Vector3 direction = owner.transform.forward;
        if (owner.isPossessed && TryGetPossessedMouseDirection(out Vector3 aimDirection))
            direction = aimDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = owner.transform.forward;
        direction.Normalize();
        owner.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        Vector3 start = owner.transform.position;
        Vector3 end = start + direction * launchDistance;
        float elapsed = 0f;
        while (owner != null && elapsed < jumpDuration)
        {
            elapsed += AbilityDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, jumpDuration));
            Vector3 pos = Vector3.Lerp(start, end, t);
            pos.y = start.y + 4f * jumpHeight * t * (1f - t);
            owner.transform.position = pos;
            yield return null;
        }

        if (owner != null)
        {
            Vector3 land = end;
            land.y = start.y;
            owner.transform.position = land;
            owner.IsAbilityFacingLocked = false;
            if (IsUpgradeUnlocked("Sloth.LandingBlast"))
            {
                PlayLandingVfxOnSelf();
                DamageEnemiesInSphere(land, landingRadius, landingDamage, null, landingVfxDuration);
            }
        }

        EndActivationEffect();
    }

    private void PlayLandingVfxOnSelf()
    {
        if (vfxPrefab == null || owner == null) return;
        GameObject vfx = Instantiate(vfxPrefab, owner.transform.position, Quaternion.identity);
        vfx.transform.SetParent(owner.transform, true);
        PlayVfx(vfx);
        StopVfxLooping(vfx);
        Destroy(vfx, Mathf.Max(0.01f, landingVfxDuration));
    }

    private void PlaceMine(Vector3 pos)
    {
        while (activeMines.Count >= Mathf.Max(1, maxMines))
        {
            MineBehaviour oldest = activeMines[0];
            activeMines.RemoveAt(0);
            if (oldest != null) Destroy(oldest.gameObject);
        }

        GameObject mineGo;
        if (minePrefab != null)
            mineGo = Instantiate(minePrefab, pos, Quaternion.identity);
        else
            mineGo = new GameObject("Mine");

        float radius = GetWorldRadiusXZ(mineGo);
        MineBehaviour mine = mineGo.GetComponent<MineBehaviour>();
        if (mine == null) mine = mineGo.AddComponent<MineBehaviour>();
        mine.lifetime = mineDuration;
        mine.triggerRadius = radius;
        mine.blastRadius = radius;
        mine.damage = mineDamage > 0f ? mineDamage : landingDamage;
        mine.placer = owner;
        mine.blastVfxPrefab = mineBlastVfxPrefab;
        mine.blastVfxDuration = mineBlastVfxDuration;
        mine.drawHitboxes = drawHitboxes;
        mine.onExplode = _ => { activeMines.Remove(mine); };
        activeMines.Add(mine);
    }

    private static float GetWorldRadiusXZ(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return 1f;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return Mathf.Max(0.1f, Mathf.Max(bounds.extents.x, bounds.extents.z));
    }

    protected override void OnDisable()
    {
        if (owner != null) owner.IsAbilityFacingLocked = false;
        base.OnDisable();
    }
}
