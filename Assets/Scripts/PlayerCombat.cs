using UnityEngine;
using System.Collections.Generic;

public class PlayerCombat : MonoBehaviour
{
    [Header("Combat Settings")]
    public bool enableAutoAttack = true;
    public float autoAttackRange = 3f;
    public float autoAttackInterval = 0.5f;
    public float autoAttackDamage = 10f;
    [Tooltip("Attack speed multiplier. 1.0 = normal. Higher = faster cooldown.")]
    public float attackSpeed = 1.0f;

    [Header("Abilities (auto-discovered from children)")]
    public List<PlayerAbility> basicAbilities = new List<PlayerAbility>();
    public List<PlayerAbility> skillAbilities = new List<PlayerAbility>();
    public List<PlayerAbility> passiveAbilities = new List<PlayerAbility>();

    private float autoAttackTimer;
    private Transform nearestEnemy;
    private PlayerHealth health;
    private bool isControllingEnemy = false;
    private Enemy controlledEnemy;
    private Camera mainCamera;

    void Awake()
    {
        health = GetComponent<PlayerHealth>();
        mainCamera = Camera.main;

        // Auto-discover abilities from children
        var found = GetComponentsInChildren<PlayerAbility>(true);
        passiveAbilities.Clear(); basicAbilities.Clear(); skillAbilities.Clear();
        foreach (var a in found)
        {
            if (a.type == PlayerAbility.AbilityType.BasicAttack && !basicAbilities.Contains(a)) basicAbilities.Add(a);
            else if (a.type == PlayerAbility.AbilityType.Skill && !skillAbilities.Contains(a)) skillAbilities.Add(a);
            else if (a.type == PlayerAbility.AbilityType.Passive && !passiveAbilities.Contains(a)) passiveAbilities.Add(a);
        }

        Debug.Log("[PlayerCombat] Awake: basic=" + basicAbilities.Count + " skill=" + skillAbilities.Count + " passive=" + passiveAbilities.Count);
        for (int i = 0; i < skillAbilities.Count; i++)
            Debug.Log("[PlayerCombat] skillAbilities[" + i + "] = " + (skillAbilities[i] != null ? skillAbilities[i].abilityName : "NULL"));
    }

    public void RegisterAbility(PlayerAbility a)
    {
        if (a == null) return;
        if (a.type == PlayerAbility.AbilityType.BasicAttack && !basicAbilities.Contains(a)) basicAbilities.Add(a);
        else if (a.type == PlayerAbility.AbilityType.Skill && !skillAbilities.Contains(a)) skillAbilities.Add(a);
        else if (a.type == PlayerAbility.AbilityType.Passive && !passiveAbilities.Contains(a)) passiveAbilities.Add(a);
    }

    void Update()
    {
        if (!isControllingEnemy && (health == null || !health.isPossessing)) AutoAttack();
    }

    void AutoAttack()
    {
        if (!enableAutoAttack) return;
        autoAttackTimer -= Time.deltaTime;
        if (autoAttackTimer > 0) return;
        FindNearestEnemy();
        if (nearestEnemy != null)
        {
            float dist = Vector3.Distance(transform.position, nearestEnemy.position);
            if (dist <= autoAttackRange)
            {
                autoAttackTimer = autoAttackInterval;
                var enemy = nearestEnemy.GetComponent<Enemy>();
                if (enemy != null) enemy.TakeDamage(autoAttackDamage);
            }
        }
    }

    void FindNearestEnemy()
    {
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        float minDist = float.MaxValue;
        nearestEnemy = null;
        foreach (var enemy in enemies)
        {
            if (health != null && health.possessedEnemy != null && enemy == health.possessedEnemy.gameObject) continue;
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist < minDist) { minDist = dist; nearestEnemy = enemy.transform; }
        }
    }

    /// <summary>Get direction from player to mouse cursor on ground plane.</summary>
    public Vector3 GetMouseAimDirection()
    {
        if (mainCamera == null) return transform.forward;
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        Vector3 targetPoint;
        if (Physics.Raycast(ray, out hit, 100f))
            targetPoint = hit.point;
        else
        {
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            float dist;
            if (plane.Raycast(ray, out dist))
                targetPoint = ray.GetPoint(dist);
            else
                return transform.forward;
        }
        Vector3 dir = targetPoint - transform.position;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
    }

    /// <summary>Called by PlayerInputController when player presses basic attack key.</summary>
    public void PlayerTriggerBasicAttack()
    {
        TryTriggerAbilitiesOfType(PlayerAbility.AbilityType.BasicAttack);
    }

    /// <summary>Called by PlayerInputController when player presses skill key.</summary>
    public void PlayerTriggerSkill(int index)
    {
        if (index >= 0 && index < skillAbilities.Count)
        {
            var a = skillAbilities[index];
            if (a != null && a.CanTrigger()) a.Trigger();
        }
    }

    /// <summary>Try to trigger the first available ability of the given type.</summary>
    bool TryTriggerAbilitiesOfType(PlayerAbility.AbilityType t)
    {
        var list = t == PlayerAbility.AbilityType.BasicAttack ? basicAbilities : skillAbilities;
        foreach (var a in list)
        {
            if (a != null && a.CanTrigger()) { a.Trigger(); return true; }
        }
        return false;
    }

    /// <summary>Called when player deals damage, for passive effects like lifesteal.</summary>
    public void OnDealtDamage(float amount)
    {
        // Lifesteal from accumulated passive buffs
        if (PlayerPassiveManager.Instance != null && PlayerHealth.Instance != null)
        {
            float lifesteal = PlayerPassiveManager.Instance.GetLifestealMultiplier();
            if (lifesteal > 0f)
            {
                float heal = amount * lifesteal;
                PlayerHealth.Instance.Heal(heal);
            }
        }
    }

    public void OnInteract()
    {
        if (GameManager.Instance != null) GameManager.Instance.SwitchState(GameManager.GameState.BulletTime);
    }

    public void OnPossessionStarted(Enemy enemy)
    {
        isControllingEnemy = true;
        controlledEnemy = enemy;
    }

    public void OnPossessionEnded()
    {
        isControllingEnemy = false;
        controlledEnemy = null;
    }
}
