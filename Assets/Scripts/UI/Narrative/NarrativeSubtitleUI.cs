using TMPro;
using UnityEngine;

/// <summary>
/// 旁白字幕条（自举：无场景挂载，首次 EnsureInstance 创建 DDOL Canvas + 底部居中 TMP）。
/// 契约 §6 Subtitle Mode 承载；与 Voice 同步开始/结束（调度器驱动，UI 被动）。
/// </summary>
public class NarrativeSubtitleUI : MonoBehaviour
{
    public static NarrativeSubtitleUI Instance { get; private set; }

    TextMeshProUGUI _text;
    CanvasGroup _canvasGroup;

    public static NarrativeSubtitleUI EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("NarrativeSubtitleUI");
        DontDestroyOnLoad(go);
        return go.AddComponent<NarrativeSubtitleUI>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildLayout();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void BuildLayout()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150; // 低于弹窗，高于常规 HUD
        var scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var panel = new GameObject("SubtitlePanel", typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(transform, false);
        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 120f);
        panelRect.sizeDelta = new Vector2(1400f, 90f);
        var img = panel.GetComponent<UnityEngine.UI.Image>();
        img.color = new Color(0f, 0f, 0f, 0.55f);

        var textGo = new GameObject("SubtitleText", typeof(TextMeshProUGUI));
        textGo.transform.SetParent(panel.transform, false);
        var textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(30f, 8f);
        textRect.offsetMax = new Vector2(-30f, -8f);
        _text = textGo.GetComponent<TextMeshProUGUI>();
        _text.fontSize = 32f;
        _text.alignment = TextAlignmentOptions.Center;
        _text.color = Color.white;

        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
    }

    public void ShowLine(string text, float duration)
    {
        if (_text == null) return;
        _text.text = text ?? "";
        _canvasGroup.alpha = 1f;
    }

    public void Hide()
    {
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
    }
}
