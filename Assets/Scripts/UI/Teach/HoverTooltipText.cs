using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 通用 hover 文字提示：挂到任意 UI 物体上，鼠标悬浮时在光标右上方弹出 title + description。
///
/// 两种用法：
///   1. Inspector 直接填 title / description（静态提示）；
///   2. 代码调用 SetText(title, description) 动态设置（用于与 MonsterSkillIconConfig.description 等联动）。
///
/// 面板优先复用场景中已有的 PossessionImprintTooltip；找不到时自动创建一个共享的
/// TMP 提示面板（中文字体 + 鼠标右上角跟随），零场景配置即可工作。
/// </summary>
public class HoverTooltipText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("提示标题。")]
    public string title;
    [TextArea(2, 6)]
    [Tooltip("提示正文。")]
    public string description;
    [Tooltip("悬浮提示面板；留空则优先查找场景中的 PossessionImprintTooltip，找不到时自动创建。")]
    public PossessionImprintTooltip tooltip;

    bool hasContent;
    static TooltipPanel sharedPanel;

    void Awake()
    {
        EnsureRaycastTarget();
        RefreshHasContent();
    }

    /// <summary>需要 Graphic 接收 hover：复用已有 Image（不破坏其 color，仅开 raycastTarget）；无 Image 才新增。</summary>
    void EnsureRaycastTarget()
    {
        Graphic graphic = GetComponent<Graphic>();
        if (graphic == null)
        {
            graphic = gameObject.AddComponent<Image>();
            graphic.color = Color.clear;
        }
        graphic.raycastTarget = true;
    }

    /// <summary>设置提示文字（供代码联动 MonsterSkillIconConfig.description 等）。</summary>
    public void SetText(string newTitle, string newDescription)
    {
        title = newTitle;
        description = newDescription;
        RefreshHasContent();
    }

    void RefreshHasContent()
    {
        hasContent = !string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(description);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!hasContent) return;

        PossessionImprintTooltip tt = ResolveTooltip();
        if (tt != null)
        {
            tt.Show(title, description);
            return;
        }

        TooltipPanel panel = TooltipPanel.Ensure();
        if (panel != null) panel.Show(title, description);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        PossessionImprintTooltip tt = ResolveTooltip();
        if (tt != null)
        {
            tt.Hide();
            return;
        }
        if (sharedPanel != null) sharedPanel.Hide();
    }

    PossessionImprintTooltip ResolveTooltip()
    {
        if (tooltip == null) tooltip = FindObjectOfType<PossessionImprintTooltip>(true);
        return tooltip;
    }

    /// <summary>自建的共享 tooltip 面板：TMP 文本 + 背景 + 鼠标右上角跟随。</summary>
    class TooltipPanel : MonoBehaviour
    {
        RectTransform panelRect;
        RectTransform canvasRect;
        Canvas canvas;
        TextMeshProUGUI titleText;
        TextMeshProUGUI descText;

        public static TooltipPanel Ensure()
        {
            if (sharedPanel != null) return sharedPanel;

            Canvas canvas = FindCanvas();
            if (canvas == null) return null;

            GameObject go = new GameObject("HoverTooltipPanel", typeof(RectTransform), typeof(Image), typeof(TooltipPanel));
            go.transform.SetParent(canvas.transform, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(320f, 120f);

            Image bg = go.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.07f, 0.94f);
            bg.raycastTarget = false;

            var panel = go.GetComponent<TooltipPanel>();
            panel.panelRect = rt;
            panel.canvas = canvas;
            panel.canvasRect = canvas.transform as RectTransform;
            panel.titleText = panel.CreateText(go.transform, "Title", new Vector2(12f, -10f), 18f, FontStyles.Bold, new Color(1f, 0.82f, 0.35f, 1f));
            panel.descText = panel.CreateText(go.transform, "Description", new Vector2(12f, -38f), 15f, FontStyles.Normal, Color.white);

            sharedPanel = panel;
            return panel;
        }

        TextMeshProUGUI CreateText(Transform parent, string name, Vector2 offsetMin, float size, FontStyles style, Color color)
        {
            GameObject textGo = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(parent, false);
            RectTransform trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = offsetMin;
            trt.offsetMax = new Vector2(-12f, -8f);

            TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            tmp.font = UiFontAssets.ChineseOrDefault;
            return tmp;
        }

        public void Show(string title, string desc)
        {
            if (titleText != null) titleText.text = string.IsNullOrEmpty(title) ? string.Empty : title;
            if (descText != null) descText.text = string.IsNullOrEmpty(desc) ? string.Empty : desc;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            PositionAtCursor();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (gameObject.activeSelf) PositionAtCursor();
        }

        void PositionAtCursor()
        {
            if (panelRect == null || canvas == null) return;
            Vector2 mouse = Input.mousePosition;
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mouse, eventCamera, out Vector2 local))
            {
                panelRect.anchoredPosition = local + new Vector2(18f, 18f);
            }
        }

        static Canvas FindCanvas()
        {
            Canvas[] canvases = FindObjectsOfType<Canvas>();
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].isRootCanvas && canvases[i].renderMode != RenderMode.WorldSpace)
                    return canvases[i];
            }
            return null;
        }
    }
}
