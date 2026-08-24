using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 附身 HUD（屏内面板）。
/// 仅负责：显示附身怪名称 + 能力槽提示 (Q/W/R/E 文字)。
/// HP 槽已统一到 PlayerHealth.healthSlider（灵魂态显示 Soul 池、附身态显示 Body 池，
/// 由 PlayerHealth.BindActor / UnbindActor 在 PossessionManager 钩子里切换数据源）。
/// 怪物头顶 World Space HP 条由 MonsterActor.LateUpdate 用 !isPossessed 自动屏蔽。
/// </summary>
public class PossessionHUD : MonoBehaviour
{
    public static PossessionHUD Instance { get; private set; }

    [Header("UI Elements")]
    public GameObject panelRoot;
    public TMP_Text enemyNameText;
    public TMP_Text abilityQText;
    public TMP_Text abilityWText;
    public TMP_Text abilityRText;

    private readonly List<AbilitySlotInfo> abilitySlots = new List<AbilitySlotInfo>();

    void Awake()
    {
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>附身 HUD 只读 IActor 视图（不依赖具体 Enemy/PlayerHealth 类型）。HP 槽已迁出。</summary>
    public void Show(IActor actor)
    {
        if (actor == null) return;
        if (panelRoot != null) panelRoot.SetActive(true);
        if (enemyNameText != null) enemyNameText.text = actor.DisplayName;
        actor.FillAbilitySlots(abilitySlots);
        string basicName = abilitySlots.Count > 0 ? abilitySlots[0].Name : TextCatalog.Get("ui.hud.basic_default");
        string skillName = abilitySlots.Count > 1 ? abilitySlots[1].Name : TextCatalog.Get("ui.hud.skill_default");
        if (abilityQText != null) abilityQText.text = string.Format(TextCatalog.Get("ui.hud.attack"), basicName);
        if (abilityWText != null) abilityWText.text = string.Format(TextCatalog.Get("ui.hud.skill"), skillName);
        if (abilityRText != null) abilityRText.text = TextCatalog.Get("ui.hud.possess");
        // 委托 PlayerHealth 切换 HP 数据源（灵魂态由它显示 Soul 池；附身态切换到 Body 池）
        if (PlayerHealth.Instance != null) PlayerHealth.Instance.BindActor(actor);
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        // 把 HP 数据源交还给 Soul 池
        if (PlayerHealth.Instance != null) PlayerHealth.Instance.UnbindActor();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
