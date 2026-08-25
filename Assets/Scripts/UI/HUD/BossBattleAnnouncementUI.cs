using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime HUD banner shown when the Sevenfold Boss becomes actionable.</summary>
public sealed class BossBattleAnnouncementUI : MonoBehaviour
{
    const string BossBattleText = "boss战开启，使用场上的七具不朽尸身与之作战！";

    [Min(0.1f)] public float displayDuration = 5f;
    [Min(0.01f)] public float fadeDuration = 0.4f;

    TextMeshProUGUI label;
    CanvasGroup canvasGroup;
    float hideAt;

    public static void ShowBossBattleStart()
    {
        Canvas canvas = FindHudCanvas();
        if (canvas == null) return;
        BossBattleAnnouncementUI announcement = canvas.GetComponent<BossBattleAnnouncementUI>();
        if (announcement == null) announcement = canvas.gameObject.AddComponent<BossBattleAnnouncementUI>();
        announcement.Show();
    }

    void Awake()
    {
        CreateBanner();
        canvasGroup.alpha = 0f;
    }

    void Show()
    {
        if (label == null) CreateBanner();
        ApplyChineseFont();
        label.text = BossBattleText;
        hideAt = Time.unscaledTime + displayDuration;
        canvasGroup.alpha = 1f;
    }

    void Update()
    {
        if (canvasGroup == null || hideAt <= 0f) return;
        float remaining = hideAt - Time.unscaledTime;
        if (remaining <= 0f)
        {
            canvasGroup.alpha = 0f;
            hideAt = 0f;
            return;
        }
        canvasGroup.alpha = Mathf.Clamp01(remaining / fadeDuration);
    }

    void CreateBanner()
    {
        GameObject panel = new GameObject("BossBattleAnnouncement", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panel.transform.SetParent(transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -42f);
        panelRect.sizeDelta = new Vector2(980f, 92f);
        panel.GetComponent<Image>().color = new Color(0.035f, 0.005f, 0.06f, 0.9f);
        canvasGroup = panel.GetComponent<CanvasGroup>();

        GameObject text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(panel.transform, false);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 8f);
        textRect.offsetMax = new Vector2(-24f, -8f);
        label = text.GetComponent<TextMeshProUGUI>();
        label.fontSize = 28f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.57f, 0.9f, 1f);
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        ApplyChineseFont();
    }

    void ApplyChineseFont()
    {
        if (label == null) return;
        UiFontAssets.ApplyTo(label, FontSlots.Default);
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
