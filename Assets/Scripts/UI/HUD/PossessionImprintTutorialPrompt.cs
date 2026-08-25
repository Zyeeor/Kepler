using UnityEngine;
using UnityEngine.UI;

public sealed class PossessionImprintTutorialPrompt : MonoBehaviour
{
    public GameObject panel;
    public Text titleText;
    public Text bodyText;
    public float displaySeconds = 4f;
    float hideAt;
    public void Show(SinType sin)
    {
        if (panel != null) panel.SetActive(true);
        int stacks = PossessionImprintManager.Instance != null
            ? PossessionImprintManager.Instance.GetStacks(sin)
            : 1;
        if (titleText != null) titleText.text = TextCatalog.Get("imprint.acquired", PossessionImprintTooltip.GetTitle(sin));
        if (bodyText != null) bodyText.text = PossessionImprintTooltip.GetEffect(sin, stacks)
            + "\n" + TextCatalog.Get("imprint.acquire_hint");
        hideAt = Time.unscaledTime + displaySeconds;
    }
    void Update()
    {
        if (panel != null && panel.activeSelf && Time.unscaledTime >= hideAt) panel.SetActive(false);
    }
}
