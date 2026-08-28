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
        var pm = PossessionManager.Instance;
        bool possessing = pm != null && pm.State == PossessionManager.SwitchState.Possessing;
        // Possessed — player is the soul but the controlled body is the enemy
        // Stats come from PlayerHealth/PlayerInputController/PlayerCombat (soul)

        if (health == null || combat == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                health = player.GetComponent<PlayerHealth>();
                combat = player.GetComponent<PlayerCombat>();
            }
        }
    }

    void RefreshStats()
    {
        if (statsText == null) return;

        float speedBonus = passives != null ? passives.totalMoveSpeedBonus : 0f;
        float currentSpeed = passives != null ? passives.CurrentMoveSpeed : 5f;

        float lifesteal = passives != null ? passives.totalLifestealBonus : 0f;

        float hp = health != null ? health.currentHealth : 0f;
        float maxHp = health != null ? health.soulMaxHealth : 0f;

        float attackSpeed = combat != null ? combat.attackSpeed : 1f;

        int prideCount = passives != null ? Mathf.RoundToInt(speedBonus / 0.05f) : 0;  // each pride = +5%
        int wrathCount = passives != null ? Mathf.RoundToInt(lifesteal / 0.01f) : 0;   // each wrath = +1%

        // 统一文本目录（TextCatalog）：标题/属性名/被动增益标签汉化，格式模板走 key
        statsText.text =
            "<b><size=22>" + TextCatalog.Get("ui.stats.title") + "</size></b>\n\n" +
            "<b>" + TextCatalog.Get("ui.stats.health") + "</b>          " + Mathf.RoundToInt(hp) + " / " + Mathf.RoundToInt(maxHp) + "\n" +
            "<b>" + TextCatalog.Get("ui.stats.move_speed") + "</b>  " + currentSpeed.ToString("F1") +
            (speedBonus > 0f ? "  <color=#00FF00>(+" + (speedBonus * 100f).ToString("F0") + "%</color>)" : "") + "\n" +
            "<b>" + TextCatalog.Get("ui.stats.attack_speed") + "</b>    " + attackSpeed.ToString("F1") + "\n" +
            "<b>" + TextCatalog.Get("ui.stats.life_steal") + "</b>      " + (lifesteal * 100f).ToString("F1") + "%\n\n" +

            "<b><size=18>" + TextCatalog.Get("ui.stats.passive_buffs") + "</size></b>\n" +
            "<color=#FFD700>" + TextCatalog.Get("concept.sin.pride") + "</color> × " + prideCount + "  +" + (prideCount * 5) + "% " + TextCatalog.Get("ui.stats.move_speed_short") + "\n" +
            "<color=#FF4444>" + TextCatalog.Get("concept.sin.wrath") + "</color> × " + wrathCount + "  +" + (wrathCount * 1) + "% " + TextCatalog.Get("ui.stats.life_steal_short");
    }
}
