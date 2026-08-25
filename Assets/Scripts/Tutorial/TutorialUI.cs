using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 教学 Banner UI：标题 + 正文横幅。
/// 两种装配方式：
///   1) 场景引用（推荐）：美术在 Canvas 下摆好面板，挂本组件并绑定字段；
///   2) 运行时自举（TutorialController.EnsureMinimalUI）：无场景引用时的最小可用版。
/// 外部遮挡（选卡/设置面板）由 TutorialController 或挂载方轮询控制 Hide/Show。
/// </summary>
public class TutorialUI : MonoBehaviour
{
    [Header("场景引用装配（留空时配合 BuildRuntimeLayout 运行时装配）")]
    public GameObject panelRoot;
    public TMP_Text titleText;
    public TMP_Text bodyText;

    public bool IsShowing => panelRoot != null && panelRoot.activeSelf;

    /// <summary>显示 Banner（幂等：重复显示只刷新文案）。</summary>
    public void ShowBanner(string title, string body)
    {
        if (panelRoot == null) return;
        if (titleText != null) titleText.text = title ?? "";
        if (bodyText != null) bodyText.text = body ?? "";
        panelRoot.SetActive(true);
    }

    /// <summary>隐藏 Banner。</summary>
    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>
    /// 运行时自举布局（仅无场景引用时使用）：
    /// 屏幕顶部横幅面板 + 标题 + 正文。字体统一由 FontRegistry.default 管理；fontOverride 仅保留旧调用兼容性。
    /// </summary>
    public void BuildRuntimeLayout(Transform canvasRoot, TMP_FontAsset fontOverride = null)
    {
        var panelGo = new GameObject("TutorialBannerPanel");
        panelGo.transform.SetParent(canvasRoot, false);
        var rect = panelGo.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -40f);
        rect.sizeDelta = new Vector2(880f, 110f);   // 固定宽度横幅

        var img = panelGo.AddComponent<Image>();
        img.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);

        panelRoot = panelGo;

        // 标题（顶部，左右留 24px 边距）
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(panelGo.transform, false);
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(24f, -40f);
        titleRect.offsetMax = new Vector2(-24f, -8f);
        titleText = CreateTmpText(titleGo, 22f, new Color(1f, 0.85f, 0.4f), fontOverride);

        // 正文（标题下方剩余区域）
        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(panelGo.transform, false);
        var bodyRect = bodyGo.AddComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 0.5f);
        bodyRect.offsetMin = new Vector2(24f, 8f);
        bodyRect.offsetMax = new Vector2(-24f, -48f);
        bodyText = CreateTmpText(bodyGo, 18f, Color.white, fontOverride);
        bodyText.alignment = TextAlignmentOptions.Center;
        bodyText.enableWordWrapping = true;

        panelRoot.SetActive(false);
    }

    TMP_Text CreateTmpText(GameObject go, float fontSize, Color color, TMP_FontAsset fontOverride = null)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        // set_font 会触发 TMP 字形补烘焙；字体资产异常（atlas 导入期间被销毁）时抛 MissingReferenceException，
        // 此处保护：异常时保持默认字体，不让字体问题中断调用方（教学系统启动）。
        try
        {
            // fontOverride 保留旧调用签名，但全局字体始终以 FontRegistry 为唯一来源。
            UiFontAssets.ApplyTo(tmp);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[TutorialUI] 字体设置失败（{e.Message}），保持默认字体，文本可能显示异常。");
        }
        if (tmp.font == null)
            Debug.LogWarning("[TutorialUI] 无可用字体（TMP Essentials 未导入且未配置 bannerFont），教学文本可能不显示。");
        return tmp;
    }
}
