using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime HUD banner shown whenever an Elite successfully appears.</summary>
public sealed class EliteAnnouncementUI : MonoBehaviour
{
    [Min(0.1f)] public float displayDuration = 3.5f;
    [Min(0.01f)] public float fadeDuration = 0.45f;

    TextMeshProUGUI label;
    CanvasGroup canvasGroup;
    float hideAt;

    public static void ShowElite(string designedMonsterName)
    {
        string name = string.IsNullOrWhiteSpace(designedMonsterName)
            ? "未知怪物"
            : designedMonsterName.Trim();
        Canvas canvas = FindHudCanvas();
        if (canvas == null) canvas = CreateFallbackCanvas();
        if (canvas == null) return;

        EliteAnnouncementUI announcement = canvas.GetComponent<EliteAnnouncementUI>();
        if (announcement == null) announcement = canvas.gameObject.AddComponent<EliteAnnouncementUI>();
        announcement.Show(name);
    }

    void Awake()
    {
        CreateBanner();
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    void Show(string designedMonsterName)
    {
        if (label == null) CreateBanner();
        if (label == null || canvasGroup == null) return;

        label.text = $"精英怪{designedMonsterName}出现！";
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

        canvasGroup.alpha = remaining < fadeDuration
            ? Mathf.Clamp01(remaining / fadeDuration)
            : 1f;
    }

    void CreateBanner()
    {
        GameObject panel = new GameObject("EliteAnnouncement", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panel.transform.SetParent(transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -42f);
        panelRect.sizeDelta = new Vector2(980f, 86f);
        panel.GetComponent<Image>().color = new Color(0.035f, 0.06f, 0.08f, 0.88f);
        canvasGroup = panel.GetComponent<CanvasGroup>();

        GameObject text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(panel.transform, false);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 8f);
        textRect.offsetMax = new Vector2(-24f, -8f);
        label = text.GetComponent<TextMeshProUGUI>();
        label.fontSize = 30f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.78f, 0.3f, 1f);
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        label.font = UiFontAssets.ChineseOrDefault;
    }

    static Canvas FindHudCanvas()
    {
        GameObject namedCanvas = GameObject.Find("UICanvas");
        if (namedCanvas != null)
        {
            Canvas found = namedCanvas.GetComponent<Canvas>();
            if (found != null && found.renderMode != RenderMode.WorldSpace) return found;
        }

        Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace)
                return canvas;
        }
        return null;
    }

    static Canvas CreateFallbackCanvas()
    {
        GameObject go = new GameObject("EliteAnnouncementCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        return canvas;
    }
}
