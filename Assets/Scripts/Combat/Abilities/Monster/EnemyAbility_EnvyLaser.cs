using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Envy Attack: continuous laser that writes target Marks.
/// Possessed aims by mouse; Enemy AI locks the current player (never highest-HP).
/// Ported beam VFX/tick loop from legacy EnemyAbility_Laser; Mark/cashout rules are Canonical.
/// </summary>
public class EnemyAbility_EnvyLaser : EnemyAbility
{
    public const string AbilityTag = "Ability.Monster.Envy.Laser";
    public const string GuardBlockTag = "State.Defense.GreedGuard";

    [Header("Laser")]
    public float maxRange = 15f;
    [Tooltip("Baseline DPS before EN-A05 ramp (Canonical 2/sec).")]
    public float damagePerSecond = 2f;
    public float tickInterval = 0.25f;
    [Tooltip("Baseline Mark storage cap before EN-R01 (TUNABLE).")]
    public float markStorageCap = 100f;
    [Tooltip("Baseline write ratio of effective damage into Mark (Canonical 20%).")]
    public float markWriteRatio = 0.2f;
    [Tooltip("EN-R04 grace seconds after disconnect. 0 = clear immediately.")]
    public float markGraceDuration = 0f;
    [Tooltip("Baseline max continuous connect window. EN-A04 raises via ConnectDuration.")]
    public float maxConnectDuration = 8f;
    public GameplayEffectDefinition markEffect;
    public GameplayEffectDefinition laserHitEffect;

    [Header("EN-A05 Ramp")]
    public float rampMaxDamagePerSecond = 50f;
    public float rampTimeToMax = 8f;

    [Header("EN-A01 Multi Eye")]
    public float multiEyeInterval = 3f;
    public float multiEyeWindow = 0.6f;
    public int multiEyeTargetCount = 4;

    [Header("EN-A03 Pierce")]
    public float piercePhaseInterval = 4f;
    public float piercePhaseDuration = 1f;

    [Header("Beam VFX")]
    public GameObject beamPrefab;
    public Material beamMaterial;
    public Vector3 beamPositionOffset = new Vector3(0f, 0.3f, 0f);
    public Vector3 beamRotationOffset = Vector3.zero;

    [Header("Hit VFX")]
    public GameObject hitImpactPrefab;
    public float hitImpactDuration = 0.3f;

    private bool _isFiring;
    private float _damageTimer;
    private float _fireDuration;
    private float _hpCostTimer;
    private float _multiEyeTimer;
    private float _pierceTimer;
    private GameObject _hitVfx;
    private readonly HashSet<Enemy> _connectedThisBurst = new HashSet<Enemy>();
    private readonly List<Enemy> _lastMarked = new List<Enemy>();

    private void OnEnable()
    {
        type = AbilityType.BasicAttack;
        abilityName = "激光";
        cooldown = cooldown < 0f ? 0f : cooldown;
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, AbilityTag, System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add(AbilityTag);
    }

    protected override void Update()
    {
        base.Update();
        if (owner == null) return;

        bool wantFire;
        if (owner.isPossessed)
            wantFire = Input.GetMouseButton(0) && !PlayerController.IsGameplayInputBlocked && Time.timeScale > 0f;
        else
            wantFire = owner.targetPlayer != null
                       && Vector3.Distance(owner.transform.position, owner.targetPlayer.position) <= GetEffectiveRange();

        if (wantFire && CanTrigger())
        {
            if (!_isFiring)
            {
                if (!TryBeginActivationEffect()) return;
                _isFiring = true;
                _damageTimer = 0f;
                _fireDuration = 0f;
                _hpCostTimer = 0f;
                _multiEyeTimer = 0f;
                _pierceTimer = 0f;
                _connectedThisBurst.Clear();
                currentCooldown = 0f;
            }

            UpdateLaser();
        }
        else if (_isFiring)
        {
            StopLaser();
        }

        Animator anim = owner.GetActiveAnimator();
        if (anim != null)
        {
            foreach (AnimatorControllerParameter p in anim.parameters)
            {
                if (p.name == "IsFiring")
                {
                    anim.SetBool("IsFiring", _isFiring);
                    break;
                }
            }
        }
    }

    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;
        return owner != null && (owner.isPossessed || owner.targetPlayer != null);
    }

    protected override void OnTrigger() { }

    public void ApplyMarkTo(Enemy target, bool dealDamage)
    {
        if (target == null || owner == null || !owner.CanDamage(target)) return;
        EnvyMarkTarget mark = EnvyMarkTarget.EnsureOn(target);
        if (mark == null) return;

        float cap = markStorageCap;
        if (IsUpgradeUnlocked("EN-R01"))
            cap *= 1f + GetCardParameter("MarkStorageBonus", 0.5f);

        float ratio = GetCardParameter("MarkWriteRatio", markWriteRatio);
        if (IsUpgradeUnlocked("EN-R02"))
            ratio = GetCardParameter("MarkWriteRatio", Mathf.Min(1f, markWriteRatio + 0.15f));

        float grace = markGraceDuration;
        if (IsUpgradeUnlocked("EN-R04"))
            grace = GetCardParameter("MarkGraceDuration", Mathf.Max(1.5f, markGraceDuration));

        mark.ApplyOrRefresh(owner, cap, ratio, grace, markEffect);
        mark.CancelGraceKeepMark();
        if (!_lastMarked.Contains(target)) _lastMarked.Add(target);
    }

    private void UpdateLaser()
    {
        float connectCap = GetCardParameter("ConnectDuration", maxConnectDuration);
        if (IsUpgradeUnlocked("EN-A04"))
            connectCap = GetCardParameter("ConnectDuration", maxConnectDuration + 4f);

        _fireDuration += AbilityDeltaTime;
        if (_fireDuration > connectCap)
        {
            StopLaser();
            currentCooldown = EffectiveCooldown;
            return;
        }

        _hpCostTimer += AbilityDeltaTime;
        if (_hpCostTimer >= 1f)
        {
            owner.PayAbilityHpCost(this);
            _hpCostTimer -= 1f;
        }

        Vector3 origin = GetBeamOrigin();
        Vector3 aimPoint = GetAimPoint(origin);
        bool pierceActive = IsPierceActive();
        float tickDamage = GetTickDamage();

        _damageTimer += AbilityDeltaTime;
        _multiEyeTimer += AbilityDeltaTime;
        _pierceTimer += AbilityDeltaTime;

        if (_damageTimer < tickInterval) return;
        _damageTimer -= tickInterval;

        List<Enemy> hitTargets = new List<Enemy>();
        Vector3 beamEnd = aimPoint;
        bool blocked = ResolveBeamHits(origin, aimPoint, pierceActive, hitTargets, out beamEnd);

        SpawnBeamVfx(origin, beamEnd);

        bool anyLegalHit = false;
        for (int i = 0; i < hitTargets.Count; i++)
        {
            Enemy target = hitTargets[i];
            if (target == null) continue;
            anyLegalHit = true;
            DealDamageTo(target, tickDamage);
            ApplyMarkTo(target, dealDamage: true);
            _connectedThisBurst.Add(target);
            if (laserHitEffect != null && target.Combat != null)
                target.Combat.ApplyEffect(laserHitEffect, owner.Combat, abilityTags, out _);
        }

        if (!owner.isPossessed && owner.targetPlayer != null && hitTargets.Count == 0 && !blocked)
        {
            // AI beam aimed at player may miss colliders; still settle soul damage on line-of-sight range.
            float dist = Vector3.Distance(origin, owner.targetPlayer.position);
            if (dist <= GetEffectiveRange())
            {
                PlayerHealth ph = owner.targetPlayer.GetComponent<PlayerHealth>();
                if (ph != null) DealDamageToPlayer(ph, tickDamage);
            }
        }

        if (!anyLegalHit)
        {
            // Empty fire: keep beam, but do not write Mark / ramp EN-A05.
            _connectedThisBurst.Clear();
            _fireDuration = 0f;
        }

        if (IsUpgradeUnlocked("EN-A01") && _multiEyeTimer >= multiEyeInterval)
        {
            _multiEyeTimer = 0f;
            FireMultiEye(origin, tickDamage, hitTargets);
        }

        UpdateHitVfx(beamEnd);
    }

    private bool IsPierceActive()
    {
        if (!IsUpgradeUnlocked("EN-A03")) return false;
        float window = GetCardParameter("PierceDuration", piercePhaseDuration);
        float interval = Mathf.Max(window + 0.1f, GetCardParameter("PierceInterval", piercePhaseInterval));
        float cycle = _pierceTimer % interval;
        return cycle <= window;
    }

    private void FireMultiEye(Vector3 origin, float tickDamage, List<Enemy> primaryHits)
    {
        List<Enemy> candidates = new List<Enemy>();
        foreach (Enemy e in FindObjectsOfType<Enemy>())
        {
            if (e == null || !owner.CanDamage(e)) continue;
            if (Vector3.Distance(origin, e.transform.position) > GetEffectiveRange()) continue;
            candidates.Add(e);
        }

        candidates.Sort((a, b) =>
            Vector3.Distance(origin, a.transform.position).CompareTo(Vector3.Distance(origin, b.transform.position)));

        HashSet<Enemy> chosen = new HashSet<Enemy>();
        for (int i = 0; i < primaryHits.Count; i++)
        {
            if (primaryHits[i] != null) chosen.Add(primaryHits[i]);
        }

        for (int i = 0; i < candidates.Count && chosen.Count < multiEyeTargetCount; i++)
            chosen.Add(candidates[i]);

        foreach (Enemy target in chosen)
        {
            if (primaryHits.Contains(target)) continue;
            Vector3 end = target.transform.position + Vector3.up;
            SpawnBeamVfx(origin, end);
            DealDamageTo(target, tickDamage);
            ApplyMarkTo(target, dealDamage: true);
        }
    }

    private bool ResolveBeamHits(Vector3 origin, Vector3 aimPoint, bool pierce, List<Enemy> hits, out Vector3 beamEnd)
    {
        hits.Clear();
        Vector3 dir = aimPoint - origin;
        float maxDist = Mathf.Min(dir.magnitude, GetEffectiveRange());
        if (maxDist < 0.01f)
        {
            beamEnd = origin + owner.transform.forward * 0.1f;
            return false;
        }

        dir.Normalize();
        CombatHitboxDebug.DrawRay(drawHitboxes, origin, dir, maxDist, 0f);
        RaycastHit[] results = Physics.RaycastAll(origin, dir, maxDist, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(results, (a, b) => a.distance.CompareTo(b.distance));

        bool blocked = false;
        beamEnd = origin + dir * maxDist;
        for (int i = 0; i < results.Length; i++)
        {
            RaycastHit hit = results[i];
            CombatAbilityComponent combat = hit.collider.GetComponentInParent<CombatAbilityComponent>();
            if (combat != null && combat.Tags != null && combat.Tags.HasTag(GuardBlockTag))
            {
                // Guard truncates; truncated segment does not write Mark.
                beamEnd = hit.point;
                blocked = true;
                break;
            }

            Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy == null || !owner.CanDamage(enemy)) continue;
            if (!hits.Contains(enemy)) hits.Add(enemy);
            beamEnd = hit.point;
            if (!pierce) break;
        }

        return blocked;
    }

    private float GetTickDamage()
    {
        float dps = damagePerSecond;
        if (IsUpgradeUnlocked("EN-A05") && _connectedThisBurst.Count > 0)
        {
            float t = Mathf.Clamp01(_fireDuration / Mathf.Max(0.01f, GetCardParameter("RampTime", rampTimeToMax)));
            float maxDps = GetCardParameter("RampMaxDps", rampMaxDamagePerSecond);
            dps = Mathf.Lerp(damagePerSecond, maxDps, t);
        }

        return dps * tickInterval;
    }

    private float GetEffectiveRange()
    {
        float range = maxRange;
        if (IsUpgradeUnlocked("EN-TG01"))
            range += GetCardParameter("AttackRangeBonus", 4f);
        return range;
    }

    private Vector3 GetBeamOrigin()
    {
        return owner.transform.position + Vector3.up + beamPositionOffset;
    }

    private Vector3 GetAimPoint(Vector3 origin)
    {
        float range = GetEffectiveRange();
        if (owner.isPossessed && PlayerController.Instance != null
            && PlayerController.Instance.TryGetAimPoint(out Vector3 aim))
        {
            Vector3 flat = aim;
            flat.y = origin.y;
            Vector3 delta = flat - origin;
            if (delta.sqrMagnitude < 0.01f) delta = owner.transform.forward;
            if (delta.magnitude > range) delta = delta.normalized * range;
            return origin + delta;
        }

        if (owner.targetPlayer != null)
        {
            Vector3 target = owner.targetPlayer.position + Vector3.up;
            Vector3 delta = target - origin;
            if (delta.magnitude > range) delta = delta.normalized * range;
            return origin + delta;
        }

        return origin + owner.transform.forward * range;
    }

    private void SpawnBeamVfx(Vector3 origin, Vector3 targetPos)
    {
        if (beamPrefab == null) return;
        Vector3 dir = targetPos - origin;
        Quaternion rot = (dir.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(dir.normalized, Vector3.up)
            : Quaternion.identity) * Quaternion.Euler(beamRotationOffset);
        GameObject vfx = SpawnVfxTracked(beamPrefab, origin, rot, tickInterval);
        if (vfx == null) return;
        Vector3 scale = vfx.transform.localScale;
        scale.z *= Mathf.Max(0.1f, dir.magnitude);
        vfx.transform.localScale = scale;

        if (beamMaterial != null)
        {
            foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null) renderer.material = beamMaterial;
            }
        }
    }

    private void UpdateHitVfx(Vector3 hitPos)
    {
        if (hitImpactPrefab == null) return;
        if (_hitVfx == null) _hitVfx = SpawnVfxTracked(hitImpactPrefab, hitPos, Quaternion.identity);
        else _hitVfx.transform.position = hitPos;
    }

    private void StopLaser()
    {
        _isFiring = false;
        EndActivationEffect();

        float grace = markGraceDuration;
        if (IsUpgradeUnlocked("EN-R04"))
            grace = GetCardParameter("MarkGraceDuration", Mathf.Max(1.5f, markGraceDuration));

        for (int i = 0; i < _lastMarked.Count; i++)
        {
            Enemy e = _lastMarked[i];
            if (e == null) continue;
            EnvyMarkTarget mark = e.GetComponent<EnvyMarkTarget>();
            if (mark == null || mark.Source != owner) continue;
            if (grace > 0f) mark.BeginGrace();
            else mark.Clear();
        }

        _lastMarked.Clear();
        _connectedThisBurst.Clear();
        if (_hitVfx != null)
        {
            Destroy(_hitVfx, hitImpactDuration);
            _hitVfx = null;
        }
    }

    protected override void OnDisable()
    {
        if (_isFiring) StopLaser();
        if (owner != null)
            EnvyMarkTarget.ClearMarksFromSource(owner);
        base.OnDisable();
    }
}
