using UnityEngine;

/// <summary>Summon basic attack: fire a bullet at the nearest valid target.</summary>
public class EnemyAbility_SummonBolt : EnemyAbility
{
    [Header("Projectile")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 16f;
    public float projectileLifetime = 3f;
    // Canonical Sloth drone attack range.
    public float searchRange = 30f;
    public Vector3 muzzleOffset = new Vector3(0f, 0f, 0.4f);

    private void OnEnable()
    {
        type = AbilityType.BasicAttack;
        abilityName = "木灵弹";
        if (cooldown <= 0f) cooldown = 0.5f;
        if (abilityTags == null) abilityTags = new System.Collections.Generic.List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Summon.Bolt", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Summon.Bolt");
    }

    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;
        return FindTargetDirection(out _);
    }

    protected override void OnTrigger()
    {
        if (owner == null || !FindTargetDirection(out Vector3 direction))
        {
            EndActivationEffect();
            return;
        }

        owner.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        Vector3 origin = owner.transform.position + owner.transform.TransformDirection(muzzleOffset);
        SpawnAbilityProjectile(projectilePrefab, origin, Quaternion.LookRotation(direction, Vector3.up), damage, projectileSpeed, projectileLifetime);

        // 木灵实际发射攻击时才播攻击音（无合法目标时 CanTrigger 已挡，不会走到这里，不发射不播）。
        // 走 MonsterSkillAudioConfig 表「无人机」条目：按召唤者七罪类型查表（音频配置中心 → 怪物技能音 → 该罪 → 无人机）。
        // 敌我分轨 / 空间化 / 音量 / 随机多音源均由该条目 ClipSet 控制，与技能条目同构。
        PlayWoodlingAttackSound();

        EndActivationEffect();
    }

    /// <summary>
    /// 播放木灵攻击音：从召唤者（summoner）取七罪类型，查 MonsterSkillAudioConfig 的无人机条目。
    /// 敌我按木灵 isPossessed（同步自召唤者附身状态）分流。召唤者非怪物或 sin=None → 静默。
    /// </summary>
    void PlayWoodlingAttackSound()
    {
        var summon = owner as SummonActor;
        MonsterActor summoner = summon != null ? summon.summoner as MonsterActor : null;
        SinType sin = summoner != null ? summoner.sinType : SinType.None;
        if (sin == SinType.None) return;
        CombatAudioManager.PlayDroneAttackAudio(sin, owner != null && owner.isPossessed, owner.transform.position);
    }

    /// <summary>
    /// 木灵弹攻击音由 PlayWoodlingAttackSound 在 OnTrigger 实际发射弹体时播放（走 MonsterSkillAudioConfig 无人机条目）；
    /// 屏蔽基类单一 cast 音（castAudioName / MonsterSkillAudioConfig 技能查表），
    /// 避免基类 Trigger 在 OnTrigger 之前多播一次（且 SummonActor.sinType=None 技能查表本就静默）。
    /// </summary>
    protected override void PlayCastSound() { }

    private bool FindTargetDirection(out Vector3 direction)
    {
        direction = Vector3.zero;
        if (owner == null) return false;

        if (owner.isPossessed)
        {
            Enemy nearest = null;
            float best = searchRange;
            // 注册表遍历（替代 FindObjectsOfType 场景扫描；CanTrigger 内仅 O(n) 内存过滤）
            foreach (var candidate in EnemyRegistry.All)
            {
                if (candidate == null || !owner.CanDamage(candidate)) continue;
                float distance = Vector3.Distance(owner.transform.position, candidate.transform.position);
                if (distance >= best) continue;
                best = distance;
                nearest = candidate;
            }
            if (nearest == null) return false;
            direction = nearest.transform.position - owner.transform.position;
        }
        else
        {
            if (owner.targetPlayer == null) owner.RefreshPlayerTarget();
            if (owner.targetPlayer == null || !owner.CanDamageSoul()) return false;
            direction = owner.targetPlayer.position - owner.transform.position;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return false;
        direction.Normalize();
        return true;
    }
}
