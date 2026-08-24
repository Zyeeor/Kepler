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
        if (titleText != null) titleText.text = PossessionImprintTooltip.GetTitle(sin) + "已获得";
        if (bodyText != null) bodyText.text = PossessionImprintTooltip.GetEffect(sin, stacks)
            + "\n每次夺舍该类怪物都会增加一层。";
        hideAt = Time.unscaledTime + displaySeconds;
    }
    void Update()
    {
        if (panel != null && panel.activeSelf && Time.unscaledTime >= hideAt) panel.SetActive(false);
    }
}
