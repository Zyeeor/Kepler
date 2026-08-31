using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lust LMB: outbound + return mist, each segment hits independently and writes Link.
/// LU-A04 adds an expanding ring mist while retaining the outbound and return mist cast.

/// LU-M05: current Anchor mirrors the attack after 0.10s at 50% damage (no recurse).
/// LU-A03: Linked target death consumes Link and blasts after 0.10s.
/// </summary>
public class EnemyAbility_LustRoundTrip : EnemyAbility
{
    [Header("Round Trip")]
    public float segmentDamage = 20f;
    [Tooltip("Possessed Player 专属每段伤害；Enemy 版本仍使用 damage / segmentDamage。")]
    public float possessedDamageOverride = 25f;
    public float mistWidth = 1f;
    public float mistSpeed = 14f;
    public float mistRange = 8f;
    public float linkDuration = 6f;
    public float aimTurnSpeed = 720f;
    public GameObject mistVfxPrefab;
    public GameObject linkMarkVfx;

    [Header("LU-A04 Ring")]
    public float ringInnerRadius = 0.5f;
    public float ringOuterRadius = 4f;
    public float ringExpandDuration = 0.8f;
    public float ringWidth = 0.8f;
    public GameObject ringMistVfx;

    [Header("LU-M05 Mirror")]
    public float mirrorDelay = 0.10f;
    public float mirrorDamageMul = 0.5f;

    [Header("LU-A03 Death Blast")]
    public float a03Delay = 0.10f;
    public float a03Damage = 30f;
    public float a03Radius = 2.5f;
    public GameObject a03BlastVfx;
    [Tooltip("LU-A03 死亡爆炸 VFX 相对爆炸点的偏移。")]
    public Vector3 a03BlastVfxOffset = Vector3.zero;

    private LustBodyState _state;
    private readonly HashSet<int> _watchedLinkIds = new HashSet<int>();

    private void OnEnable()
    {
        type = AbilityType.BasicAttack;
        abilityName = "迷情往返";
        cooldown = cooldown <= 0f ? 2f : cooldown;
        if (damage <= 0f) damage = segmentDamage;
        if (abilityTags == null) abilityTags = new List<string>();
        EnsureTag("Ability.Monster.Lust");
        EnsureTag("Ability.Monster.Lust.RoundTrip");
        EnsureUpgradeSlot("LU-M05");
        EnsureUpgradeSlot("LU-A03");
        EnsureUpgradeSlot("LU-A04");
        EnsureUpgradeSlot("LU-TG01");
    }

    private void EnsureTag(string tag)
    {
        if (!abilityTags.Exists(t => string.Equals(t, tag, System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add(tag);
    }

    private void EnsureUpgradeSlot(string effectId)
    {
        if (upgrades == null) upgrades = new List<UpgradeSlot>();
        if (upgrades.Exists(u => u != null && string.Equals(u.effectId, effectId, System.StringComparison.OrdinalIgnoreCase)))
            return;
        upgrades.Add(new UpgradeSlot { effectId = effectId, unlocked = false });
    }

    protected override void Update()
    {
        base.Update();
        if (!IsUpgradeUnlocked("LU-A03")) return;
        CacheState();
        WatchLinkedDeaths();
    }

    protected override void OnTrigger()
    {
        CacheState();
        if (owner is BossSevenfoldActor)
        {
            StartCoroutine(BossSpreadRoutine());
            return;
        }
        StartCoroutine(AttackRoutine(owner != null ? owner.transform.position : Vector3.zero, ResolveAimDirection(), 1f, true));
    }

    /// <summary>
    /// 迷情往返是直线往返飞弹：红圈用矩形预警带（长度=mistRange、宽度=mistWidth、朝向=瞄准方向），
    /// 与 TravelSegment 的 SphereCastAll 判定一致（红圈=实际范围）。受 enemyIndicatorEnabled 开关控制。
    /// </summary>
    public override EnemyTelegraphGeometry GetEnemyTelegraphGeometry()
    {
        if (owner == null || !enemyIndicatorEnabled) return default;

        Vector3 forward = ResolveAimDirection();
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
        forward.Normalize();

        float length = ScaleAbilityRadius(mistRange);
        float width = ScaleAbilityRadius(mistWidth);
        return new EnemyTelegraphGeometry
        {
            shape = EnemyIndicatorShape.Rect,
            center = owner.transform.position + forward * (length * 0.5f),
            forward = forward,
            length = length,
            width = width,
            isValid = length > 0f && width > 0f
        };
    }

    private IEnumerator BossSpreadRoutine()
    {
        if (owner == null)
        {
            EndActivationEffect();
            yield break;
        }

        BossSevenfoldActor boss = owner as BossSevenfoldActor;
        Vector3 origin = owner.transform.position;
        Vector3 center = boss.GetBossTargetPosition() - origin;
        center.y = 0f;
        if (center.sqrMagnitude < 0.0001f) center = ResolveAimDirection();
        else center.Normalize();
        int count = Mathf.Clamp(boss.CombatPhase, 1, 3);
        const float spread = 24f;
        Animator anim = owner.GetActiveAnimator();
        if (anim != null) anim.SetTrigger("Basic");

        for (int i = 0; i < count; i++)
        {
            float angle = count == 1 ? 0f : Mathf.Lerp(-spread, spread, i / (float)(count - 1));
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * center;
            StartCoroutine(AttackRoutine(origin, direction, 1f, false));
        }

        yield return null;
        EndActivationEffect();
    }

    /// <summary>Anchor mirror cast: no HP cost / no recurse / independent hit registry.</summary>
    public void TriggerMirrorFromAnchor(Vector3 origin, Vector3 direction, float damageMul)
    {
        if (owner == null) return;
        StartCoroutine(AttackRoutine(origin, direction, damageMul, false));
    }

    private IEnumerator AttackRoutine(Vector3 origin, Vector3 direction, float damageMul, bool canMirror)
    {
        if (owner == null)
        {
            EndActivationEffect();
            yield break;
        }

        if (canMirror && owner.isPossessed && TryGetPossessedMouseDirection(out Vector3 aim))
        {
            yield return StartCoroutine(RotatePossessedOwnerTowards(aim, aimTurnSpeed));
            direction = aim;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = owner.transform.forward;
        direction.Normalize();

        var anim = owner.GetActiveAnimator();
        if (anim != null && canMirror) anim.SetTrigger("Basic");

        float baseDamage = damage > 0f ? damage : segmentDamage;
        if (owner.isPossessed && possessedDamageOverride > 0f)
            baseDamage = possessedDamageOverride;
        float dmg = baseDamage * damageMul;
        if (IsUpgradeUnlocked("LU-A04"))
            yield return SixWayRingRoundTripRoutine(origin, dmg);
        else
            yield return RoundTripRoutine(origin, direction, dmg);



        if (canMirror && IsUpgradeUnlocked("LU-M05"))
            StartCoroutine(MirrorRoutine(direction));

        if (canMirror) EndActivationEffect();
    }

    private IEnumerator RoundTripRoutine(Vector3 origin, Vector3 direction, float dmg)
    {
        Vector3 end = origin + direction * ScaleAbilityRadius(mistRange);
        HashSet<int> outboundHits = new HashSet<int>();
        yield return TravelSegment(origin, end, dmg, outboundHits);
        HashSet<int> returnHits = new HashSet<int>();
        yield return TravelSegment(end, origin, dmg, returnHits);
    }

    private IEnumerator TravelSegment(Vector3 from, Vector3 to, float dmg, HashSet<int> hitIds)
    {
        float distance = Vector3.Distance(from, to);
        float duration = Mathf.Max(0.05f, distance / Mathf.Max(0.1f, mistSpeed));
        GameObject mist = SpawnMistVisual(from, Quaternion.LookRotation((to - from).normalized, Vector3.up));

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += AbilityDeltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            Vector3 pos = Vector3.Lerp(from, to, u);
            if (mist != null) mist.transform.position = pos;
            RegisterHitsAlong(from, pos, mistWidth * 0.5f, dmg, hitIds);
            yield return null;
        }

        RegisterHitsAlong(from, to, mistWidth * 0.5f, dmg, hitIds);
        if (mist != null) Object.Destroy(mist, 0.05f);
    }

    private IEnumerator SixWayRingRoundTripRoutine(Vector3 origin, float dmg)
    {
        const int directionCount = 6;
        for (int i = 0; i < directionCount; i++)
        {
            Vector3 direction = Quaternion.Euler(0f, i * (360f / directionCount), 0f) * Vector3.forward;
            StartCoroutine(RoundTripRoutine(origin, direction, dmg));
        }

        yield return RingDamageRoutine(origin, dmg);

        float roundTripDuration = ScaleAbilityRadius(mistRange) * 2f / Mathf.Max(0.1f, mistSpeed);
        float ringDuration = GetCardParameter("T", ringExpandDuration);
        if (roundTripDuration > ringDuration)
            yield return AbilityWait(roundTripDuration - ringDuration);
    }

    private IEnumerator RingDamageRoutine(Vector3 origin, float dmg)
    {
        float r0 = GetCardParameter("R0", ringInnerRadius);
        float r1 = GetCardParameter("R1", ringOuterRadius);
        float expand = GetCardParameter("T", ringExpandDuration);
        float width = GetCardParameter("Width", ringWidth);
        HashSet<int> hitIds = new HashSet<int>();

        float elapsed = 0f;
        while (elapsed < expand)
        {
            elapsed += AbilityDeltaTime;
            float u = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, expand));
            float radius = Mathf.Lerp(r0, r1, u);
            RegisterRingHits(origin, radius, width, dmg, hitIds);
            yield return null;
        }

        RegisterRingHits(origin, r1, width, dmg, hitIds);
    }


    private IEnumerator MirrorRoutine(Vector3 direction)
    {
        CacheState();
        if (_state == null || !_state.HasValidAnchor) yield break;
        float delay = GetCardParameter("MirrorDelay", mirrorDelay);
        float mul = GetCardParameter("DmgMul", mirrorDamageMul);
        Vector3 origin = _state.ActiveAnchor.transform.position;
        yield return AbilityWait(delay);
        if (owner == null || _state == null || !_state.HasValidAnchor) yield break;
        TriggerMirrorFromAnchor(origin, direction, mul);
    }

    private void RegisterHitsAlong(Vector3 from, Vector3 to, float radius, float dmg, HashSet<int> hitIds)
    {
        radius = ScaleAbilityRadius(radius);
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist < 0.0001f)
        {
            RegisterSphereHits(to, radius, dmg, hitIds);
            return;
        }

        RaycastHit[] hits = Physics.SphereCastAll(from, radius, delta / dist, dist, ~0, QueryTriggerInteraction.Collide);
        CombatHitboxDebug.DrawCapsule(drawHitboxes, from, to, radius, 0f);
        for (int i = 0; i < hits.Length; i++)
            TryHitCollider(hits[i].collider, dmg, hitIds);

        if (!owner.isPossessed)
            TryHitPlayer(to, radius, dmg, hitIds);
    }

    private void RegisterRingHits(Vector3 origin, float radius, float width, float dmg, HashSet<int> hitIds)
    {
        radius = ScaleAbilityRadius(radius);
        float half = Mathf.Max(0.05f, ScaleAbilityRadius(width * 0.5f));
        CombatHitboxDebug.DrawSphere(drawHitboxes, origin, radius + half, 0f);
        Collider[] hits = Physics.OverlapSphere(origin, radius + half, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            Enemy enemy = hits[i].GetComponentInParent<Enemy>();
            if (enemy == null || owner == null || !owner.CanDamage(enemy)) continue;
            float d = HorizontalDistance(origin, enemy.transform.position);
            if (Mathf.Abs(d - radius) > half) continue;
            TryHitEnemy(enemy, dmg, hitIds);
        }

        if (!owner.isPossessed)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                float d = HorizontalDistance(origin, playerObj.transform.position);
                if (Mathf.Abs(d - radius) <= half)
                    TryHitPlayer(playerObj.transform.position, half, dmg, hitIds);
            }
        }
    }

    private void RegisterSphereHits(Vector3 center, float radius, float dmg, HashSet<int> hitIds)
    {
        radius = ScaleAbilityRadius(radius);
        CombatHitboxDebug.DrawSphere(drawHitboxes, center, radius, 0f);
        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
            TryHitCollider(hits[i], dmg, hitIds);
        if (!owner.isPossessed)
            TryHitPlayer(center, radius, dmg, hitIds);
    }

    private void TryHitCollider(Collider col, float dmg, HashSet<int> hitIds)
    {
        if (col == null) return;
        Enemy enemy = col.GetComponentInParent<Enemy>();
        if (enemy == null || owner == null || !owner.CanDamage(enemy)) return;
        TryHitEnemy(enemy, dmg, hitIds);
    }

    private void TryHitEnemy(Enemy enemy, float dmg, HashSet<int> hitIds)
    {
        if (enemy == null) return;
        int id = enemy.GetInstanceID();
        if (!hitIds.Add(id)) return;
        DealDamageTo(enemy, dmg);
        CacheState();
        _state?.WriteOrRefreshLink(enemy, GetCardParameter("LinkDuration", linkDuration));
        if (IsUpgradeUnlocked("LU-A03"))
            _watchedLinkIds.Add(id);
        if (linkMarkVfx != null)
            Object.Instantiate(linkMarkVfx, enemy.transform.position + Vector3.up * 1.2f, Quaternion.identity, enemy.transform);
    }

    private void TryHitPlayer(Vector3 center, float radius, float dmg, HashSet<int> hitIds)
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;
        if (Vector3.Distance(center, playerObj.transform.position) > radius) return;
        int id = playerObj.GetInstanceID();
        if (!hitIds.Add(id)) return;
        var ph = playerObj.GetComponent<PlayerHealth>();
        if (ph != null) DealDamageToPlayer(ph, dmg);
    }

    private void WatchLinkedDeaths()
    {
        if (_state == null || owner == null) return;
        List<Enemy> linked = _state.GetValidLinkedTargets();
        for (int i = 0; i < linked.Count; i++)
        {
            Enemy target = linked[i];
            if (target == null) continue;
            if (!target.isDowned) continue;
            int id = target.GetInstanceID();
            if (!_watchedLinkIds.Contains(id) && !IsUpgradeUnlocked("LU-A03")) continue;
            Vector3 pos = target.transform.position;
            _state.ClearLink(target);
            _watchedLinkIds.Remove(id);
            StartCoroutine(A03BlastRoutine(pos));
        }
    }

    private IEnumerator A03BlastRoutine(Vector3 pos)
    {
        float delay = GetCardParameter("Delay", a03Delay);
        yield return AbilityWait(delay);
        if (owner == null) yield break;
        float dmg = GetCardParameter("Dmg", a03Damage);
        float radius = GetCardParameter("R", a03Radius);
        if (a03BlastVfx != null)
        {
            GameObject blast = Object.Instantiate(a03BlastVfx, pos + a03BlastVfxOffset, Quaternion.identity);
            blast.transform.localScale *= OwnerCombatScaleMultiplier;
            PlayVfx(blast);
        }
        DamageEnemiesInSphere(pos, radius, dmg);
        if (!owner.isPossessed)
            TryDamagePlayerInRadius(pos, radius, dmg);
    }

    private GameObject SpawnMistVisual(Vector3 pos, Quaternion rot)
    {
        if (mistVfxPrefab != null)
        {
            GameObject mist = Object.Instantiate(mistVfxPrefab, pos, rot);
            mist.transform.localScale *= OwnerCombatScaleMultiplier;
            return mist;
        }
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "PF_MON_LUST_ROUNDTRIP_MIST";
        go.transform.position = pos;
        go.transform.rotation = rot;
        go.transform.localScale = Vector3.one * Mathf.Max(0.3f, mistWidth) * OwnerCombatScaleMultiplier;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        return go;
    }

    private Vector3 ResolveAimDirection()
    {
        if (owner == null) return Vector3.forward;
        if (owner.isPossessed && TryGetPossessedMouseDirection(out Vector3 aim)) return aim;
        if (!owner.isPossessed && owner.targetPlayer != null)
        {
            Vector3 to = owner.targetPlayer.position - owner.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f) return to.normalized;
        }
        return owner.transform.forward;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void CacheState()
    {
        if (owner == null) return;
        _state = owner.GetComponent<LustBodyState>();
        if (_state == null) _state = owner.gameObject.AddComponent<LustBodyState>();
    }

    protected override void OnDisable()
    {
        if (owner != null) owner.IsAbilityFacingLocked = false;
        base.OnDisable();
    }

    public override void ResetForOwnerReuse()
    {
        if (owner != null) owner.IsAbilityFacingLocked = false;
        _watchedLinkIds.Clear();
        CacheState();
        _state?.ClearBodyBoundState();
        base.ResetForOwnerReuse();
    }
}
