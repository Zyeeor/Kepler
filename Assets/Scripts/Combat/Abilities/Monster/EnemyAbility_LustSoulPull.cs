using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lust Q: pull all Linked targets toward the active Anchor, deal damage, consume Anchor+Links.
/// Requires valid Anchor + at least one Linked target (fail: no start / no cost / no reload).
/// LU-S05: mutual collision blasts among pulled targets.
/// LU-S06: isolate pulled sources' damage onto the player's Possessed Body.
/// </summary>
public class EnemyAbility_LustSoulPull : EnemyAbility
{
    public float pullWindow = 0.60f;
    public float pullMaxDistance = 6f;
    public float pullDamage = 25f;
    [Tooltip("Possessed Player 专属直接伤害；Enemy 版本仍使用 damage / pullDamage。")]
    public float possessedDamageOverride = 32f;
    public float aimTurnSpeed = 720f;
    public GameObject tetherVfx;
    [Tooltip("tether prefab 的创作长度（粒子锥体长度），用于按 anchor→target 距离归一化缩放。")]
    public float tetherVfxLength = 5f;
    public GameObject impactVfx;
    [Tooltip("冲击 VFX 相对目标位置的偏移。")]
    public Vector3 impactVfxOffset = Vector3.zero;
    public GameObject telegraphVfx;

    [Header("LU-S05")]
    public float s05CollisionDistance = 0.75f;
    public float s05BlastDamage = 25f;
    public float s05BlastRadius = 2f;
    public GameObject s05BlastVfx;
    [Tooltip("LU-S05 碰撞爆炸 VFX 相对爆炸点的偏移。")]
    public Vector3 s05BlastVfxOffset = Vector3.zero;

    [Header("LU-S06")]
    public float s06Grace = 0.15f;

    private LustBodyState _state;
    private bool _gluttonyCopyMode;
    private Vector3 _gluttonyAnchorPosition;
    private float _gluttonyPullRadius;

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "诱引牵魂";
        cooldown = cooldown <= 0f ? 6f : cooldown;
        if (damage <= 0f) damage = pullDamage;
        if (abilityTags == null) abilityTags = new List<string>();
        EnsureTag("Ability.Monster.Lust");
        EnsureTag("Ability.Monster.Lust.SoulPull");
        EnsureUpgradeSlot("LU-S05");
        EnsureUpgradeSlot("LU-S06");
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

    public bool HasValidPullTargets
    {
        get
        {
            CacheState();
            if (_state == null || !_state.HasValidAnchor) return false;
            return _state.GetValidLinkedTargets().Count > 0;
        }
    }

    /// <summary>Configures a copied Lust skill to create its Anchor at the swallowed body's position.</summary>
    public void ConfigureForGluttonyCopy(Vector3 anchorPosition, float radius)
    {
        _gluttonyCopyMode = true;
        _gluttonyAnchorPosition = anchorPosition;
        _gluttonyPullRadius = Mathf.Max(0f, radius);
    }

    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;
        return _gluttonyCopyMode ? CountGluttonyCopyTargets() > 0 : HasValidPullTargets;
    }

    protected override void OnTrigger()
    {
        CacheState();
        bool valid = _gluttonyCopyMode
            ? PrepareGluttonyCopyState()
            : _state != null && _state.HasValidAnchor && _state.GetValidLinkedTargets().Count > 0;
        if (!valid)
        {
            // Spec: failed gate must not charge / reload. Undo the base Trigger cooldown.
            currentCooldown = 0f;
            EndActivationEffect();
            return;
        }

        StartCoroutine(PullRoutine());
    }

    private bool PrepareGluttonyCopyState()
    {
        if (_state == null) return false;
        List<Enemy> targets = CollectGluttonyCopyTargets();
        if (targets.Count == 0) return false;

        _state.ClearAllLinks();
        _state.PlaceOrReplaceAnchor(_gluttonyAnchorPosition, Quaternion.identity);
        for (int i = 0; i < targets.Count; i++)
            _state.WriteOrRefreshLink(targets[i]);
        return _state.HasValidAnchor && _state.GetValidLinkedTargets().Count > 0;
    }

    private List<Enemy> CollectGluttonyCopyTargets()
    {
        List<Enemy> targets = new List<Enemy>();
        if (owner == null) return targets;

        float radiusSqr = _gluttonyPullRadius * _gluttonyPullRadius;
        Vector3 origin = _gluttonyAnchorPosition;
        IReadOnlyList<Enemy> enemies = EnemyRegistry.All;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy candidate = enemies[i];
            if (candidate == null || candidate == owner || !owner.CanDamage(candidate)) continue;
            Vector3 offset = candidate.transform.position - origin;
            offset.y = 0f;
            if (offset.sqrMagnitude <= radiusSqr)
                targets.Add(candidate);
        }
        return targets;
    }

    private int CountGluttonyCopyTargets()
    {
        if (owner == null) return 0;
        float radiusSqr = _gluttonyPullRadius * _gluttonyPullRadius;
        Vector3 origin = _gluttonyAnchorPosition;
        IReadOnlyList<Enemy> enemies = EnemyRegistry.All;
        int count = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy candidate = enemies[i];
            if (candidate == null || candidate == owner || !owner.CanDamage(candidate)) continue;
            Vector3 offset = candidate.transform.position - origin;
            offset.y = 0f;
            if (offset.sqrMagnitude <= radiusSqr) count++;
        }
        return count;
    }

    private IEnumerator PullRoutine()
    {
        if (owner == null)
        {
            EndActivationEffect();
            yield break;
        }

        var anim = owner.GetActiveAnimator();
        if (anim != null) anim.SetTrigger("Skill");

        LustAnchorMarker anchor = _state.ActiveAnchor;
        Vector3 anchorPos = anchor != null ? anchor.transform.position : owner.transform.position;
        List<Enemy> linked = _state.GetValidLinkedTargets();
        if (linked.Count == 0 || anchor == null)
        {
            currentCooldown = 0f;
            EndActivationEffect();
            yield break;
        }

        if (telegraphVfx != null)
            Object.Instantiate(telegraphVfx, anchorPos, Quaternion.identity);

        List<PullTargetState> pulls = new List<PullTargetState>();
        List<MonsterActor> isolationSources = new List<MonsterActor>();
        List<GameObject> tethers = new List<GameObject>();
        for (int i = 0; i < linked.Count; i++)
        {
            Enemy target = linked[i];
            if (target == null || target.isDowned) continue;
            float weightMul = _state.GetPullDistanceMultiplier(target);
            float maxDist = GetCardParameter("PullMaxDistance", pullMaxDistance) * weightMul;
            Vector3 from = target.transform.position;
            Vector3 toAnchor = anchorPos - from;
            toAnchor.y = 0f;
            Vector3 destination = from;
            if (toAnchor.sqrMagnitude > 0.0001f)
            {
                float travel = Mathf.Min(maxDist, toAnchor.magnitude);
                destination = from + toAnchor.normalized * travel;
                destination.y = from.y;
            }

            IController previous = target.Controller;
            target.SetController(NullController.Instance);
            pulls.Add(new PullTargetState
            {
                enemy = target,
                start = from,
                end = destination,
                previousController = previous,
                damaged = false
            });
            isolationSources.Add(target);

            if (tetherVfx != null)
            {
                GameObject tether = SpawnTether(anchorPos, from);
                if (tether != null) tethers.Add(tether);
            }
        }

        bool isolate = IsUpgradeUnlocked("LU-S06");
        MonsterActor protectedBody = ResolveProtectedBody();
        if (isolate && protectedBody != null)
            LustPullDamageGate.BeginWindow(protectedBody, isolationSources);

        float window = GetCardParameter("PullWindow", pullWindow);
        float elapsed = 0f;
        HashSet<int> collisionUsed = new HashSet<int>();
        bool s05 = IsUpgradeUnlocked("LU-S05");

        while (elapsed < window)
        {
            elapsed += AbilityDeltaTime;
            float u = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, window));
            for (int i = 0; i < pulls.Count; i++)
            {
                PullTargetState state = pulls[i];
                if (state.enemy == null || state.enemy.isDowned) continue;
                Vector3 pos = Vector3.Lerp(state.start, state.end, u);
                state.enemy.transform.position = pos;
            }

            if (s05)
                ProcessCollisions(pulls, collisionUsed);

            yield return null;
        }

        float dmg = damage > 0f ? damage : pullDamage;
        if (owner.isPossessed && possessedDamageOverride > 0f)
            dmg = possessedDamageOverride;
        for (int i = 0; i < pulls.Count; i++)
        {
            PullTargetState state = pulls[i];
            if (state.enemy == null) continue;
            if (!state.enemy.isDowned && !state.damaged)
            {
                DealDamageTo(state.enemy, dmg);
                state.damaged = true;
                if (impactVfx != null)
                    Object.Instantiate(impactVfx, state.enemy.transform.position + impactVfxOffset, Quaternion.identity);
            }

            if (state.previousController != null)
                state.enemy.SetController(state.previousController);
            else
                state.enemy.SetController(NullController.Instance);
        }

        // tether 激光在爆炸（impact）完成后消失
        for (int i = 0; i < tethers.Count; i++)
            if (tethers[i] != null) ReleaseVfx(tethers[i]);

        List<Enemy> consumed = new List<Enemy>();
        for (int i = 0; i < pulls.Count; i++)
            if (pulls[i].enemy != null) consumed.Add(pulls[i].enemy);
        _state.ConsumeLinks(consumed);
        _state.DestroyActiveAnchor();

        if (isolate)
            LustPullDamageGate.EndWindow(GetCardParameter("Grace", s06Grace));

        EndActivationEffect();
    }

    /// <summary>从 anchor（lust clone）向 target（有印记敌人）发射一条 tether 激光，返回实例。</summary>
    private GameObject SpawnTether(Vector3 from, Vector3 to)
    {
        if (tetherVfx == null) return null;
        Vector3 dir = to - from;
        Quaternion rot = dir.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(dir.normalized, Vector3.up)
            : Quaternion.identity;
        GameObject tether = VfxPool.Instance.Spawn(tetherVfx, from, rot);
        BulletTimeController.MarkVfxOrigin(tether, IsOwnedByPlayer);
        ScaleAbilityObject(tether);
        Vector3 scale = tether.transform.localScale;
        scale.z *= dir.magnitude / Mathf.Max(0.01f, tetherVfxLength);
        tether.transform.localScale = scale;
        PlayVfx(tether);
        return tether;
    }

    private void ProcessCollisions(List<PullTargetState> pulls, HashSet<int> used)
    {
        float maxDist = GetCardParameter("Dist", s05CollisionDistance);
        List<(int a, int b, float dist, Vector3 mid)> pairs = new List<(int, int, float, Vector3)>();
        for (int i = 0; i < pulls.Count; i++)
        {
            Enemy a = pulls[i].enemy;
            if (a == null || a.isDowned || used.Contains(a.GetInstanceID())) continue;
            for (int j = i + 1; j < pulls.Count; j++)
            {
                Enemy b = pulls[j].enemy;
                if (b == null || b.isDowned || used.Contains(b.GetInstanceID())) continue;
                float d = HorizontalDistance(a.transform.position, b.transform.position);
                bool contact = d <= maxDist;
                if (!contact)
                {
                    // Soft contact fallback via collider proximity already covered by distance.
                    contact = false;
                }
                if (!contact) continue;
                Vector3 mid = (a.transform.position + b.transform.position) * 0.5f;
                pairs.Add((a.GetInstanceID(), b.GetInstanceID(), d, mid));
            }
        }

        pairs.Sort((x, y) => x.dist.CompareTo(y.dist));
        for (int i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            if (used.Contains(pair.a) || used.Contains(pair.b)) continue;
            used.Add(pair.a);
            used.Add(pair.b);
            float dmg = GetCardParameter("CollisionDmg", s05BlastDamage);
            float radius = GetCardParameter("R", s05BlastRadius);
            if (s05BlastVfx != null)
                Object.Instantiate(s05BlastVfx, pair.mid + s05BlastVfxOffset, Quaternion.identity);
            DamageEnemiesInSphere(pair.mid, radius, dmg, null, -1f);
            if (!owner.isPossessed)
                TryDamagePlayerInRadius(pair.mid, radius, dmg, -1f);
        }
    }

    private MonsterActor ResolveProtectedBody()
    {
        // Isolation protects the player's current Possessed Body (when Lust is AI pulling that body,
        // or when another Lust body is pulling while player is elsewhere). Prefer PossessionManager.
        if (PossessionManager.Instance != null && PossessionManager.Instance.CurrentBody != null)
            return PossessionManager.Instance.CurrentBody;
        return null;
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
        LustPullDamageGate.Clear();
        base.OnDisable();
    }

    public override void ResetForOwnerReuse()
    {
        LustPullDamageGate.Clear();
        CacheState();
        _state?.ClearBodyBoundState();
        base.ResetForOwnerReuse();
    }

    private class PullTargetState
    {
        public Enemy enemy;
        public Vector3 start;
        public Vector3 end;
        public IController previousController;
        public bool damaged;
    }
}
