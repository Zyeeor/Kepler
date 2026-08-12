using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One snapshot of an ability slot, for HUD display.
/// </summary>
public struct AbilitySlotInfo
{
    public string Name;
    public float CooldownRemaining;
    public float CooldownTotal;
    public float HpCost;
}

/// <summary>
/// Read-only entity view for UI and systems. UI no longer depends on the concrete
/// Enemy / PlayerHealth types. Consumers: PossessionHUD, AbilityCooldownUI, StatsPanelUI.
/// (Architecture decision D3 — read-only view only.)
/// </summary>
public interface IActor
{
    string DisplayName { get; }
    float CurrentHealth { get; }
    float MaxHealth { get; }
    bool IsDowned { get; }            // Equivalent-semantics anchor for WaveManager polling
    bool IsPlayerControlled { get; }
    Transform BodyTransform { get; }

    /// <summary>Fill buffer with ability slot snapshots for the possession HUD.</summary>
    void FillAbilitySlots(List<AbilitySlotInfo> buffer);
}
