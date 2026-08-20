using UnityEngine;

/// <summary>
/// Combat summon: Actor with CombatAbilityComponent and HP. Follows its summoner when the
/// summoner is possessed, otherwise follows the soul player. Fires its own BasicAttack abilities.
/// Not possessable; death can optionally explode toward enemies.
/// </summary>
public class SummonActor : Enemy
{
    [Header("Summon")]
    public Actor summoner;
    public float followDistance = 2.2f;
    public float followHeight = 2.4f;
    public float followLerp = 8f;
    public float lifetime = 4f;
    public bool explodeOnDeath;
    [Tooltip("Explode once closer than this to the pursued enemy.")]
    public float kamikazeTriggerDistance = 1.5f;
    public float deathExplosionRadius = 3f;
    public float deathExplosionDamage = 20f;
    public float kamikazeSpeed = 14f;
    public GameObject deathExplosionVfx;
    public float deathExplosionVfxDuration = 1f;
    public float autoAttackInterval = 0.5f;

    private float spawnedAt;
    private float nextAttackAt;
    private bool consumed;
    private bool diving;

    protected override IController CreateDefaultController()
    {
        return NullController.Instance;
    }

    protected override void Awake()
    {
        base.Awake();
        if (Combat != null) Combat.AddLooseTags(this, new[] { "Actor.Summon" });
        spawnedAt = Time.time;
        corpsePossessionWindow = 0f;
        showHealthBar = true;
    }

    protected override void Start()
    {
        gameObject.layer = 8;
        gameObject.tag = "Enemy";
        RefreshPlayerTarget();
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(showHealthBar && ShowHealthBars);
        UpdateHealthUI();
    }

    public void Bind(
        Actor owner,
        float duration,
        bool deathBlast,
        float blastDamage,
        float triggerDistance,
        float blastRadius,
        float diveSpeed,
        GameObject blastVfx,
        float blastVfxDuration)
    {
        summoner = owner;
        lifetime = duration;
        explodeOnDeath = deathBlast;
        deathExplosionDamage = blastDamage;
        kamikazeTriggerDistance = Mathf.Max(0.1f, triggerDistance);
        deathExplosionRadius = Mathf.Max(0.1f, blastRadius);
        kamikazeSpeed = Mathf.Max(0.1f, diveSpeed);
        deathExplosionVfx = blastVfx;
        deathExplosionVfxDuration = Mathf.Max(0.01f, blastVfxDuration);
        spawnedAt = Time.time;
        SyncFaction();
    }

    protected override void Update()
    {
        if (consumed) return;
        if (diving)
        {
            TickDive();
            return;
        }

        SyncFaction();
        base.Update();
        if (isDowned || Body == BodyState.Fading || Body == BodyState.Despawned) return;

        if (lifetime > 0f && Time.time >= spawnedAt + lifetime)
            BeginDeathDive();
        else
            TryAutoAttack();
    }

    protected override void FixedUpdate()
    {
        if (consumed || diving) return;
        base.FixedUpdate();
    }

    protected override void ExecuteMovement(in ControlCommand cmd)
    {
        if (consumed || diving) return;

        Transform follow = GetFollowTarget();
        if (follow == null) return;

        Vector3 back = follow.forward;
        back.y = 0f;
        if (back.sqrMagnitude < 0.0001f) back = Vector3.back;
        back.Normalize();

        Vector3 desired = follow.position - back * followDistance + Vector3.up * followHeight;
        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followLerp * Time.deltaTime));

        Vector3 look = GetLookDirection(follow);
        if (look.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look, Vector3.up), Time.deltaTime * 10f);
    }

    protected override void ExecuteButtons(in ControlCommand cmd)
    {
    }

    protected override void Die()
    {
        BeginDeathDive();
    }

    private void BeginDeathDive()
    {
        if (consumed || diving) return;
        isDowned = true;
        if (!explodeOnDeath)
        {
            Despawn(explode: false);
            return;
        }

        diving = true;
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    public override void TakeDamage(float amount)
    {
        if (consumed || isDowned || Body == BodyState.Fading || Body == BodyState.Despawned) return;
        if (IsUntargetable(this) || IsDamageImmune(this)) return;
        if (amount <= 0f) return;
        currentHealth -= amount;
        FlashDamage();
        UpdateHealthUI();
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }
    }

    private void TryAutoAttack()
    {
        if (Time.time < nextAttackAt) return;
        bool fired = false;
        foreach (var entry in basicAbilities)
        {
            if (entry == null || entry.ability == null || !entry.ability.CanTrigger()) continue;
            entry.ability.Trigger();
            fired = true;
        }
        if (fired) nextAttackAt = Time.time + Mathf.Max(0.05f, autoAttackInterval);
    }

    private void SyncFaction()
    {
        MonsterActor monster = summoner as MonsterActor;
        isPossessed = monster != null && monster.isPossessed;
    }

    private Transform GetFollowTarget()
    {
        MonsterActor monster = summoner as MonsterActor;
        if (monster != null && monster.isPossessed) return monster.transform;
        if (targetPlayer == null) RefreshPlayerTarget();
        return targetPlayer != null ? targetPlayer : (summoner != null ? summoner.transform : null);
    }

    private Vector3 GetLookDirection(Transform follow)
    {
        if (isPossessed)
        {
            Enemy nearest = null;
            float best = 24f;
            foreach (var candidate in EnemyRegistry.All)   // 注册表（替代 FindObjectsOfType 全场景扫描）
            {
                if (candidate == null || !CanDamage(candidate)) continue;
                float distance = Vector3.Distance(transform.position, candidate.transform.position);
                if (distance >= best) continue;
                best = distance;
                nearest = candidate;
            }
            if (nearest != null)
            {
                Vector3 toEnemy = nearest.transform.position - transform.position;
                toEnemy.y = 0f;
                if (toEnemy.sqrMagnitude > 0.0001f) return toEnemy.normalized;
            }
        }
        else if (targetPlayer != null)
        {
            Vector3 toPlayer = targetPlayer.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f) return toPlayer.normalized;
        }

        Vector3 fallback = follow.forward;
        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
    }

    private Enemy FindKamikazeTarget(float maxRange = 80f)
    {
        Enemy result = null;
        float best = maxRange;
        foreach (var candidate in EnemyRegistry.All)
        {
            if (candidate == null || candidate == this || candidate == summoner) continue;
            if (candidate is SummonActor) continue;
            if (candidate.isDowned || candidate.Body == BodyState.Fading || candidate.Body == BodyState.Despawned) continue;
            float distance = HorizontalDistance(candidate.transform.position);
            if (distance >= best) continue;
            best = distance;
            result = candidate;
        }
        return result;
    }

    private float HorizontalDistance(Vector3 worldPos)
    {
        Vector3 a = transform.position;
        Vector3 b = worldPos;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void TickDive()
    {
        Enemy target = FindKamikazeTarget();
        if (target == null)
        {
            Despawn(explode: true);
            return;
        }

        Vector3 dest = target.transform.position + Vector3.up * 0.6f;
        transform.position = Vector3.MoveTowards(transform.position, dest, kamikazeSpeed * Time.deltaTime);
        Vector3 look = dest - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);

        if (HorizontalDistance(target.transform.position) < kamikazeTriggerDistance)
            Despawn(explode: true);
    }

    private void Despawn(bool explode)
    {
        if (consumed) return;
        consumed = true;
        if (explode) Explode();
        Destroy(gameObject);
    }

    private void Explode()
    {
        if (deathExplosionVfx != null)
        {
            GameObject vfx = Instantiate(deathExplosionVfx, transform.position, Quaternion.identity);
            foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                if (main.loop) main.loop = false;
                ps.Play(true);
            }
            Destroy(vfx, Mathf.Max(0.01f, deathExplosionVfxDuration));
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, deathExplosionRadius, ~0, QueryTriggerInteraction.Collide);
        CombatHitboxDebug.DrawSphere(true, transform.position, deathExplosionRadius, deathExplosionVfxDuration);
        foreach (Collider hit in hits)
        {
            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (CanDamage(enemy))
                ApplyOffensiveDamage(enemy, deathExplosionDamage);

            PlayerHealth player = hit.GetComponentInParent<PlayerHealth>();
            if (player != null && CanDamageSoul())
                player.TakeDamage(deathExplosionDamage);
        }
    }
}
