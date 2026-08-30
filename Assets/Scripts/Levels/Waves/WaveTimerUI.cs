using UnityEngine;
using TMPro;

/// <summary>
/// 时间波倒计时 UI：挂在主 Canvas（UICanvas）下，屏幕中上方显示当前时间波剩余秒数。
/// 仅当 WaveManager 运行 Timed 波时显示；数量波/无波次自动隐藏。
/// Text 子对象在 Awake 时动态创建（TMP，沿用项目默认字体），无需场景预制配置。
/// </summary>
public class WaveTimerUI : MonoBehaviour
{
    /// <summary>全局单例。</summary>
    public static WaveTimerUI Instance { get; private set; }

    [Header("显示")]
    [Tooltip("剩余时间低于此秒数时文字变红（紧迫提示）；0 = 不变色。")]
    [Min(0f)] public float warnThreshold = 10f;
    [Tooltip("正常状态文字颜色。")]
    public Color normalColor = Color.white;
    [Tooltip("低于 warnThreshold 时的紧迫颜色。")]
    public Color warnColor = new Color(1f, 0.35f, 0.3f);
    [Tooltip("距屏幕顶部偏移（像素，锚点=中上方）。")]
    public float yOffset = -40f;
    [Tooltip("字号。")]
    public float fontSize = 44f;

    [Header("手动挂载（可选）")]
    [Tooltip("手动指定倒计时文本（可选）。留空则运行时自动创建到中上方；手动挂载后位置由该对象控制，yOffset 失效。")]
    public TextMeshProUGUI labelOverride;
    [Tooltip("手动指定字体（可选）。留空则使用 FontRegistry 默认字体。")]
    public TMP_FontAsset fontOverride;

    private TextMeshProUGUI label;
    private int lastShownSeconds = -1;

    void Awake()
    {
        Instance = this;
        EnsureLabel();
        if (label != null) label.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>动态创建 TMP 倒计时文本（挂在所属 Canvas 下，中上方）。</summary>
    void EnsureLabel()
    {
        if (labelOverride != null)
        {
            label = labelOverride;
            ApplyFont(label);
            return;
        }

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
            Debug.LogWarning("[WaveTimerUI] 未找到父级 Canvas，自动使用场景中第一个 Canvas。");
        }

        var go = new GameObject("WaveTimerText", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        if (canvas != null) rt.SetParent(canvas.transform, false);
        rt.anchorMin = new Vector2(0.5f, 1f); // 中上方
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta = new Vector2(360f, 64f);

        label = go.GetComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.color = normalColor;
        label.raycastTarget = false;
        ApplyFont(label);
        label.text = "";
    }

    void ApplyFont(TextMeshProUGUI target)
    {
        if (fontOverride != null)
        {
            target.font = fontOverride;
            if (fontOverride.material != null) target.fontSharedMaterial = fontOverride.material;
        }
        else
        {
            UiFontAssets.ApplyTo(target);
        }
    }

    void Update()
    {
        if (label == null) return;

        var wm = WaveManager.Instance;
        bool show = wm != null && wm.IsWaveActive && wm.TimeWaveRemaining > 0f;
        if (label.gameObject.activeSelf != show)
        {
            label.gameObject.SetActive(show);
            if (!show) return;
        }
        if (!show) return;

        // 向上取整显示剩余整秒，避免开局直接跳 59
        int secs = Mathf.CeilToInt(wm.TimeWaveRemaining);
        if (secs != lastShownSeconds)
        {
            lastShownSeconds = secs;
            label.text = $"{secs / 60:0}:{secs % 60:00}";
        }
        bool warn = warnThreshold > 0f && wm.TimeWaveRemaining <= warnThreshold;
        label.color = warn ? warnColor : normalColor;
    }
}
