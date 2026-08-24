using UnityEngine;

/// <summary>Seven fixed entries; visual layout is supplied by an independent HUD prefab.</summary>
public sealed class PossessionImprintHUD : MonoBehaviour
{
    public PossessionImprintIcon[] icons = new PossessionImprintIcon[7];
    public PossessionImprintTooltip tooltip;
    public PossessionImprintTutorialPrompt tutorialPrompt;
    PossessionImprintManager manager;

    void OnEnable()
    {
        manager = PossessionImprintManager.EnsureInstance();
        manager.OnImprintChanged += RefreshChanged;
        RefreshAll();
        ShowPendingTutorial();
    }
    void OnDisable()
    {
        if (manager != null) manager.OnImprintChanged -= RefreshChanged;
        if (tooltip != null) tooltip.Hide();
    }
    void RefreshChanged(SinType sin, int stacks)
    {
        Refresh(sin, stacks);
        if (tutorialPrompt != null && !manager.HasSeenTutorial(sin))
        {
            tutorialPrompt.Show(sin);
            manager.MarkTutorialSeen(sin);
        }
    }
    void RefreshAll()
    {
        SinType[] order = { SinType.Pride, SinType.Wrath, SinType.Gluttony, SinType.Greed, SinType.Envy, SinType.Lust, SinType.Sloth };
        for (int i = 0; i < order.Length; i++) Refresh(order[i], manager.GetStacks(order[i]));
    }

    void ShowPendingTutorial()
    {
        if (tutorialPrompt == null || manager.IsRestoredRun) return;
        SinType[] order = { SinType.Pride, SinType.Wrath, SinType.Gluttony, SinType.Greed, SinType.Envy, SinType.Lust, SinType.Sloth };
        for (int i = 0; i < order.Length; i++)
        {
            SinType sin = order[i];
            if (manager.GetStacks(sin) <= 0 || manager.HasSeenTutorial(sin)) continue;
            tutorialPrompt.Show(sin);
            manager.MarkTutorialSeen(sin);
            break;
        }
    }
    void Refresh(SinType sin, int stacks)
    {
        int index = sin == SinType.Pride ? 0 : sin == SinType.Wrath ? 1 : sin == SinType.Gluttony ? 2
            : sin == SinType.Greed ? 3 : sin == SinType.Envy ? 4 : sin == SinType.Lust ? 5 : 6;
        if (icons != null && index >= 0 && index < icons.Length && icons[index] != null)
        {
            icons[index].sin = sin;
            icons[index].Refresh(stacks);
        }
    }
}
