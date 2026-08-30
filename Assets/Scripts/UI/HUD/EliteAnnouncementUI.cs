using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 精英怪出现公告横幅。两种装配方式：
///   1) 场景引用（推荐）：美术在 Canvas 下摆好面板（Image 背景 + TMP 文本 + CanvasGroup），
///      挂本组件并绑定 panelRoot / label；
///   2) 运行时自举：panelRoot / label 留空时自动创建最小可用横幅。
/// 显示时长 / 淡出时长可在 Inspector 调整；文案走 TextCatalog 的 elite.announce.* key。
/// </summary>
public sealed class EliteAnnouncementUI : MonoBehaviour
{
    [Header("场景引用装配（留空则运行时自举）")]
    [Tooltip("公告面板根节点（含 CanvasGroup + Image 背景）。留空则运行时自动创建。")]
    public GameObject panelRoot;
    [Tooltip("公告文本（TMP）。留空则运行时自动创建。")]
    public TextMeshProUGUI label;

    [Header("显示时长")]
    [Min(0.1f)] public float displayDuration = 3.5f;
    [Min(0.01f)] public float fadeDuration = 0.45f;

    CanvasGroup canvasGroup;
    float hideAt;

    // 统一文本目录（Dual_Line §1：所有玩家可见文本必须走 Text Key，不得硬编码）
    const string BannerKey = "elite.announce.banner";
    const string UnknownKey = "elite.announce.unknown";

    public static void ShowElite(string designedMonsterName)
    {
        string name = string.IsNullOrWhiteSpace(designedMonsterName)
            ? TextCatalog.Get(UnknownKey)
            : designedMonsterName.Trim();
        Canvas canvas = FindHudCanvas();
        if (canvas == null) canvas = CreateFallbackCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("[EliteAnnouncementUI] 找不到 HUD Canvas 且自举 Canvas 创建失败，精英公告无法显示。");
            return;
        }

        EliteAnnouncementUI announcement = canvas.GetComponent<EliteAnnouncementUI>();
        if (announcement == null) announcement = canvas.gameObject.AddComponent<EliteAnnouncementUI>();
        Debug.Log($"[EliteAnnouncementUI] 显示精英公告：{name}（canvas={canvas.name}）。");
        announcement.Show(name);
    }

    void Awake()
    {
        EnsureBanner();
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    /// <summary>确保横幅就绪：优先用场景引用，缺失则运行时自举创建。</summary>
    void EnsureBanner()
    {
        if (panelRoot != null && label != null)
        {
            // 场景引用：强制启用 panel（场景可能残留 inactive 的旧公告对象），并补/取 CanvasGroup 用于淡入淡出。
            if (!panelRoot.activeSelf) panelRoot.SetActive(true);
            canvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = panelRoot.AddComponent<CanvasGroup>();
            return;
        }
        CreateBanner();
    }

    void Show(string designedMonsterName)
    {
        EnsureBanner();
        if (label == null || canvasGroup == null)
        {
            Debug.LogWarning("[EliteAnnouncementUI] 横幅装配失败（label/canvasGroup 缺失），精英公告无法显示。");
            return;
        }

        // 强制确保横幅可见：场景残留的旧公告对象可能 Image 被禁用，显示前统一复位。
        if (panelRoot != null && !panelRoot.activeSelf)
            panelRoot.SetActive(true);
        var image = panelRoot != null ? panelRoot.GetComponent<Image>() : null;
        if (image != null && !image.enabled)
            image.enabled = true;

        label.text = TextCatalog.Get(BannerKey, designedMonsterName);
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

    /// <summary>运行时自举：创建最小可用横幅（顶部居中，面板 + 文本），并把 panelRoot / label 字段回填。</summary>
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

        panelRoot = panel;
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
