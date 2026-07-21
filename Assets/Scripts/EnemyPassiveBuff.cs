using UnityEngine;

/// <summary>
/// Passive component on an enemy prefab. Defines permanent stat bonuses + combat effects.
/// Supports: moveSpeed, lifesteal, burn
/// </summary>
public class EnemyPassiveBuff : EnemyAbility
{
    [Header("Stat Bonuses (permanent, stackable)")]
    [Tooltip("Move speed bonus (additive). 5 = +5% move speed.")]
    public float moveSpeedBonusPercent = 0f;
    [Tooltip("Lifesteal bonus (additive). 1 = +1% lifesteal (0.01).")]
    public float lifestealBonusPercent = 0f;
    [Tooltip("Burn bonus (additive). 1 = +1% max HP burn per second (0.01).")]
    public float burnBonusPercent = 0f;

    [Header("Burn VFX (shared)")]
    [Tooltip("Burn VFX prefab spawned on the burning target.")]
    public GameObject burnVfxPrefab;

    private void OnEnable()
    {
        type = AbilityType.Passive;
        abilityName = "被动";
    }

    /// <summary>
    /// Apply burn to a target. burnPercent is the total accumulated value from all passives.
    /// </summary>
    public void OnOwnerDealtDamage(Enemy target, float damageDealt, float totalBurnPercent)
    {
        if (target == null || totalBurnPercent <= 0f) return;
        if (target.GetComponent<BurnEffect>() != null) return;

        var burn = target.gameObject.AddComponent<BurnEffect>();
        burn.Init(target, totalBurnPercent, 3f, 0.5f, burnVfxPrefab);
    }

    protected override void OnTrigger() { }
    public override bool CanTrigger() { return false; }
}

/// <summary>
/// Runtime component attached to a burning enemy. Handles damage ticks and VFX cleanup.
/// </summary>
public class BurnEffect : MonoBehaviour
{
    private Enemy enemy;
    private float burnPercent;
    private float duration;
    private float interval;
    private float timer;
    private float tickTimer;
    private float vfxTimer;
    private GameObject vfxPrefab;

    public void Init(Enemy e, float percent, float dur, float tick, GameObject prefab)
    {
        enemy = e;
        burnPercent = percent;
        duration = dur;
        interval = tick;
        timer = dur;
        tickTimer = 0f;
        vfxTimer = 0f;
        vfxPrefab = prefab;
    }

    void Update()
    {
        if (enemy == null || enemy.isDowned)
        {
            Destroy(this);
            return;
        }

        timer -= Time.deltaTime;
        tickTimer += Time.deltaTime;
        vfxTimer += Time.deltaTime;

        // Spawn burn VFX every 1 second
        if (vfxTimer >= 1f)
        {
            vfxTimer -= 1f;
            if (vfxPrefab != null)
            {
                GameObject vfx = Instantiate(vfxPrefab, enemy.transform.position + Vector3.up * 1f, Quaternion.identity);
                foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>())
                    ps.Play();
                Destroy(vfx, 1f);
            }
        }

        if (tickTimer >= interval)
        {
            float tickDamage = enemy.maxHealth * burnPercent * interval;
            enemy.TakeDamage(tickDamage);
            tickTimer -= interval;
        }

        if (timer <= 0f)
        {
            Destroy(this);
        }
    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
