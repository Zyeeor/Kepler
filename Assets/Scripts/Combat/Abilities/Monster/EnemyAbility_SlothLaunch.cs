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

    [Header("Landing VFX")]
    [Tooltip("VFX played only when Sloth.LandingBlast is unlocked and the launch lands.")]
    public GameObject landingVfxPrefab;
    [Tooltip("Optional Transform the landing VFX follows. Falls back to the Sloth owner when unassigned.")]
    public Transform landingVfxSpawnPoint;
    [Tooltip("Local position offset from the Landing VFX Spawn Point.")]
    public Vector3 landingVfxPositionOffset;
    [Tooltip("Local Euler rotation offset from the Landing VFX Spawn Point.")]
    public Vector3 landingVfxRotationOffset;
    [Tooltip("Landing explosion VFX lifetime before it is destroyed.")]
    public float landingVfxDuration = 1f;

    [Header("Upgrade - Sloth.LandingMine")]
    public GameObject minePrefab;
    [Tooltip("Mine spawn position offset from the takeoff point (owner local space).")]
    public Vector3 mineSpawnOffset;
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
        EnsureUpgrade("SL-M01");
        EnsureUpgrade("SL-M02");
        if (appliedEffectTags != null)

            appliedEffectTags.Clear();
    }

    protected override GameObject SpawnVfx()
    {
        SpawnWeaponVfx();
        if (vfxPrefab == null) return null;

        Transform anchor = vfxSpawnPoint != null ? vfxSpawnPoint : owner.transform;
        activeVfx = Instantiate(vfxPrefab, anchor);
        activeVfx.transform.localPosition = vfxPositionOffset;
        activeVfx.transform.localRotation = Quaternion.Euler(vfxRotationOffset);
        activeVfx.transform.localScale *= OwnerCombatScaleMultiplier;
        PlayVfx(activeVfx);
        return activeVfx;
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
        if (IsUpgradeUnlocked("SL-M01"))

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
            Vector3 target = Vector3.Lerp(start, end, t);
            target.y = start.y + 4f * jumpHeight * t * (1f - t);

            // 水平位移复用 MonsterActor 的 SphereCast + CollideAndSlide：
            // 不能直接写 transform.position，否则大体型精英会穿过建筑并把落点写进装饰物内部。
            Vector3 current = owner.transform.position;
            Vector3 horizontalTarget = new Vector3(target.x, current.y, target.z);
            owner.MoveWithAbilityCollision(horizontalTarget - current);

            // 碰撞只约束 XZ，Y 仍由跳跃曲线驱动。
            current = owner.transform.position;
            current.y = target.y;
            owner.transform.position = current;
            yield return null;
        }

        if (owner != null)
        {
            // 不再把位置强行写回 nominal end；若途中撞到建筑，保持最后一个安全落点。
            Vector3 land = owner.transform.position;
            land.y = start.y;
            owner.transform.position = land;
            // 水平碰撞检测是在腾空高度进行的；落地时再做一次起点重叠校正，
            // 防止障碍物低矮/顶部可穿过但地面占位仍把根节点卡在建筑内部。
            owner.ResolveAbilityPenetration();
            owner.IsAbilityFacingLocked = false;
            if (IsUpgradeUnlocked("SL-M02"))

            {
                PlayLandingVfxOnSelf();
                DamageEnemiesInSphere(land, landingRadius, landingDamage, null, landingVfxDuration);
            }
        }

        EndActivationEffect();
    }

    private void PlayLandingVfxOnSelf()
    {
        if (landingVfxPrefab == null || owner == null) return;

        Transform anchor = landingVfxSpawnPoint != null ? landingVfxSpawnPoint : owner.transform;
        GameObject vfx = Instantiate(landingVfxPrefab, anchor);
        vfx.transform.localPosition = landingVfxPositionOffset;
        vfx.transform.localRotation = Quaternion.Euler(landingVfxRotationOffset);
        PlayVfx(vfx);
        StopVfxLooping(vfx);
        ReleaseVfx(vfx, Mathf.Max(0.01f, landingVfxDuration));
    }

    private void PlaceMine(Vector3 pos)
    {
        while (activeMines.Count >= Mathf.Max(1, maxMines))
        {
            MineBehaviour oldest = activeMines[0];
            activeMines.RemoveAt(0);
            if (oldest != null) Destroy(oldest.gameObject);
        }

        Vector3 minePos = pos;
        if (owner != null)
            minePos = pos + owner.transform.TransformDirection(mineSpawnOffset);

        GameObject mineGo;
        if (minePrefab != null)
            mineGo = Instantiate(minePrefab, minePos, Quaternion.identity);
        else
            mineGo = new GameObject("Mine");

        mineGo.transform.localScale *= OwnerCombatScaleMultiplier;

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

    private void EnsureUpgrade(string effectId)
    {
        if (upgrades == null) upgrades = new List<UpgradeSlot>();
        if (upgrades.Exists(slot => slot != null && string.Equals(slot.effectId, effectId, System.StringComparison.OrdinalIgnoreCase))) return;
        upgrades.Add(new UpgradeSlot { effectId = effectId, unlocked = false });
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
