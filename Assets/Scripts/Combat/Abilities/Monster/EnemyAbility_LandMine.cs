using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Skill: Land Mine. Places a mine that persists independently after the owner dies/unpossesses.
/// </summary>
public class EnemyAbility_LandMine : EnemyAbility
{
    [Header("Mine")]
    public GameObject minePrefab;
    public float mineDuration = 10f;
    public int maxMines = 3;

    [Header("Explosion")]
    public float blastRadius = 3f;
    public float triggerRadius = 1.5f;
    public GameObject blastVfxPrefab;
    public float blastVfxDuration = 1f;

    [Header("Damage")]
    public float damageMultiplier = 1.5f;

    [Header("Animation")]
    public string animTrigger = "Skill";

    private List<MineBehaviour> activeMines = new List<MineBehaviour>();

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "地雷";
        cooldown = cooldown <= 0f ? 5f : cooldown;
    }

    public override bool CanTrigger()
    {
        if (owner.isPossessed)
            return base.CanTrigger();
        return base.CanTrigger() && owner != null && owner.targetPlayer != null;
    }

    protected override void OnTrigger()
    {
        if (owner == null) return;
        var anim = owner.GetComponent<Animator>();
        if (anim != null) anim.SetTrigger(animTrigger);
        PlaceMine();
    }

    void PlaceMine()
    {
        // Remove oldest if at max
        while (activeMines.Count >= maxMines)
        {
            var oldest = activeMines[0];
            activeMines.RemoveAt(0);
            if (oldest != null) Destroy(oldest.gameObject);
        }

        GameObject mineGo;
        if (minePrefab != null)
            mineGo = Instantiate(minePrefab, owner.transform.position, Quaternion.identity);
        else
            mineGo = new GameObject("Mine");

        mineGo.transform.localScale *= OwnerCombatScaleMultiplier;

        var mine = mineGo.GetComponent<MineBehaviour>();
        if (mine == null) mine = mineGo.AddComponent<MineBehaviour>();

        mine.lifetime = mineDuration;
        mine.triggerRadius = ScaleAbilityRadius(triggerRadius);
        mine.blastRadius = ScaleAbilityRadius(blastRadius);
        mine.damage = damage * damageMultiplier;
        mine.placer = owner;
        mine.blastVfxPrefab = blastVfxPrefab;
        mine.blastVfxDuration = blastVfxDuration;
        mine.onExplode = (go) => { activeMines.Remove(mine); };

        activeMines.Add(mine);
    }
}
