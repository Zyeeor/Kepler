using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays a stats panel showing player's base stats and accumulated passive buffs.
/// Attach to a panel under UICanvas.
/// </summary>
public class StatsPanelUI : MonoBehaviour
{
    [Header("Panel")]
    public GameObject panelRoot;             // the whole panel (toggle show/hide)
    public TMP_Text statsText;               // main text showing all stats

    [Header("References (auto-found)")]
    private PlayerPassiveManager passives;
    private PlayerInputController input;
    private PlayerHealth health;
    private PlayerCombat combat;

    void Start()
    {
        passives = PlayerPassiveManager.Instance;
        if (passives == null) passives = FindObjectOfType<PlayerPassiveManager>();

        RefreshPlayerRefs();

        if (panelRoot != null) panelRoot.SetActive(true);
        else Debug.LogWarning("[StatsPanel] panelRoot not assigned in Inspector");
    }

    void Update()
    {
        // Re-fresh player refs every frame in case of possession/unpossession
        RefreshPlayerRefs();

        // Keep stats up to date
        if (panelRoot != null && panelRoot.activeSelf)
        {
            RefreshStats();
        }
    }

    void RefreshPlayerRefs()
    {
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isPossessing && PlayerHealth.Instance.possessedEnemy != null)
        {
            // Possessed — player is the soul but the controlled body is the enemy
            // Stats come from PlayerHealth/PlayerInputController/PlayerCombat (soul)
        }

        if (input == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                input = player.GetComponent<PlayerInputController>();
                health = player.GetComponent<PlayerHealth>();
                combat = player.GetComponent<PlayerCombat>();
            }
        }
    }

    void RefreshStats()
    {
        if (statsText == null) return;

        float baseSpeed = passives != null ? passives.baseMoveSpeed : (input != null ? input.moveSpeed : 5f);
        float speedBonus = passives != null ? passives.totalMoveSpeedBonus : 0f;
        float currentSpeed = baseSpeed * (1f + speedBonus);

        float lifesteal = passives != null ? passives.totalLifestealBonus : 0f;

        float hp = health != null ? health.currentHealth : 0f;
        float maxHp = health != null ? health.soulMaxHealth : 0f;

        float attackSpeed = combat != null ? combat.attackSpeed : 1f;

        int prideCount = passives != null ? Mathf.RoundToInt(speedBonus / 0.05f) : 0;  // each pride = +5%
        int wrathCount = passives != null ? Mathf.RoundToInt(lifesteal / 0.01f) : 0;   // each wrath = +1%

        statsText.text =
            "<b><size=22>Player Stats</size></b>\n\n" +
            "<b>Health</b>          " + Mathf.RoundToInt(hp) + " / " + Mathf.RoundToInt(maxHp) + "\n" +
            "<b>Movement Speed</b>  " + currentSpeed.ToString("F1") +
            (speedBonus > 0f ? "  <color=#00FF00>(+" + (speedBonus * 100f).ToString("F0") + "%</color>)" : "") + "\n" +
            "<b>Attack Speed</b>    " + attackSpeed.ToString("F1") + "\n" +
            "<b>Life Steal</b>      " + (lifesteal * 100f).ToString("F1") + "%\n\n" +

            "<b><size=18>Passive Buffs (Possessed)</size></b>\n" +
            "<color=#FFD700>Pride</color> × " + prideCount + "  +" + (prideCount * 5) + "% Move Speed\n" +
            "<color=#FF4444>Wrath</color> × " + wrathCount + "  +" + (wrathCount * 1) + "% Life Steal";
    }
}
