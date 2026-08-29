using UnityEngine;
using UnityEngine.UI;

public sealed class PossessionImprintTooltip : MonoBehaviour
{
    public GameObject panel;
    public Text titleText;
    public Text effectText;
    public Vector2 cursorOffset = new Vector2(18f, 18f);

    RectTransform panelRect;
    RectTransform canvasRect;
    RectTransform parentRect;
    Canvas canvas;
    bool visible;

    void Awake()
    {
        CacheReferences();
        EnsureTextRefs();
        DisableRaycasts();
    }

    void CacheReferences()
    {
        if (panel == null) panel = gameObject;
        panelRect = panel.GetComponent<RectTransform>();
        parentRect = panelRect != null ? panelRect.parent as RectTransform : null;
        canvas = panel.GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? canvas.transform as RectTransform : null;
    }

    /// <summary>
    /// titleText / effectText 未配置时，从 panel 子节点按名字自动找 "Title" / "Effect" 文本。
    /// 这样即使场景里 PossessionImprintTooltip 的字段引用为空，hover 也能正确写入文字。
    /// </summary>
    void EnsureTextRefs()
    {
        if (panel == null) return;
        if (titleText != null && effectText != null) return;

        Text[] texts = panel.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text t = texts[i];
            if (t == null) continue;
            if (titleText == null && t.name.Equals("Title", System.StringComparison.OrdinalIgnoreCase)) titleText = t;
            else if (effectText == null && t.name.Equals("Effect", System.StringComparison.OrdinalIgnoreCase)) effectText = t;
        }
    }

    void DisableRaycasts()
    {
        if (panel == null) return;
        Graphic[] graphics = panel.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics) graphic.raycastTarget = false;
    }

    void Update()
    {
        if (visible && panel != null && panel.activeSelf) PositionNearCursor();
    }

    public void Show(SinType sin, int stacks)
    {
        Show(TextCatalog.Get("imprint.stack_suffix", GetTitle(sin), stacks), GetEffect(sin, stacks));
    }

    public void Show(string title, string effect)
    {
        if (panel != null)
        {
            panel.SetActive(true);
            DisableRaycasts();
            panel.transform.SetAsLastSibling();
        }
        EnsureTextRefs();
        if (titleText != null) titleText.text = title ?? string.Empty;
        if (effectText != null) effectText.text = effect ?? string.Empty;
        visible = true;
        PositionNearCursor();
    }

    public void Hide()
    {
        visible = false;
        if (panel != null) panel.SetActive(false);
    }

    void PositionNearCursor()
    {
        if (panelRect == null || parentRect == null || canvas == null)
            CacheReferences();
        if (panelRect == null || parentRect == null || canvas == null) return;

        Rect bounds = canvas.pixelRect;
        Vector2 mouse = Input.mousePosition;
        Vector2 scale = panelRect.lossyScale;
        Vector2 size = new Vector2(
            panelRect.rect.width * Mathf.Abs(scale.x),
            panelRect.rect.height * Mathf.Abs(scale.y));
        Vector2 pivot = new Vector2(
            mouse.x + cursorOffset.x + size.x <= bounds.xMax - 8f ? 0f : 1f,
            mouse.y - cursorOffset.y - size.y >= bounds.yMin + 8f ? 1f : 0f);

        Vector2 screenPosition = mouse;
        screenPosition.x += pivot.x < 0.5f ? cursorOffset.x : -cursorOffset.x;
        screenPosition.y += pivot.y > 0.5f ? -cursorOffset.y : cursorOffset.y;
        panelRect.pivot = pivot;

        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parentRect, screenPosition, eventCamera, out Vector3 worldPosition))
            panelRect.position = worldPosition;
    }

    public static string GetTitle(SinType sin)
    {
        // 统一文本目录：七罪罪印名（imprint.title.*），文本只动资产不动代码
        switch (sin)
        {
            case SinType.Pride: return TextCatalog.Get("imprint.title.pride");
            case SinType.Wrath: return TextCatalog.Get("imprint.title.wrath");
            case SinType.Gluttony: return TextCatalog.Get("imprint.title.gluttony");
            case SinType.Greed: return TextCatalog.Get("imprint.title.greed");
            case SinType.Envy: return TextCatalog.Get("imprint.title.envy");
            case SinType.Lust: return TextCatalog.Get("imprint.title.lust");
            case SinType.Sloth: return TextCatalog.Get("imprint.title.sloth");
            default: return TextCatalog.Get("imprint.title.default");
        }
    }

    public static string GetEffect(SinType sin, int stacks)
    {
        // 统一文本目录：效果描述模板（imprint.effect.*，{0}/{1} 占位符），文本只动资产不动代码
        switch (sin)
        {
            case SinType.Pride:
                return TextCatalog.Get("imprint.effect.pride",
                    ((1f - PossessionImprintMath.PrideCooldownMultiplier(stacks)) * 100f).ToString("0.0"));
            case SinType.Wrath:
                return TextCatalog.Get("imprint.effect.wrath",
                    ((PossessionImprintMath.WrathDamageMultiplier(stacks) - 1f) * 100f).ToString("0"));
            case SinType.Gluttony:
                return TextCatalog.Get("imprint.effect.gluttony",
                    ((PossessionImprintMath.GluttonyHealthMultiplier(stacks) - 1f) * 100f).ToString("0"),
                    ((PossessionImprintMath.GluttonyScaleMultiplier(stacks) - 1f) * 100f).ToString("0"));
            case SinType.Greed:
                float progress = PossessionImprintMath.GreedProgressPerPossession(stacks);
                float fractionalProgress = progress - Mathf.Floor(progress);
                return TextCatalog.Get("imprint.effect.greed",
                    (progress * 100f).ToString("0"), (fractionalProgress * 100f).ToString("0"));
            case SinType.Envy:
                return TextCatalog.Get("imprint.effect.envy",
                    PossessionImprintMath.EnvyBulletTimeBonus(stacks).ToString("0.00"));
            case SinType.Lust:
                return TextCatalog.Get("imprint.effect.lust",
                    (PossessionImprintMath.LustLifestealMultiplier(stacks) * 100f).ToString("0"));
            case SinType.Sloth:
                return TextCatalog.Get("imprint.effect.sloth",
                    ((1f - PossessionImprintMath.SlothDrainMultiplier(stacks)) * 100f).ToString("0.0"));
            default:
                return string.Empty;
        }
    }
}
