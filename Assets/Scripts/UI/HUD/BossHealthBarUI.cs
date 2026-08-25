using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Persistent top-screen health bar for the active Sevenfold Boss.</summary>
public sealed class BossHealthBarUI : MonoBehaviour
{
    [Min(1f)] public float barWidth = 780f;
    [Min(1f)] public float barHeight = 32f;

    BossSevenfoldActor boss;
    GameObject panel;
    Image fill;
    float lastLoggedHealth = float.NaN;

    public static void ShowFor(BossSevenfoldActor target)
    {
        if (target == null) return;
        Canvas canvas = FindHudCanvas();
        if (canvas == null) return;
        BossHealthBarUI healthBar = canvas.GetComponent<BossHealthBarUI>();
        if (healthBar == null) healthBar = canvas.gameObject.AddComponent<BossHealthBarUI>();
        healthBar.Bind(target);
    }

    public static void HideFor(BossSevenfoldActor target)
    {
        if (target == null) return;
        BossHealthBarUI[] bars = FindObjectsOfType<BossHealthBarUI>();
        for (int i = 0; i < bars.Length; i++)
            if (bars[i] != null && bars[i].boss == target) bars[i].Hide();
    }

    void Awake()
    {
        CreateBar();
        Hide();
    }

    void Bind(BossSevenfoldActor target)
    {
        boss = target;
        lastLoggedHealth = float.NaN;
        if (panel != null) panel.SetActive(true);
        Debug.Log($"[BossHealth] Bound UI to {boss.name}, hp={boss.currentHealth:F1}/{boss.maxHealth:F1}", this);
        Refresh();
    }

    void Hide()
    {
        boss = null;
        lastLoggedHealth = float.NaN;
        if (panel != null) panel.SetActive(false);
    }

    void Update()
    {
        if (boss == null || boss.IsDefeated)
        {
            Hide();
            return;
        }
        Refresh();
    }

    void Refresh()
    {
        if (boss == null || fill == null) return;
        float healthFraction = boss.maxHealth > 0f ? Mathf.Clamp01(boss.currentHealth / boss.maxHealth) : 0f;
        fill.fillAmount = healthFraction;
        fill.rectTransform.anchorMax = new Vector2(healthFraction, 1f);
        fill.enabled = healthFraction > 0f;
        if (float.IsNaN(lastLoggedHealth) || !Mathf.Approximately(lastLoggedHealth, boss.currentHealth))
        {
            Debug.Log($"[BossHealth] UI refreshed: hp={boss.currentHealth:F1}/{boss.maxHealth:F1}, fill={healthFraction:F4}, panelActive={panel != null && panel.activeSelf}, fillWidth={fill.rectTransform.rect.width:F1}", this);
            lastLoggedHealth = boss.currentHealth;
        }
    }

    void CreateBar()
    {
        panel = new GameObject("BossHealthBar", typeof(RectTransform));
        panel.transform.SetParent(transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -22f);
        panelRect.sizeDelta = new Vector2(barWidth + 112f, barHeight + 12f);

        CreateLabel(panel.transform);
        Image background = CreateImage(panel.transform, "Background", new Color(0.02f, 0.003f, 0.008f, 0.96f));
        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(1f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.offsetMin = new Vector2(106f, -barHeight * 0.5f);
        backgroundRect.offsetMax = new Vector2(-6f, barHeight * 0.5f);

        Image bottomBorder = CreateImage(background.transform, "BottomBorder", new Color(0.25f, 0.008f, 0.025f, 1f));
        RectTransform borderRect = bottomBorder.rectTransform;
        borderRect.anchorMin = new Vector2(0f, 0f);
        borderRect.anchorMax = new Vector2(1f, 0f);
        borderRect.pivot = new Vector2(0.5f, 1f);
        borderRect.anchoredPosition = new Vector2(0f, -2f);
        borderRect.sizeDelta = new Vector2(0f, 6f);

        Image borderHighlight = CreateImage(bottomBorder.transform, "WhiteHighlight", new Color(1f, 0.9f, 0.9f, 0.9f));
        RectTransform highlightRect = borderHighlight.rectTransform;
        highlightRect.anchorMin = new Vector2(0f, 1f);
        highlightRect.anchorMax = new Vector2(1f, 1f);
        highlightRect.pivot = new Vector2(0.5f, 1f);
        highlightRect.anchoredPosition = Vector2.zero;
        highlightRect.sizeDelta = new Vector2(0f, 1f);

        fill = CreateImage(background.transform, "Fill", new Color(0.85f, 0.025f, 0.08f, 1f));
        fill.type = Image.Type.Simple;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = new Vector2(4f, 4f);
        fill.rectTransform.offsetMax = new Vector2(-4f, -4f);
    }

    void CreateLabel(Transform parent)
    {
        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, 0f);
        labelRect.sizeDelta = new Vector2(100f, barHeight + 12f);
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = "Boss";
        label.fontSize = 26f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = new Color(1f, 0.55f, 0.72f, 1f);
        label.raycastTarget = false;
        UiFontAssets.ApplyTo(label);
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Canvas FindHudCanvas()
    {
        GameObject namedCanvas = GameObject.Find("UICanvas");
        if (namedCanvas != null)
        {
            Canvas found = namedCanvas.GetComponent<Canvas>();
            if (found != null) return found;
        }

        Canvas[] canvases = FindObjectsOfType<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
            if (canvases[i] != null && canvases[i].isRootCanvas && canvases[i].renderMode != RenderMode.WorldSpace)
                return canvases[i];
        return null;
    }
}
