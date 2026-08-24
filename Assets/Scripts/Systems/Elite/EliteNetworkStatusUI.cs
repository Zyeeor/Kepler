using UnityEngine;
using TMPro;

/// <summary>
/// 精英怪网络状态 UI：在屏幕角落显示网络异常提示（服务器不可达时出现，恢复后自动消失）。
/// 由 EliteBuildDirector 驱动（启动探活 + 请求失败计数触发）。
/// 动态创建 TMP 文本，无需场景预制配置。
/// </summary>
public class EliteNetworkStatusUI : MonoBehaviour
{
    public static EliteNetworkStatusUI Instance { get; private set; }

    [Header("显示")]
    [Tooltip("提示文字。")]
    public string offlineText = "精英服务器离线";
    [Tooltip("提示颜色。")]
    public Color textColor = new Color(1f, 0.6f, 0.2f, 0.9f);
    [Tooltip("字号。")]
    public float fontSize = 22f;

    TextMeshProUGUI label;
    bool showing;

    void Awake()
    {
        Instance = this;
        EnsureLabel();
        Hide();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void EnsureLabel()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("EliteNetworkStatus", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(12f, -12f);
        rt.sizeDelta = new Vector2(300f, 36f);

        label = go.GetComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.color = textColor;
        label.raycastTarget = false;
        // 中文提示需中文字形：TMP 默认字体缺 CJK 会显示方框；set_font 加保护防字体资产异常中断调用方
        try
        {
            label.font = UiFontAssets.ChineseOrDefault;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[EliteNetworkStatusUI] 字体设置失败：{e.Message}");
        }
        label.text = offlineText;
    }

    public void Show()
    {
        if (showing) return;
        showing = true;
        if (label != null) label.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (!showing && label != null && !label.gameObject.activeSelf) return;
        showing = false;
        if (label != null) label.gameObject.SetActive(false);
    }
}
