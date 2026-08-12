using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("Combat Settings")]
    [Tooltip("Attack speed multiplier. 1.0 = normal. Higher = faster cooldown.")]
    public float attackSpeed = 1.0f;

    [Header("Abilities")]
    [Tooltip("Soul form exposes only BasicAttack PlayerAbility instances through left-click.")]
    public List<PlayerAbility> basicAbilities = new List<PlayerAbility>();

    private Camera mainCamera;

    void Awake()
    {
        CombatAbilityComponent combatState = GetComponent<CombatAbilityComponent>();
        if (combatState == null) combatState = gameObject.AddComponent<CombatAbilityComponent>();
        combatState.AddLooseTags(this, new[] { "Actor.Soul" });

        mainCamera = Camera.main;
        basicAbilities.Clear();

        PlayerAbility[] found = GetComponentsInChildren<PlayerAbility>(true);
        foreach (PlayerAbility ability in found)
        {
            if (ability.type == PlayerAbility.AbilityType.BasicAttack && !basicAbilities.Contains(ability))
                basicAbilities.Add(ability);
        }

        Debug.Log("[PlayerCombat] Basic abilities=" + basicAbilities.Count);
    }

    public void RegisterAbility(PlayerAbility ability)
    {
        if (ability == null) return;
        if (ability.type == PlayerAbility.AbilityType.BasicAttack && !basicAbilities.Contains(ability))
            basicAbilities.Add(ability);
    }

    /// <summary>Get direction from player to mouse cursor on ground plane.</summary>
    public Vector3 GetMouseAimDirection()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return transform.forward;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Vector3 targetPoint;
        if (Physics.Raycast(ray, out RaycastHit hit, 100f)) targetPoint = hit.point;
        else
        {
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float distance)) return transform.forward;
            targetPoint = ray.GetPoint(distance);
        }

        Vector3 direction = targetPoint - transform.position;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
    }

    public void PlayerTriggerBasicAttack()
    {
        foreach (PlayerAbility ability in basicAbilities)
        {
            if (ability == null || !ability.CanTrigger()) continue;
            ability.Trigger();
            return;
        }
    }

    public void OnDealtDamage(float amount)
    {
        if (PlayerPassiveManager.Instance == null || PlayerHealth.Instance == null) return;

        float lifesteal = PlayerPassiveManager.Instance.GetLifestealMultiplier();
        if (lifesteal > 0f) PlayerHealth.Instance.Heal(amount * lifesteal);
    }
}
