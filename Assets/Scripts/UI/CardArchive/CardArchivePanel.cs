using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 卡牌图鉴组件（局外系统 §4）。
///
/// 设计范式与 BuildView 一致：把它挂到任意 UI GameObject（如主菜单 Canvas 下的一个空物体）上，
/// 该物体即拥有「显示卡牌图鉴」的能力；所有显示配置（卡片尺寸/间距/配色/字号等）
/// 直接在本组件的 Inspector 上编辑，运行时改参数即时生效（OnValidate）。
///
/// 不依赖任何 Resources prefab，全部 UI 在 Start 时于自身 transform 下动态构建。
/// 卡片渲染复用 choice1 预制体，走 CoreChoiceCard.Init() 标准入口并只读化，
/// 保证图鉴中的卡面表现与选卡界面完全一致。
///
/// 三态：Unknown=剪影(???) / Known=卡面但置灰 / Unlocked=完整+时间戳+次数+新解锁角标。
/// 分类页签：全部 / 七宗罪 / 通用。进度分母取自 CardArchiveStore.ValidCardTotal。
/// </summary>
public class CardArchivePanel : MonoBehaviour
{
    // ───────────────────────── 配置（Inspector） ─────────────────────────

    [Header("数据源")]
    [Tooltip("卡片预制体（复用 choice1）。留空则自动取 CardLibrary.Instance.cardPrefab。")]
    [SerializeField] GameObject cardPrefab;

    [Header("面板尺寸")]
    [Tooltip("面板宽度（像素）。0 = 自动取父级宽度。")]
    [SerializeField] float panelWidth = 1440f;
    [Tooltip("面板高度（像素）。0 = 自动取父级高度。")]
    [SerializeField] float panelHeight = 880f;
    [Tooltip("面板背景色")]
    [SerializeField] Color panelBgColor = new Color(0.07f, 0.075f, 0.1f, 0.97f);
    [Tooltip("面板描边颜色")]
    [SerializeField] Color frameColor = new Color(0.42f, 0.36f, 0.2f, 0.9f);
    [Tooltip("是否绘制四边描边")]
    [SerializeField] bool showFrame = true;

    [Header("卡片布局（策划可调）")]
    [Tooltip("卡面显示高度（像素）。卡面按实际内容等比缩放撑满该高度，不会被截断。")]
    [SerializeField] float cardFaceHeight = 300f;
    [Tooltip("卡面显示宽度（像素）。0 = 按卡面内容实测比例自动推算。")]
    [SerializeField] float cardFaceWidth = 0f;
    [Tooltip("卡片底部信息条高度（像素）")]
    [SerializeField] float infoBarHeight = 64f;
    [Tooltip("卡片间距（水平, 垂直）")]
    [SerializeField] Vector2 cardSpacing = new Vector2(16f, 16f);
    [Tooltip("网格内边距（左 右 上 下）")]
    [SerializeField] Vector4 padding = new Vector4(20f, 20f, 20f, 20f);

    [Header("文字")]
    [Tooltip("标题字号")]
    [SerializeField] int titleFontSize = 50;
    [Tooltip("统计文字字号")]
    [SerializeField] int progressFontSize = 30;
    [Tooltip("底部状态字号")]
    [SerializeField] int statusFontSize = 30;
    [Tooltip("页签文字字号")]
    [SerializeField] int tabFontSize = 36;
    [Tooltip("卡片信息条：卡名字号 / 状态字号 / 次数字号")]
    [SerializeField] Vector3 infoBarFontSizes = new Vector3(18f, 18f, 18f);

    [Header("页签")]
    [Tooltip("页签行高度")]
    [SerializeField] float tabRowHeight = 36f;
    [Tooltip("页签间距")]
    [SerializeField] float tabSpacing = 20f;
    [Tooltip("页签选中底色")]
    [SerializeField] Color tabSelectedColor = new Color(0.85f, 0.68f, 0.28f);
    [Tooltip("页签未选中底色")]
    [SerializeField] Color tabNormalColor = new Color(0.22f, 0.22f, 0.28f);

    [Header("进度条")]
    [Tooltip("进度条宽度 / 高度")]
    [SerializeField] Vector2 progressBarSize = new Vector2(800f, 20f);
    [Tooltip("进度条底色 / 填充色")]
    [SerializeField] Color progressTrackColor = new Color(0.16f, 0.16f, 0.2f);
    [SerializeField] Color progressFillColor = new Color(0.95f, 0.75f, 0.25f);

    [Header("行为")]
    [Tooltip("启动时自动构建并隐藏（false 则由外部调用 Build/Show 控制）")]
    [SerializeField] bool autoBuildOnStart = true;
    [Tooltip("初始是否显示")]
    [SerializeField] bool startVisible = false;

    // ───────────────────────── 运行时 ─────────────────────────

    GameObject panelRoot;                 // 面板根（自身 transform 下）
    RectTransform contentRoot;            // 卡片容器（Content）
    ScrollRect scrollRect;
    Button closeButton;
    TextMeshProUGUI titleText, progressText, statusText;
    RectTransform tabRow;
    Image progressFill;

    readonly List<string> tabs = new List<string>();
    string currentTab = "all";
    bool visible;
    bool built;

    /// <summary>卡面显示宽度：策划指定则用指定值，否则按卡面内容实测比例推算。</summary>
    float FaceWidth => cardFaceWidth > 0f ? cardFaceWidth : cardFaceHeight * Mathf.Max(0.1f, measuredAspect);

    /// <summary>把 Vector4 的内边距配置转成 GridLayoutGroup 需要的 RectOffset。</summary>
    RectOffset PaddingRect => new RectOffset((int)padding.x, (int)padding.y, (int)padding.z, (int)padding.w);

    /// <summary>
    /// 颜色兜底：[SerializeField] Color 的字段初始值在反序列化时（尤其经 AddComponent 后存进场景）
    /// 可能不生效而退化为 Color.clear（alpha=0），导致元素完全不可见。
    /// 这里对 alpha 为 0 的颜色回落到代码默认值。
    /// </summary>
    static Color EnsureColor(Color configured, Color fallback)
        => configured.a > 0.001f ? configured : fallback;

    /// <summary>
    /// 字号兜底：与 Color 同理，[SerializeField] 数值字段的初始值在反序列化时可能不生效而退化为 0，
    /// 导致 TMP 文本字号为 0（完全不可见）。这里对 &lt;=0 的字号回落到代码默认值。
    /// </summary>
    static int EnsureSize(int configured, int fallback)
        => configured > 0 ? configured : fallback;

    // 各颜色的代码默认值（兜底用）
    static readonly Color DefPanelBg = new Color(0.07f, 0.075f, 0.1f, 0.97f);
    static readonly Color DefFrame = new Color(0.42f, 0.36f, 0.2f, 0.9f);
    static readonly Color DefTabSelected = new Color(0.85f, 0.68f, 0.28f, 1f);
    static readonly Color DefTabNormal = new Color(0.22f, 0.22f, 0.28f, 1f);
    static readonly Color DefTrack = new Color(0.16f, 0.16f, 0.2f, 1f);
    static readonly Color DefFill = new Color(0.95f, 0.75f, 0.25f, 1f);

    /// <summary>卡面内容实测宽高比，首次渲染后缓存，用于 cardFaceWidth=0 时推算宽度。</summary>
    float measuredAspect = 0.625f;
    bool aspectMeasured;

    public System.Action onClose;

    // ───────────────────────── 静态获取（兼容既有调用方） ─────────────────────────

    /// <summary>
    /// 获取图鉴组件实例。
    /// 优先复用场景中已挂载的实例（策划可直接在主界面挂本组件并配置参数）；
    /// 没有则自动创建一个，挂到主 Canvas 下并铺满。
    /// 与 BuildView 的自发现思路一致：挂上就能用，不挂也能自动兜底。
    /// </summary>
    public static CardArchivePanel EnsureInstance()
    {
        var existing = FindObjectOfType<CardArchivePanel>();
        if (existing != null) return existing;

        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogWarning("[CardArchivePanel] 场景中未找到 Canvas，无法创建图鉴。"); return null; }

        var host = new GameObject(nameof(CardArchivePanel), typeof(RectTransform));
        host.transform.SetParent(canvas.transform, false);
        var hostRT = host.GetComponent<RectTransform>();
        hostRT.anchorMin = Vector2.zero;
        hostRT.anchorMax = Vector2.one;
        hostRT.offsetMin = Vector2.zero;
        hostRT.offsetMax = Vector2.zero;

        var comp = host.AddComponent<CardArchivePanel>();
        comp.Build();
        return comp;
    }

    // ───────────────────────── 生命周期 ─────────────────────────

    void Start()
    {
        // EnsureInstance 可能已提前 Build 过，此处仅在尚未构建时执行
        if (!built && autoBuildOnStart) Build();
        if (startVisible) Show();
        else if (panelRoot != null) panelRoot.SetActive(false);
    }

    // 运行时在 Inspector 改参数即时重建，方便策划调试（与 BuildView.OnValidate 同思路）
    void OnValidate()
    {
        if (!Application.isPlaying || !built) return;
        Rebuild();
    }

    // ───────────────────────── 构建 ─────────────────────────

    /// <summary>构建整个图鉴 UI（幂等：已构建则先清理）。</summary>
    public void Build()
    {
        if (built) Clear();
        BuildPanel();
        built = true;
        // 构建完默认隐藏，由 Show() 控制显隐（避免刚创建就遮挡主菜单）
        if (panelRoot != null) panelRoot.SetActive(startVisible);
    }

    /// <summary>销毁已构建的 UI 并重新构建。</summary>
    public void Rebuild()
    {
        Clear();
        Build();
        if (visible) Refresh();
    }

    void Clear()
    {
        if (panelRoot != null) { DestroyImmediate(panelRoot); panelRoot = null; }
        contentRoot = null; scrollRect = null; closeButton = null;
        titleText = progressText = statusText = null;
        tabRow = null; progressFill = null;
        built = false;
    }

    void BuildPanel()
    {
        // 面板根：挂在自身 transform 下，尺寸可配置或跟随父级
        panelRoot = new GameObject("ArchiveRoot", typeof(RectTransform));
        var prt = panelRoot.GetComponent<RectTransform>();
        prt.SetParent(transform, false);
        float pw = panelWidth > 0f ? panelWidth : (transform is RectTransform p1 ? p1.rect.width : 1440f);
        float ph = panelHeight > 0f ? panelHeight : (transform is RectTransform p2 ? p2.rect.height : 880f);
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(pw, ph);
        prt.anchoredPosition = Vector2.zero;

        var bg = panelRoot.AddComponent<Image>();
        bg.color = EnsureColor(panelBgColor, DefPanelBg);
        if (showFrame) AddFrameLines(prt, EnsureColor(frameColor, DefFrame));

        float top = ph * 0.5f;      // 面板顶边（局部坐标）
        float cursor = top;          // 垂直排布游标，从顶边向下递减

        // 标题
        titleText = AddText(prt, "卡牌图鉴", EnsureSize(titleFontSize, 30), new Vector2(0, cursor - 40f), TextAlignmentOptions.Center, (int)pw - 40);
        cursor -= 80f;

        // 标题下分隔线
        AddBar(prt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(560, 2),
               new Vector2(0, cursor - top), frameColor);
        cursor -= 12f;

        // 页签行
        float tabTop = cursor;
        var tabGO = new GameObject("Tabs", typeof(RectTransform));
        var trt = tabGO.GetComponent<RectTransform>();
        trt.SetParent(prt, false);
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1);
        trt.pivot = new Vector2(0.5f, 1);
        trt.anchoredPosition = new Vector2(0, tabTop - top);
        trt.sizeDelta = new Vector2(pw - 40f, tabRowHeight);
        var thlg = tabGO.AddComponent<HorizontalLayoutGroup>();
        thlg.spacing = tabSpacing;
        thlg.childAlignment = TextAnchor.MiddleCenter;
        thlg.padding = new RectOffset(4, 4, 2, 2);
        // 9 个页签等分填满行宽：互不遮挡、不超出面板、随面板宽度自适应
        thlg.childControlWidth = true;
        thlg.childForceExpandWidth = true;
        thlg.childControlHeight = true;
        tabGO.AddComponent<RectMask2D>();   // 兜底裁剪，杜绝按钮溢出遮挡上下文本
        tabRow = trt;
        cursor -= (tabRowHeight + 20f);

        // 统计文本
        progressText = AddText(prt, "", EnsureSize(progressFontSize, 30), new Vector2(0, cursor - 20f), TextAlignmentOptions.Center, (int)pw - 40);
        cursor -= 60f;

        // 进度条
        var trackGO = new GameObject("ProgressTrack", typeof(RectTransform));
        var trackRT = trackGO.GetComponent<RectTransform>();
        trackRT.SetParent(prt, false);
        trackRT.anchorMin = trackRT.anchorMax = new Vector2(0.5f, 1);
        trackRT.pivot = new Vector2(0.5f, 1);
        trackRT.anchoredPosition = new Vector2(0, cursor - top);
        // 尺寸兜底：序列化退化时会变成 0，导致进度条不可见
        trackRT.sizeDelta = progressBarSize.x > 1f && progressBarSize.y > 1f
            ? progressBarSize : new Vector2(800f, 50f);
        trackGO.AddComponent<Image>().color = EnsureColor(progressTrackColor, DefTrack);
        var fillGO = new GameObject("Fill", typeof(RectTransform));
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.SetParent(trackRT, false);
        fillRT.anchorMin = new Vector2(0, 0);
        fillRT.anchorMax = new Vector2(0, 1);
        fillRT.pivot = new Vector2(0, 0.5f);
        fillRT.anchoredPosition = new Vector2(2, 0);
        fillRT.sizeDelta = new Vector2(0, -4);
        progressFill = fillGO.AddComponent<Image>();
        progressFill.color = EnsureColor(progressFillColor, DefFill);
        cursor -= (progressBarSize.y + 24f);

        // 底部状态文本
        statusText = AddText(prt, "", EnsureSize(statusFontSize, 30), new Vector2(0, -ph * 0.5f + 32f), TextAlignmentOptions.Center, (int)pw - 40);

        // 关闭按钮
        var closeBtn = new GameObject("Close", typeof(RectTransform));
        var crt = closeBtn.GetComponent<RectTransform>();
        crt.SetParent(prt, false);
        crt.anchorMin = crt.anchorMax = new Vector2(1, 1);
        crt.pivot = new Vector2(1, 1);
        crt.anchoredPosition = new Vector2(-14, -14);
        crt.sizeDelta = new Vector2(124, 54);
        closeBtn.AddComponent<Image>().color = new Color(0.5f, 0.22f, 0.2f);
        var cbtn = closeBtn.AddComponent<Button>();
        closeButton = cbtn;
        var cc = cbtn.colors;
        cc.highlightedColor = new Color(0.68f, 0.3f, 0.26f);
        cc.pressedColor = new Color(0.4f, 0.16f, 0.14f);
        cbtn.colors = cc;
        cbtn.onClick.AddListener(Hide);
        AddText(crt, "关闭", EnsureSize(tabFontSize, 36), Vector2.zero, TextAlignmentOptions.Center, 124);

        // 滚动区：上接进度条下方，下接状态文本上方
        var scrollGO = new GameObject("Scroll", typeof(RectTransform));
        var srt = scrollGO.GetComponent<RectTransform>();
        srt.SetParent(prt, false);
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.pivot = new Vector2(0.5f, 0.5f);
        float scrollTop = cursor;                 // 顶边（局部 y）
        float scrollBottom = -ph * 0.5f + 64f;    // 底边
        float scrollH = Mathf.Max(80f, scrollTop - scrollBottom);
        srt.sizeDelta = new Vector2(pw - 32f, scrollH);
        srt.anchoredPosition = new Vector2(0, (scrollTop + scrollBottom) * 0.5f);

        var scroll = scrollGO.AddComponent<ScrollRect>();
        scrollRect = scroll;
        scroll.vertical = true;
        scroll.horizontal = false;

        var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
        viewport.SetParent(srt, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewport.gameObject.AddComponent<RectMask2D>();
        // ⚠️ Viewport 必须有可接收射线的 Graphic，否则 ScrollRect 收不到拖拽事件、完全滑不动。
        // alpha 取极小值保证肉眼不可见，但 raycastTarget 必须为 true。
        var vpImg = viewport.gameObject.AddComponent<Image>();
        vpImg.color = new Color(1f, 1f, 1f, 0.001f);
        vpImg.raycastTarget = true;
        scroll.viewport = viewport;

        var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0, 1);
        content.sizeDelta = new Vector2(0, 0);
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(FaceWidth, cardFaceHeight + infoBarHeight);
        grid.spacing = cardSpacing;
        grid.padding = PaddingRect;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.childAlignment = TextAnchor.UpperCenter;   // 每行卡牌水平居中
        var fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;
        contentRoot = content;

        BuildTabs();
        if (FontRegistry.Instance != null) FontRegistry.Instance.ApplyToTree(panelRoot.transform);
    }

    /// <summary>构建页签按钮（全部 / 七宗罪 / 通用）。</summary>
    void BuildTabs()
    {
        if (tabRow == null) return;
        tabs.Clear();
        tabs.Add("all");
        foreach (SinType s in System.Enum.GetValues(typeof(SinType)))
            if (s != SinType.None) tabs.Add(s.ToString());
        tabs.Add("Universal");

        foreach (var t in tabs)
        {
            var btn = new GameObject("Tab_" + t, typeof(RectTransform));
            var brt = btn.GetComponent<RectTransform>();
            brt.SetParent(tabRow, false);
            // 必须显式给尺寸：裸 GameObject 由 Image 自动补的 RectTransform 默认 100×100，
            // 会超出页签行高度并在垂直居中后溢出遮挡统计文本与进度条。
            brt.sizeDelta = new Vector2(130, tabRowHeight - 8f);
            btn.AddComponent<Image>().color = EnsureColor(tabNormalColor, DefTabNormal);
            var b = btn.AddComponent<Button>();
            var label = t == "all" ? "全部" : (t == "Universal" ? "通用" : t);
            AddText(brt, label, EnsureSize(tabFontSize, 36), Vector2.zero, TextAlignmentOptions.Center, 130);
            var captured = t;
            b.onClick.AddListener(() => { currentTab = captured; Refresh(); });
        }
    }

    // ───────────────────────── 显隐 / 刷新 ─────────────────────────

    public void Show()
    {
        bool wasVisible = visible;
        if (!built) Build();
        panelRoot.SetActive(true);
        visible = true;
        CardArchiveStore.MarkAllRead();   // 打开即视为已读（清除新解锁角标）
        Refresh();
        if (!wasVisible) AudioManager.Instance?.Play(SfxId.CardArchiveOpen);
    }

    public void Hide()
    {
        bool wasVisible = visible;
        if (panelRoot != null) panelRoot.SetActive(false);
        visible = false;
        if (wasVisible) AudioManager.Instance?.Play(SfxId.CardArchiveClose);
        onClose?.Invoke();
        onClose = null;
    }

    public bool IsVisible() => visible;

    public void Refresh()
    {
        // currentTab 兜底：为空（序列化/AddComponent 时机导致）时按「全部」处理
        if (string.IsNullOrEmpty(currentTab)) currentTab = "all";
        if (!built || contentRoot == null) return;

        // 清空（同步销毁，避免与同帧 Instantiate 叠加导致格子重复累积）
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            DestroyImmediate(contentRoot.GetChild(i).gameObject);

        var grid = contentRoot.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.cellSize = new Vector2(FaceWidth, cardFaceHeight + infoBarHeight);
            grid.spacing = cardSpacing;
            grid.padding = PaddingRect;
        }

        var entries = CardArchiveStore.AllEntries();
        int shown = 0;
        foreach (var e in entries)
        {
            if (!TabMatches(e)) continue;
            RenderTile(e);
            shown++;
        }

        int unknownCount = 0;
        if (currentTab == "all")
        {
            unknownCount = Mathf.Max(0, CardArchiveStore.ValidCardTotal - entries.Count);
            for (int i = 0; i < unknownCount; i++) RenderUnknown();
        }

        int unlocked = CardArchiveStore.UnlockedCount();
        int totalAll = CardArchiveStore.ValidCardTotal;
        progressText.text = TextCatalog.Get("ui.archive.unlocked_count", unlocked, totalAll);
        statusText.text = currentTab == "all"
            ? TextCatalog.Get("ui.archive.page_count", shown, unknownCount)
            : TextCatalog.Get("ui.archive.page_count_known", shown);
        statusText.color = new Color(0.9f, 0.9f, 0.9f);

        if (progressFill != null)
        {
            float ratio = totalAll > 0 ? (float)unlocked / totalAll : 0f;
            float trackW = (progressBarSize.x > 1f ? progressBarSize.x : 800f) - 4f;
            progressFill.rectTransform.sizeDelta = new Vector2(trackW * Mathf.Clamp01(ratio), -4f);
        }

        UpdateTabHighlight();

        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.StopMovement();
            scrollRect.normalizedPosition = new Vector2(0f, 1f);   // 回到顶部
        }
    }

    bool TabMatches(CardArchiveEntry e)
    {
        // currentTab 可能因序列化/AddComponent 时机为空，统一兜底为 "all"，
        // 否则空字符串匹配不到任何分类，会导致整页卡片渲染为 0 张。
        string tab = string.IsNullOrEmpty(currentTab) ? "all" : currentTab;
        if (tab == "all") return true;
        if (tab == "Universal") return e.sin == "Universal" || string.IsNullOrEmpty(e.sin);
        return e.sin == tab;
    }

    void UpdateTabHighlight()
    {
        if (tabRow == null) return;
        foreach (Transform tab in tabRow)
        {
            bool selected = tab.name == "Tab_" + currentTab;
            var img = tab.GetComponent<Image>();
            if (img != null)
                img.color = selected ? EnsureColor(tabSelectedColor, DefTabSelected)
                                     : EnsureColor(tabNormalColor, DefTabNormal);
            var txt = tab.GetComponentInChildren<TMP_Text>(true);
            if (txt != null)
            {
                txt.color = selected ? new Color(0.15f, 0.12f, 0.08f) : new Color(0.85f, 0.85f, 0.85f);
                txt.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
            }
        }
    }

    // ───────────────────────── 卡片渲染 ─────────────────────────

    void RenderTile(CardArchiveEntry e)
    {
        var tile = new GameObject("Card_" + e.cardId, typeof(RectTransform));
        var trt = tile.GetComponent<RectTransform>();
        trt.SetParent(contentRoot, false);
        trt.sizeDelta = new Vector2(FaceWidth, cardFaceHeight + infoBarHeight);

        var card = CardLibrary.Instance != null ? CardLibrary.Instance.FindCard(e.cardId) : null;
        if (card != null && e.state != 0)
            RenderCardFace(tile, card, e.state);

        BuildInfoBar(trt, e);
    }

    void RenderUnknown()
    {
        var tile = new GameObject("Unknown", typeof(RectTransform));
        var trt = tile.GetComponent<RectTransform>();
        trt.SetParent(contentRoot, false);
        trt.sizeDelta = new Vector2(FaceWidth, cardFaceHeight + infoBarHeight);

        // 剪影：大号问号居中卡面区（不泄露卡面/效果）
        var q = AddText(trt, "？", 64, new Vector2(0, infoBarHeight * 0.5f), TextAlignmentOptions.Center, 120);
        q.color = new Color(0.32f, 0.32f, 0.38f);
        if (FontRegistry.Instance != null) FontRegistry.Instance.ApplyFontToText(q, FontSlots.Default);

        BuildInfoBar(trt, new CardArchiveEntry { state = 0, cardName = null });
    }

    /// <summary>卡片底部信息条：卡名 / 状态 / 次数（Unlocked）。与卡面区上下分离，互不遮挡。</summary>
    void BuildInfoBar(RectTransform tile, CardArchiveEntry e)
    {
        var barGO = new GameObject("InfoBar", typeof(RectTransform));
        var barRT = barGO.GetComponent<RectTransform>();
        barRT.SetParent(tile, false);
        barRT.anchorMin = new Vector2(0, 0);
        barRT.anchorMax = new Vector2(1, 0);
        barRT.pivot = new Vector2(0.5f, 0f);
        barRT.anchoredPosition = Vector2.zero;
        barRT.sizeDelta = new Vector2(0, infoBarHeight);
        barGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.82f);

        // 顶部亮线，颜色跟随状态
        var line = new GameObject("TopLine", typeof(RectTransform)).GetComponent<RectTransform>();
        line.SetParent(barRT, false);
        line.anchorMin = new Vector2(0, 1);
        line.anchorMax = new Vector2(1, 1);
        line.pivot = new Vector2(0.5f, 1);
        line.anchoredPosition = Vector2.zero;
        line.sizeDelta = new Vector2(0, 2);
        line.gameObject.AddComponent<Image>().color = e.state == 2
            ? new Color(0.8f, 0.65f, 0.25f, 0.85f)
            : new Color(0.5f, 0.5f, 0.55f, 0.6f);

        float w = FaceWidth;
        string title = e.state == 0 ? "？？？" : (e.cardName ?? "？？？");
        var titleTxt = AddText(barRT, title, EnsureSize((int)infoBarFontSizes.x, 18),
            new Vector2(0, infoBarHeight * 0.30f), TextAlignmentOptions.Center, w);
        titleTxt.color = e.state == 0 ? new Color(0.55f, 0.55f, 0.55f) : Color.white;

        string desc = e.state == 0 ? "未解锁" : (e.state == 1 ? "已遇见 · 未获得" : "已获得");
        var descTxt = AddText(barRT, desc, EnsureSize((int)infoBarFontSizes.y, 18),
            new Vector2(0, -infoBarHeight * 0.02f), TextAlignmentOptions.Center, w);
        descTxt.color = new Color(0.85f, 0.85f, 0.85f);

        if (e.state == 2)
        {
            var meta = AddText(barRT, $"×{e.selectedCount}  {UnixToDate(e.firstUnlockedAtUnix)}",
                EnsureSize((int)infoBarFontSizes.z, 18), new Vector2(0, -infoBarHeight * 0.30f), TextAlignmentOptions.Center, w);
            meta.color = new Color(0.65f, 0.9f, 0.65f);

            if (e.isNewUnread)
            {
                // 新解锁徽章：红底白字
                var badge = new GameObject("NewBadge", typeof(RectTransform)).GetComponent<RectTransform>();
                badge.SetParent(tile, false);
                badge.anchorMin = badge.anchorMax = new Vector2(1, 1);
                badge.pivot = new Vector2(1, 1);
                badge.anchoredPosition = new Vector2(-5, -5);
                badge.sizeDelta = new Vector2(52, 24);
                badge.gameObject.AddComponent<Image>().color = new Color(0.85f, 0.25f, 0.2f);
                var bt = AddText(badge, "NEW", 14, Vector2.zero, TextAlignmentOptions.Center, 52);
                bt.color = Color.white;
                bt.fontStyle = FontStyles.Bold;
            }
        }
    }

    /// <summary>
    /// 渲染卡面：完全复用 choice1 预制体，走 CoreChoiceCard.Init() 标准入口（与 BuildView 一致），
    /// 再只读化（隐藏按钮/文本、禁射线）。Card 内部各层的 sprite/enabled/alpha
    /// 一律交给 Init()→ApplyLayers() 决定，本方法绝不改动 Card 内部任何节点或颜色，
    /// 以保证图鉴卡面与选卡界面表现完全一致。
    ///
    /// ⚠️ 切勿对 Card 内部节点做 alpha=0 / 删除 / 重父化：
    ///    - background (1) 上挂着 Mask，alpha=0 会让模板失效，其下 background/middleground 子层被整片裁掉；
    ///    - 移动/重父化子层会改变兄弟顺序，导致前后遮挡错乱。
    /// 仅隐藏 Card 以外的兄弟节点（Image (1)/Image (2)/refresh/upgrade/description 等交互与主图节点）。
    /// </summary>
    void RenderCardFace(GameObject tile, CardData card, int state)
    {
        var prefab = ResolvePrefab();
        if (prefab == null) return;

        var inst = Instantiate(prefab, tile.transform);
        inst.name = "CardFace";
        var rt = inst.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }
        if (FontRegistry.Instance != null) FontRegistry.Instance.ApplyFontToTree(inst.transform, FontSlots.Card);

        // 与 BuildView 完全一致的标准渲染入口
        var cc = inst.GetComponent<CoreChoiceCard>();
        if (cc == null) cc = inst.AddComponent<CoreChoiceCard>();
        cc.Init(0, card.ResolveCardName(), card.image, card.ResolveDescription() ?? "", null, null, card);

        // 只读化
        if (cc.confirmButton != null) cc.confirmButton.gameObject.SetActive(false);
        if (cc.rerollButton != null) cc.rerollButton.gameObject.SetActive(false);
        if (cc.cardText != null) cc.cardText.gameObject.SetActive(false);
        if (cc.descriptionText != null) cc.descriptionText.gameObject.SetActive(false);
        var choiceCard = inst.GetComponent<ChoiceCard>();
        if (choiceCard != null) Destroy(choiceCard);

        foreach (var img in inst.GetComponentsInChildren<Image>(true))
            img.raycastTarget = false;   // 只读：不响应点击

        // 只隐藏 Card 以外的兄弟节点，Card 内部一律不动
        var cardTf = inst.transform.Find("Card");
        for (int i = 0; i < inst.transform.childCount; i++)
        {
            var child = inst.transform.GetChild(i);
            if (child != cardTf) child.gameObject.SetActive(false);
        }

        // Known 置灰：仅改 RGB，保留原 alpha（绝不用 alpha 做隐藏）
        if (state != 2)
        {
            foreach (var img in inst.GetComponentsInChildren<Image>(false))
                img.color = new Color(0.55f, 0.55f, 0.55f, img.color.a);
        }

        // 等比缩放并居中到卡面区
        FitCardFace(rt, cardTf, (RectTransform)tile.transform);
    }

    /// <summary>把卡面按实际内容包围盒等比缩放，撑满卡面区并居中（不截断、不溢出、不重叠）。</summary>
    void FitCardFace(RectTransform faceRT, Transform cardTf, RectTransform tileRT)
    {
        if (faceRT == null || tileRT == null) return;
        var measureRoot = (Transform)(cardTf != null ? cardTf : faceRT);
        var bounds = MeasureVisibleBounds(measureRoot, tileRT);
        if (bounds.width <= 1f || bounds.height <= 1f) return;

        // 记录卡面内容真实比例，供 cardFaceWidth=0 时推算格子宽度
        if (!aspectMeasured) { measuredAspect = bounds.width / bounds.height; aspectMeasured = true; }

        float scale = Mathf.Min(FaceWidth / bounds.width, cardFaceHeight / bounds.height);
        faceRT.localScale = Vector3.one * scale;
        var scaled = MeasureVisibleBounds(measureRoot, tileRT);
        // 对齐卡面区中心（tile 上部 cardFaceHeight 区域，底部 infoBarHeight 留给信息条）
        faceRT.anchoredPosition += new Vector2(0f, infoBarHeight * 0.5f) - scaled.center;
    }

    GameObject ResolvePrefab()
    {
        if (cardPrefab != null) return cardPrefab;
        return CardLibrary.Instance != null ? CardLibrary.Instance.cardPrefab : null;
    }

    /// <summary>测量 root 下所有可见 Image 在 space 本地空间的合并包围盒。</summary>
    static Rect MeasureVisibleBounds(Transform root, RectTransform space)
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        var corners = new Vector3[4];
        bool any = false;
        foreach (var img in root.GetComponentsInChildren<Image>(false))
        {
            if (!img.enabled || img.sprite == null) continue;
            img.rectTransform.GetWorldCorners(corners);
            for (int i = 0; i < 4; i++)
            {
                var p = (Vector2)space.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
                any = true;
            }
        }
        if (!any) return new Rect(0f, 0f, 100f, 100f);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    // ───────────────────────── 工具 ─────────────────────────

    void AddFrameLines(Transform parent, Color color)
    {
        AddBar(parent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 2), Vector2.zero, color);
        AddBar(parent, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 2), Vector2.zero, color);
        AddBar(parent, new Vector2(0, 0), new Vector2(0, 1), new Vector2(2, 0), Vector2.zero, color);
        AddBar(parent, new Vector2(1, 0), new Vector2(1, 1), new Vector2(2, 0), Vector2.zero, color);
    }

    void AddBar(Transform parent, Vector2 aMin, Vector2 aMax, Vector2 size, Vector2 pos, Color color)
    {
        var go = new GameObject("Bar", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    TextMeshProUGUI AddText(Transform parent, string txt, int size, Vector2 pos, TextAlignmentOptions align, float width)
    {
        var go = new GameObject("Txt", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, size + 14f);

        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = txt;
        t.fontSize = EnsureSize(size, 24);
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        if (FontRegistry.Instance != null) FontRegistry.Instance.ApplyFontToText(t, FontSlots.Default);
        return t;
    }

    static string UnixToDate(long unix)
    {
        if (unix <= 0) return "--";
        var dt = System.DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime();
        return dt.ToString("yyyy-MM-dd");
    }
}
