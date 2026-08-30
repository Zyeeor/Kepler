using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 卡牌图鉴组件（局外系统 §4）。
///
/// 图鉴由 Resources/SystemUI/CardArchivePanel Prefab 作为独立 Overlay 创建并跨场景常驻；
/// 所有显示配置（卡片尺寸/间距/配色/字号等）直接在该 Prefab 的 Inspector 中编辑。
///
/// 静态界面壳层从 Resources/SystemUI/CardArchivePanel Prefab 实例化；卡片网格仍在运行时按图鉴数据生成。
/// 卡片渲染使用独立的 Resources/SystemUI/CardArchiveTile Prefab（CardArchiveTileView），
/// 不复用局内选卡 UI（choice1/CoreChoiceCard）——两者内部图层画布尺寸不一致，曾导致图鉴卡片
/// 大小互相不统一；CardArchiveTile 的外框在未知/已知/已解锁三态下共享同一张素材与画布比例。
///
/// 三态：Unknown=剪影(???) / Known=卡面但置灰 / Unlocked=完整+时间戳+次数+新解锁角标。
/// 分类页签：七宗罪。进度分母取自 CardArchiveStore.ValidCardTotal。
/// </summary>
public class CardArchivePanel : MonoBehaviour
{
    // ───────────────────────── 配置（Inspector） ─────────────────────────

    [Header("数据源")]
    [Tooltip("图鉴专用卡片预制体（Resources/SystemUI/CardArchiveTile）。留空则运行时自动 Resources.Load。")]
    [SerializeField] GameObject tilePrefab;
    [Tooltip("未知卡占位图；也作为已知/已解锁卡的外框素材（三态共用同一画布）。留空时使用问号占位。")]
    [SerializeField] Sprite unknownCardSilhouette;

    [Header("New Unlock Tint")]
    [Tooltip("新解锁卡面右上角标记使用的 Sprite。")]
    [SerializeField] Sprite cardNewUnlockTintSprite;
    [Tooltip("存在新解锁卡时，对应分类页签右上角标记使用的 Sprite。")]
    [SerializeField] Sprite filterNewUnlockTintSprite;

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

    [Serializable]
    public class TabVisualStyle
    {
        [Tooltip("该分类页签的 Image 节点。")]
        public Image image;
        [Tooltip("未选中态 Sprite；留空时使用 Card Filters.png 对应切片。")]
        public Sprite normalSprite;
        [Tooltip("选中态 Sprite；留空时使用 Card Filters.png 对应切片。")]
        public Sprite selectedSprite;
        public Color normalColor = Color.white;
        public Color selectedColor = Color.white;
    }

    [Header("Visual Layout (Prefab)")]
    [SerializeField] GameObject panelRoot;
    [SerializeField] Image backgroundImage;
    [SerializeField] RectTransform contentRoot;
    [SerializeField] RectTransform scrollViewport;
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] Button refreshButton;
    [Tooltip("刷新按钮图标；不指定时回退 Func Buttons.png 运行时切图。")]
    [SerializeField] Sprite refreshIconSprite;
    [SerializeField] Button closeButton;
    [Tooltip("关闭按钮图标；不指定时回退 Func Buttons.png 运行时切图。")]
    [SerializeField] Sprite closeIconSprite;

    [Header("Tab Images")]
    [Tooltip("顺序：傲慢、色欲、怠惰、暴怒、嫉妒、贪婪、暴食。每项可单独配置未选中态与选中态。")]
    [SerializeField] TabVisualStyle[] tabStyles = new TabVisualStyle[7];

    [SerializeField] TextMeshProUGUI progressText;
    [SerializeField] TextMeshProUGUI statusText;
    [SerializeField] RectTransform tabRow;
    [SerializeField] Image progressFill;

    const float HallFilterWidth = 131.2f;
    const float HallFilterHeight = 46.4f;
    const float HallFilterSpacing = 29.4f; // 荣誉殿堂筛选项间距的一半
    const float HallFilterLeft = 90f;
    const float HallFilterTop = -195f;

    readonly List<string> tabs = new List<string>();
    string currentTab = "Pride";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    readonly Dictionary<string, int> debugMockStates = new Dictionary<string, int>();
    readonly HashSet<string> debugMockNewUnlocks = new HashSet<string>();
#endif

    Sprite refreshSprite;
    Sprite closeSprite;
    readonly Sprite[,] filterSprites = new Sprite[7, 2];

    // ───────────────────────── 卡牌详情 overlay（Prefab 实例化，懒加载） ─────────────────────────
    RectTransform cardInfoRoot;
    CardInfoOverlayView cardInfoView;
    bool visible;
    bool built;

    float refreshFaceWidth;

    /// <summary>卡面显示宽度：每次刷新期间固定，避免实卡测量更新比例后改变后续未知占位尺寸。</summary>
    float CalculatedFaceWidth => cardFaceWidth > 0f ? cardFaceWidth : cardFaceHeight * Mathf.Max(0.3f, ReferenceAspect);
    float FaceWidth => refreshFaceWidth > 0f ? refreshFaceWidth : CalculatedFaceWidth;

    /// <summary>
    /// tile 宽高比的基准：以未知卡占位图（unknownCardSilhouette）自身宽高比为准——
    /// CardArchiveTile 预制体（见 RenderCard）的外框在未知/已知/已解锁三态下共享同一张素材、
    /// 同一块画布，天然保证基准恒定，不再让 tile 尺寸跟着某张具体卡的实测内容走。
    /// 没有占位图时退回旧的经验值兜底。
    /// </summary>
    float ReferenceAspect => unknownCardSilhouette != null
        ? unknownCardSilhouette.rect.width / unknownCardSilhouette.rect.height
        : DefaultCardFaceAspect;

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

    // ReferenceAspect 取不到未知占位图时的经验兜底值（由 choice1 实测）。
    const float DefaultCardFaceAspect = 0.63125f;

    public System.Action onClose;
    public static CardArchivePanel Instance { get; private set; }

    // ───────────────────────── 静态获取 ─────────────────────────

    public static CardArchivePanel EnsureInstance()
    {
        if (IsUsable(Instance)) return Instance;
        DisposeInvalidInstance(Instance);

        var existing = FindObjectOfType<CardArchivePanel>();
        if (IsUsable(existing)) return existing;
        DisposeInvalidInstance(existing);

        var prefab = Resources.Load<CardArchivePanel>("SystemUI/CardArchivePanel");
        if (prefab == null)
            throw new InvalidOperationException("缺少 Resources/SystemUI/CardArchivePanel Prefab，无法创建卡牌收藏界面。");

        var instance = Instantiate(prefab);
        if (!IsUsable(instance))
        {
            DisposeInvalidInstance(instance);
            throw new InvalidOperationException("CardArchivePanel Prefab 缺少必要的 UI 引用，无法创建卡牌收藏界面。");
        }

        DontDestroyOnLoad(instance.gameObject);
        return instance;
    }

    static bool IsUsable(CardArchivePanel panel) => panel != null && panel.enabled && panel.HasVisualLayout();

    static void DisposeInvalidInstance(CardArchivePanel panel)
    {
        if (panel == null) return;
        if (Instance == panel) Instance = null;
        if (Application.isPlaying) Destroy(panel.gameObject);
        else DestroyImmediate(panel.gameObject);
    }

    // ───────────────────────── 生命周期 ─────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (!HasVisualLayout())
        {
            Debug.LogError("[CardArchivePanel] CardArchivePanel Prefab 缺少必要的 UI 引用。", this);
            enabled = false;
            return;
        }

        built = true;
        LoadSystemUISprites();
        ApplyVisualAssets();
        BindVisualLayoutEvents();
        if (Application.isPlaying) panelRoot.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // Prefab 里静态摆放的 TMP 文本（标题/进度/状态/页签）字体只在编辑器工具 ApplyAllToActiveScene()
        // 批量替换时才会被设成含中文字形的字体；该工具只扫描"当前活动场景"的根物体，扫不到
        // Resources 下的 Prefab 资产本身，导致这个面板的中文一直显示成方块。这里强制在运行时
        // 对整个面板子树套用 Default 槽字体，和 RenderCard 里对卡面子树套用 Card 槽字体是同一套逻辑。
        // ⚠️ 必须放在 Start()（而非 Awake()）：TMP_Text 自身的 OnEnable 在同一帧内于所有组件的
        // Awake() 之后运行，会用序列化数据重新同步内部字体状态，把 Awake() 里刚设置的字体覆盖掉；
        // Start() 保证发生在整个场景当帧全部 Awake/OnEnable 结束之后，第一帧渲染之前，不会有闪烁。
        if (panelRoot != null && FontRegistry.Instance != null) FontRegistry.Instance.ApplyFontToTree(panelRoot.transform, FontSlots.Default);
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

    /// <summary>绑定 Prefab 静态布局，运行时仅刷新动态卡片数据。</summary>
    public void Build()
    {
        EnsureBuilt();
    }

    /// <summary>Prefab 布局由编辑器保存；运行时仅刷新动态卡片数据。</summary>
    public void Rebuild()
    {
        EnsureBuilt();
        if (visible) Refresh();
    }

    void EnsureBuilt()
    {
        if (built) return;
        if (!HasVisualLayout())
            throw new InvalidOperationException("CardArchivePanel 必须从 Resources/SystemUI/CardArchivePanel Prefab 实例化。");

        built = true;
        BindVisualLayoutEvents();
    }

    // 标题从动态 TMP 文本换成了美术图（Title 节点现在是 CATitle.png 的 Image，无 TMP 组件），
    // 所以这里不再把 titleText 作为必需引用——继续要求它会导致 HasVisualLayout 恒为 false，
    // EnsureInstance() 每次都抛异常，整个面板永远建不起来。
    bool HasVisualLayout() =>
        panelRoot != null && backgroundImage != null && contentRoot != null && scrollViewport != null && scrollRect != null &&
        refreshButton != null && closeButton != null && tabStyles != null && tabStyles.Length == 7 &&
        progressText != null && statusText != null && tabRow != null && progressFill != null;

    void BindVisualLayoutEvents()
    {
        EnsureTabStyles();
        ApplyResponsiveTabLayout();
        scrollRect.viewport = scrollViewport;
        scrollRect.content = contentRoot;
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        refreshButton.onClick.AddListener(Refresh);
        closeButton.onClick.AddListener(Hide);

        tabs.Clear();
        for (int i = 0; i < tabRow.childCount; i++)
        {
            var tab = tabRow.GetChild(i);
            if (!tab.name.StartsWith("Tab_")) continue;
            string tabKey = tab.name.Substring("Tab_".Length);
            tabs.Add(tabKey);
            var button = tab.GetComponent<Button>();
            if (button == null) continue;
            string captured = tabKey;
            button.onClick.AddListener(() => { currentTab = captured; Refresh(); });
        }
    }

    void OnRectTransformDimensionsChange()
    {
        if (built) ApplyResponsiveTabLayout();
    }

    void ApplyResponsiveTabLayout()
    {
        if (tabRow == null || panelRoot == null) return;
        var rootRect = panelRoot.GetComponent<RectTransform>();
        if (rootRect == null || rootRect.rect.width <= 1f) return;

        float requiredWidth = HallFilterWidth * 7f + HallFilterSpacing * 6f;
        float availableWidth = Mathf.Max(1f, rootRect.rect.width - HallFilterLeft * 2f);
        float scale = Mathf.Min(1f, availableWidth / requiredWidth);
        tabRow.localScale = Vector3.one * scale;
    }

    void LoadSystemUISprites()
    {
        var func = Resources.Load<Sprite>("SystemUI/Func Buttons");
        var funcTex = func != null ? func.texture : Resources.Load<Texture2D>("SystemUI/Func Buttons");
        if (funcTex != null)
        {
            refreshSprite = Sprite.Create(funcTex, new Rect(0f, 0f, 66f, 66f), new Vector2(0.5f, 0.5f), 100f);
            closeSprite = Sprite.Create(funcTex, new Rect(68f, 0f, 66f, 66f), new Vector2(0.5f, 0.5f), 100f);
        }

        var filter = Resources.Load<Sprite>("SystemUI/Card Filters");
        var filterTex = filter != null ? filter.texture : Resources.Load<Texture2D>("SystemUI/Card Filters");
        if (filterTex == null) return;

        const float rowHeight = 52.75f;
        const float textureHeight = 422f;
        for (int row = 0; row < 7; row++)
        {
            float y = textureHeight - (row + 1) * rowHeight;
            filterSprites[row, 0] = Sprite.Create(filterTex, new Rect(0f, y, 164f, rowHeight), new Vector2(0.5f, 0.5f), 100f);
            filterSprites[row, 1] = Sprite.Create(filterTex, new Rect(164f, y, 164f, rowHeight), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    void ApplyVisualAssets()
    {
        // 背景图和刷新/关闭图标都不再由代码按固定路径 Resources.Load 强制覆盖——直接使用
        // Prefab 里已经配好的贴图/颜色，换图只需要在 Prefab 里改，不会被这里的代码在运行时
        // 悄悄换回旧图（同 HallOfFamePanel 的修复思路）。
        ApplyIconButton(refreshButton, refreshIconSprite != null ? refreshIconSprite : refreshSprite);
        ApplyIconButton(closeButton, closeIconSprite != null ? closeIconSprite : closeSprite);
        RefreshTabVisuals();
    }

    static void ApplyIconButton(Button button, Sprite sprite)
    {
        if (button == null || sprite == null) return;
        button.image.sprite = sprite;
        button.image.color = Color.white;
        button.image.preserveAspect = true;
    }

    static int FilterRowFor(string key)
    {
        switch (key)
        {
            case "Pride": return 0;
            case "Envy": return 1;
            case "Sloth": return 2;
            case "Lust": return 3;
            case "Wrath": return 4;
            case "Gluttony": return 5;
            case "Greed": return 6;
            default: return -1;
        }
    }

    void RefreshTabVisuals()
    {
        EnsureTabStyles();
        for (int i = 0; i < tabStyles.Length; i++)
        {
            var style = tabStyles[i];
            var image = style != null ? style.image : null;
            if (image == null) continue;

            string key = image.transform.name.Substring("Tab_".Length);
            int row = FilterRowFor(key);
            bool selected = key == currentTab;
            Sprite configuredSprite = selected ? style.selectedSprite : style.normalSprite;
            Sprite fallbackSprite = row >= 0 ? filterSprites[row, selected ? 1 : 0] : null;
            if (configuredSprite != null || fallbackSprite != null)
                image.sprite = configuredSprite != null ? configuredSprite : fallbackSprite;
            image.color = selected ? style.selectedColor : style.normalColor;
            image.preserveAspect = true;
        }
    }

    void EnsureTabStyles()
    {
        if (tabStyles == null || tabStyles.Length != 7)
            Array.Resize(ref tabStyles, 7);
        for (int i = 0; i < tabStyles.Length; i++)
            if (tabStyles[i] == null) tabStyles[i] = new TabVisualStyle();
    }

    void RefreshTabNewUnlockTints()
    {
        if (tabStyles == null) return;
        for (int i = 0; i < tabStyles.Length; i++)
        {
            var style = tabStyles[i];
            if (style == null || style.image == null) continue;

            // 挂在页签按钮本身（style.image）上，而不是它的 Label 子物体：Label 这个 GameObject
            // 在 Prefab 里是关着的（m_IsActive: 0，具体原因未知，可能是本地化占位），挂在它下面
            // 的子物体不会渲染，红点会直接不可见——所以仍然挂在保证处于激活状态的按钮节点上，
            // 只是把 Label 的 TMP_Text 传进去用于计算文字实际宽高（见 AddFilterNewUnlockTint）。
            var existing = style.image.transform.Find("NewUnlockTint");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }

            string tab = style.image.transform.name.Substring("Tab_".Length);
            if (HasUnreadNewCard(tab))
            {
                var label = style.image.transform.Find("Label");
                AddFilterNewUnlockTint(style.image.rectTransform, label != null ? label.GetComponent<TMPro.TMP_Text>() : null);
            }
        }
    }

    bool HasUnreadNewCard(string tab)
    {
        var cards = CardsForTab(tab);
        for (int i = 0; i < cards.Count; i++)
        {
            if (IsNewUnlock(cards[i].effectId)) return true;
        }
        return false;
    }

    /// <summary>
    /// parent：页签按钮本体（激活状态，安全的挂载点）。label：按钮里的文字（可能是禁用状态，
    /// 只用来读取文本尺寸，不作为挂载点）。按钮本身比"傲慢"这类两字文案宽得多（HorizontalLayoutGroup
    /// 把 7 个页签撑成等宽），文字在按钮里是居中对齐的——如果直接把红点锚定到按钮的右上角，
    /// 会落在文字右侧一大截空白之外，而不是紧贴文字本身的右上角。这里用 TMP 的
    /// GetPreferredValues() 量出文字的真实渲染宽高（这个方法不依赖对象是否处于激活状态），
    /// 换算出文字右上角相对按钮右上角的偏移量，让红点始终贴着文字走，不随按钮宽度或文案长短跑偏。
    /// </summary>
    void AddFilterNewUnlockTint(RectTransform parent, TMPro.TMP_Text label)
    {
        if (parent == null || filterNewUnlockTintSprite == null) return;
        var tint = new GameObject("NewUnlockTint", typeof(RectTransform), typeof(Image));
        tint.transform.SetParent(parent, false);
        var rt = tint.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(13f, 13f);

        Vector2 offset = Vector2.zero;
        if (label != null)
        {
            var textSize = label.GetPreferredValues();
            var buttonSize = parent.rect.size;
            offset = new Vector2((textSize.x - buttonSize.x) * 0.5f, (textSize.y - buttonSize.y) * 0.5f);
        }
        rt.anchoredPosition = offset;

        var image = tint.GetComponent<Image>();
        image.sprite = filterNewUnlockTintSprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    void AddNewUnlockTint(RectTransform parent, Sprite tintSprite, float size)
    {
        if (parent == null || tintSprite == null) return;
        var tint = new GameObject("NewUnlockTint", typeof(RectTransform), typeof(Image));
        tint.transform.SetParent(parent, false);
        var rt = tint.GetComponent<RectTransform>();
        // 中心（而非自身右上角）与 slot 的右上角重合，跟 AddFilterNewUnlockTint 用同一套对齐方式。
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        var image = tint.GetComponent<Image>();
        image.sprite = tintSprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    void Clear()
    {
        if (panelRoot != null) { DestroyImmediate(panelRoot); panelRoot = null; }
        backgroundImage = null;
        contentRoot = null; scrollViewport = null; scrollRect = null;
        refreshButton = closeButton = null;
        tabStyles = null;
        progressText = statusText = null;
        tabRow = null; progressFill = null;
        built = false;
    }
    private static string TranslateArchiveTabLabel(string label)
    {
        switch (label)
        {
            case "Pride": return "傲慢";
            case "Lust": return "色欲";
            case "Wrath": return "愤怒";
            case "Greed": return "贪婪";
            case "Gluttony": return "暴食";
            case "Envy": return "嫉妒";
            case "Sloth": return "怠惰";
            default: return label;
        }
    }

    private void TranslateArchiveTabLabels()
    {
        if (tabRow == null) return;
        var labels = tabRow.GetComponentsInChildren<TMPro.TMP_Text>(true);
        for (var i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null) labels[i].text = TranslateArchiveTabLabel(labels[i].text);
        }
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

        // 标题（纯代码回退布局专用；Prefab 正常路径下标题是美术图，不走这里）
        AddText(prt, "卡牌图鉴", EnsureSize(titleFontSize, 30), new Vector2(0, cursor - 40f), TextAlignmentOptions.Center, (int)pw - 40);
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
        scrollViewport = viewport;
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

    /// <summary>构建七罪页签按钮（仅旧版纯代码布局回退使用）。</summary>
    void BuildTabs()
    {
        if (tabRow == null) return;
        tabs.Clear();
        foreach (SinType s in System.Enum.GetValues(typeof(SinType)))
            if (s != SinType.None) tabs.Add(s.ToString());

        foreach (var t in tabs)
        {
            var btn = new GameObject("Tab_" + t, typeof(RectTransform));
            var brt = btn.GetComponent<RectTransform>();
            brt.SetParent(tabRow, false);
            brt.sizeDelta = new Vector2(130, tabRowHeight - 8f);
            btn.AddComponent<Image>().color = EnsureColor(tabNormalColor, DefTabNormal);
            var b = btn.AddComponent<Button>();
            AddText(brt, t, EnsureSize(tabFontSize, 36), Vector2.zero, TextAlignmentOptions.Center, 130);
            var captured = t;
            b.onClick.AddListener(() => { currentTab = captured; Refresh(); });
        }

        TranslateArchiveTabLabels();
    }

    // ───────────────────────── 显隐 / 刷新 ─────────────────────────

    public void Show()
    {
        bool wasVisible = visible;
        EnsureBuilt();
        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        ApplyResponsiveTabLayout();
        visible = true;
        // 新卡角标保留到卡牌真正查看后再清除，不在打开图鉴时全部清空。
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void EnableDebugPreviewData()
    {
        if (GameManager.IsFormalFlow) return;

        debugMockStates.Clear();
        debugMockNewUnlocks.Clear();
        var library = CardLibrary.Instance;
        if (library == null || library.cards == null) return;

        int index = 0;
        for (int i = 0; i < library.cards.Count; i++)
        {
            var card = library.cards[i];
            if (card == null || string.IsNullOrEmpty(card.effectId) || !library.IsEffectEnabled(card.effectId)) continue;
            int state = index % 3;
            debugMockStates[card.effectId] = state;
            if (state == CardArchiveStore.Unlocked && index % 2 == 0)
                debugMockNewUnlocks.Add(card.effectId);
            index++;
        }

        Refresh();
        Debug.Log($"[CardArchive] 已启用 {debugMockStates.Count} 张卡的开发 Mocking Data（仅内存，不写入图鉴存档）。");
    }

    public void DisableDebugPreviewData()
    {
        if (GameManager.IsFormalFlow || debugMockStates.Count == 0) return;
        debugMockStates.Clear();
        debugMockNewUnlocks.Clear();
        Refresh();
        Debug.Log("[CardArchive] 已关闭开发 Mocking Data，恢复真实图鉴数据。");
    }

    bool IsDebugPreviewActive => debugMockStates.Count > 0;
#endif

    int DisplayStateOf(string cardId)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugMockStates.TryGetValue(cardId, out int state)) return state;
#endif
        return CardArchiveStore.StateOf(cardId);
    }

    bool IsNewUnlock(string cardId)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (IsDebugPreviewActive) return debugMockNewUnlocks.Contains(cardId);
#endif
        var entry = CardArchiveStore.GetEntry(cardId);
        return entry != null && entry.isNewUnread;
    }

    int DisplayUnlockedCount()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (IsDebugPreviewActive)
        {
            int count = 0;
            foreach (var state in debugMockStates.Values)
                if (state == CardArchiveStore.Unlocked) count++;
            return count;
        }
#endif
        return CardArchiveStore.UnlockedCount();
    }

    int DisplayTotalCount()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (IsDebugPreviewActive) return debugMockStates.Count;
#endif
        return CardArchiveStore.ValidCardTotal;
    }

    bool fontFixApplied;

    public void Refresh()
    {
        // 兜底：正常情况下 Start() 已经套用过 Default 槽字体（见 Start() 里的说明）；
        // 这里再保险一次，避免任何生命周期时序意外（如 EnsureBuilt 走 Show() 触发而非 Awake）
        // 导致标题/进度/状态文本停留在没有中文字形的默认字体上、显示成方块。
        if (!fontFixApplied && panelRoot != null && FontRegistry.Instance != null)
        {
            FontRegistry.Instance.ApplyFontToTree(panelRoot.transform, FontSlots.Default);
            fontFixApplied = true;
        }
        TranslateArchiveTabLabels();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!IsDebugPreviewActive)
#endif
            CardArchiveStore.SyncCurrentLibrary();
        // currentTab 兜底：为空（序列化/AddComponent 时机导致）时按默认傲慢页处理。
        if (string.IsNullOrEmpty(currentTab)) currentTab = "Pride";
        if (!built || contentRoot == null) return;

        // 清空（同步销毁，避免与同帧 Instantiate 叠加导致格子重复累积）
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            DestroyImmediate(contentRoot.GetChild(i).gameObject);

        var horizontalLayout = contentRoot.GetComponent<HorizontalLayoutGroup>();
        if (horizontalLayout != null)
        {
            horizontalLayout.spacing = cardSpacing.x;
            horizontalLayout.padding = PaddingRect;
        }

        var cards = CardsForCurrentTab();
        refreshFaceWidth = CalculatedFaceWidth;
        int knownCount = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            int state = DisplayStateOf(card.effectId);
            if (state >= CardArchiveStore.Known) knownCount++;
            RenderCatalogCard(card, state);
        }

        int unlocked = DisplayUnlockedCount();
        int totalAll = DisplayTotalCount();
        progressText.text = TextCatalog.Get("ui.archive.unlocked_count", unlocked, totalAll);
        statusText.text = $"{SinDisplayName(currentTab)}：已知 {knownCount} / {cards.Count} 张";
        statusText.color = new Color(0.9f, 0.9f, 0.9f);

        if (progressFill != null)
        {
            float ratio = totalAll > 0 ? (float)unlocked / totalAll : 0f;
            float trackW = (progressBarSize.x > 1f ? progressBarSize.x : 800f) - 4f;
            progressFill.rectTransform.sizeDelta = new Vector2(trackW * Mathf.Clamp01(ratio), -4f);
        }

        UpdateTabHighlight();
        RefreshTabNewUnlockTints();

        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.StopMovement();
            scrollRect.normalizedPosition = new Vector2(0f, 0.5f); // 回到最左侧
        }
    
        // 新卡角标由卡牌实际查看流程清除，不在刷新列表时批量清除。
    }

    List<CardData> CardsForCurrentTab() => CardsForTab(string.IsNullOrEmpty(currentTab) ? "Pride" : currentTab);

    List<CardData> CardsForTab(string tab)
    {
        var result = new List<CardData>();
        var library = CardLibrary.Instance;
        if (library == null || library.cards == null) return result;

        for (int i = 0; i < library.cards.Count; i++)
        {
            var card = library.cards[i];
            if (card == null || string.IsNullOrEmpty(card.effectId) || !library.IsEffectEnabled(card.effectId)) continue;
            if (card.monsterType != SinType.None && card.monsterType.ToString() == tab)
                result.Add(card);
        }
        return result;
    }

    static string SinDisplayName(string tab)
    {
        switch (tab)
        {
            case "Pride": return "傲慢";
            case "Lust": return "色欲";
            case "Sloth": return "怠惰";
            case "Wrath": return "暴怒";
            case "Envy": return "嫉妒";
            case "Greed": return "贪婪";
            case "Gluttony": return "暴食";
            default: return "卡牌";
        }
    }

    void UpdateTabHighlight()
    {
        RefreshTabVisuals();
    }

    // ───────────────────────── 卡片渲染 ─────────────────────────

    void RenderCatalogCard(CardData card, int state)
    {
        var tile = CreateCardTile("Card_" + card.effectId);
        RenderCard(tile, card, state);

        if (IsNewUnlock(card.effectId))
            AddNewUnlockTint(tile, cardNewUnlockTintSprite, 44f);

        // 点击卡面＝"查看过了"：清掉这张卡的新解锁角标；如果该页签下已经没有卡带角标，
        // 顺带把页签自己的角标也摘掉（见 MarkCardViewed）。已知/已解锁的卡额外弹出详情
        // overlay；未知卡（剪影）没有内容可看，不弹窗。
        var clickCatcher = tile.Find("ClickCatcher")?.GetComponent<Button>();
        if (clickCatcher != null)
        {
            string effectId = card.effectId;
            int capturedState = state;
            clickCatcher.onClick.AddListener(() =>
            {
                MarkCardViewed(effectId, tile);
                if (capturedState >= CardArchiveStore.Known)
                    ShowCardInfo(card, capturedState);
            });
        }
    }

    RectTransform CreateCardTile(string name)
    {
        var tile = new GameObject(name, typeof(RectTransform));
        var trt = tile.GetComponent<RectTransform>();
        trt.SetParent(contentRoot, false);
        trt.sizeDelta = new Vector2(FaceWidth, cardFaceHeight);
        var layout = tile.AddComponent<LayoutElement>();
        layout.minWidth = layout.preferredWidth = FaceWidth;
        layout.minHeight = layout.preferredHeight = cardFaceHeight;

        // 裁剪只作用在这层内容子物体上（卡面/占位图的出血内容需要在卡槽边缘被裁掉）；
        // tile 本体不挂遮罩，新解锁角标（AddNewUnlockTint）挂在 tile 本体上才不会被这层
        // 遮罩切掉骑在角上的那一半——角标要在裁剪层之上单独一层，不受它约束。
        var content = new GameObject("Content", typeof(RectTransform), typeof(RectMask2D));
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.SetParent(trt, false);
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;

        // 透明的可点击层：卡面内部的图片都关掉了 raycastTarget（只读展示），需要单独一层
        // 接收点击，用来判定"这张卡被查看过了"。铺满整个 tile，不挡卡面显示。
        var clickCatcher = new GameObject("ClickCatcher", typeof(RectTransform), typeof(Image), typeof(Button));
        var clickRt = clickCatcher.GetComponent<RectTransform>();
        clickRt.SetParent(trt, false);
        clickRt.anchorMin = Vector2.zero;
        clickRt.anchorMax = Vector2.one;
        clickRt.offsetMin = Vector2.zero;
        clickRt.offsetMax = Vector2.zero;
        var clickImg = clickCatcher.GetComponent<Image>();
        clickImg.color = new Color(1f, 1f, 1f, 0.001f);
        clickImg.raycastTarget = true;
        var clickBtn = clickCatcher.GetComponent<Button>();
        clickBtn.transition = Selectable.Transition.None;

        // 悬停微放大：挂在实际接收射线的 ClickCatcher 上，缩放整个 tile（trt）。
        var hover = clickCatcher.AddComponent<UICardHoverScale>();
        hover.target = trt;

        return trt;
    }

    /// <summary>
    /// 卡面被点击查看后调用：清掉这张卡自己的新解锁角标（真实数据走
    /// CardArchiveStore.MarkRead，调试预览数据走 debugMockNewUnlocks），
    /// 再刷新一遍页签角标——该页签下如果已经没有带角标的卡，页签自己的角标也一并摘掉。
    /// </summary>
    void MarkCardViewed(string effectId, RectTransform tile)
    {
        if (string.IsNullOrEmpty(effectId)) return;

        bool wasNew = IsNewUnlock(effectId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (IsDebugPreviewActive)
            debugMockNewUnlocks.Remove(effectId);
        else
#endif
            CardArchiveStore.MarkRead(effectId);
        if (!wasNew) return;

        var existing = tile.Find("NewUnlockTint");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        RefreshTabNewUnlockTints();
    }

    // ───────────────────────── 卡牌详情 overlay ─────────────────────────

    /// <summary>点开已知/已解锁卡片时弹出的详情面板：已知只显示名称，已解锁额外显示效果说明。</summary>
    void ShowCardInfo(CardData card, int state)
    {
        if (card == null) return;
        EnsureCardInfoOverlay();

        cardInfoRoot.gameObject.SetActive(true);
        cardInfoRoot.SetAsLastSibling();

        var artHost = cardInfoView.ArtHost;
        for (int i = artHost.childCount - 1; i >= 0; i--)
        {
            var child = artHost.GetChild(i);
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }
        var prefab = ResolveTilePrefab();
        if (prefab != null)
        {
            // ArtHost 挂了 AspectRatioFitter（WidthControlsHeight），比例从卡面预制体自身的
            // RectTransform 尺寸实时算出——不写死数字，预制体尺寸以后变了这里也跟着变，
            // 保证任何分辨率下卡面都不会被拉伸变形。
            var fitter = artHost.GetComponent<AspectRatioFitter>();
            if (fitter != null)
            {
                var prefabRt = (RectTransform)prefab.transform;
                if (prefabRt.sizeDelta.y > 0.01f)
                    fitter.aspectRatio = prefabRt.sizeDelta.x / prefabRt.sizeDelta.y;
            }

            var inst = Instantiate(prefab, artHost, false);
            var rt = (RectTransform)inst.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            if (FontRegistry.Instance != null) FontRegistry.Instance.ApplyFontToTree(inst.transform, FontSlots.Card);
            var view = inst.GetComponent<CardArchiveTileView>();
            if (view != null) view.Bind(card, state, unknownCardSilhouette);
            foreach (var img in inst.GetComponentsInChildren<Image>(true)) img.raycastTarget = false;
        }

        cardInfoView.NameText.text = card.ResolveCardName();

        bool unlocked = state >= CardArchiveStore.Unlocked;
        cardInfoView.EffectSection.SetActive(unlocked);
        if (unlocked) cardInfoView.EffectText.text = card.ResolveDescription() ?? "";
    }

    void HideCardInfo()
    {
        if (cardInfoRoot != null) cardInfoRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// 懒加载实例化卡牌详情 overlay——布局本身由 Resources/SystemUI/CardInfoOverlay Prefab
    /// 决定（策划可在 Prefab 编辑模式下直接可视化调整边框、卡面区域、名称/效果框，跟卡牌图鉴
    /// 主面板自身走 Prefab + 序列化引用是同一套思路），这里只挂到 panelRoot 下铺满全屏、
    /// 补上运行时才能确定的关闭图标（Func Buttons 图集切片是运行时用 Sprite.Create 切出来的，
    /// 不是可序列化进 Prefab 的资产），并把点背景/点右上角关闭都接到 HideCardInfo。
    /// </summary>
    void EnsureCardInfoOverlay()
    {
        if (cardInfoRoot != null) return;

        var prefab = Resources.Load<GameObject>("SystemUI/CardInfoOverlay");
        if (prefab == null) { Debug.LogError("[CardArchive] 缺少 Resources/SystemUI/CardInfoOverlay Prefab。"); return; }

        var inst = Instantiate(prefab, panelRoot.transform, false);
        var box = (RectTransform)inst.transform;
        box.anchorMin = Vector2.zero;
        box.anchorMax = Vector2.one;
        box.offsetMin = Vector2.zero;
        box.offsetMax = Vector2.zero;
        cardInfoRoot = box;

        cardInfoView = inst.GetComponent<CardInfoOverlayView>();
        if (FontRegistry.Instance != null) FontRegistry.Instance.ApplyFontToTree(box, FontSlots.Default);

        var backgroundBtn = inst.GetComponent<Button>();
        if (backgroundBtn != null) backgroundBtn.onClick.AddListener(HideCardInfo);

        // 关闭图标优先用 Prefab 里配置的 closeIconSprite，不配置时才回退运行时切图（同 CardArchivePanel/HallOfFamePanel 的思路）。
        ApplyIconButton(cardInfoView.CloseButton, cardInfoView.CloseIconSprite != null ? cardInfoView.CloseIconSprite : closeSprite);
        cardInfoView.CloseButton.onClick.AddListener(HideCardInfo);

        cardInfoRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// 卡牌图鉴专用渲染入口：使用独立的 CardArchiveTile 预制体（CardArchiveTileView），
    /// 不复用局内选卡 UI（choice1/CoreChoiceCard）。该预制体的外框（Frame）无论未知/已知/已解锁
    /// 状态都用同一张素材（unknownCardSilhouette）、同一块画布比例，插画图层在这块固定画布内
    /// 按统一比例锚定——不再依赖对任何单张卡内容的实测，从根上保证图鉴里所有卡片视觉大小一致。
    /// </summary>
    void RenderCard(RectTransform tile, CardData card, int state)
    {
        var prefab = ResolveTilePrefab();
        if (prefab == null) return;

        // 卡面实例化到带 RectMask2D 的 Content 子物体里（出血内容在卡槽边缘被裁掉）；
        // 找不到时退回 tile 本体兜底，保证旧版/异常情况下依然能画出卡面。
        var contentParent = tile.Find("Content") as RectTransform ?? tile;
        var inst = Instantiate(prefab, contentParent, false);
        inst.name = "CardArchiveTile";
        var rt = (RectTransform)inst.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        if (FontRegistry.Instance != null) FontRegistry.Instance.ApplyFontToTree(inst.transform, FontSlots.Card);

        var view = inst.GetComponent<CardArchiveTileView>();
        if (view != null) view.Bind(card, state, unknownCardSilhouette);

        foreach (var img in inst.GetComponentsInChildren<Image>(true))
            img.raycastTarget = false;
    }

    GameObject ResolveTilePrefab()
    {
        if (tilePrefab != null) return tilePrefab;
        tilePrefab = Resources.Load<GameObject>("SystemUI/CardArchiveTile");
        return tilePrefab;
    }

    /// <summary>卡片底部信息条：卡名 / 状态 / 次数（Unlocked）。与卡面区上下分离，互不遮挡。</summary>
    void BuildInfoBar(RectTransform tile, CardArchiveEntry e)
    {
        // 三态展示：未知为剪影；已知和已解锁均展示卡面，其中已知保留灰度区分。
        int displayState = !string.IsNullOrEmpty(e.cardId) ? CardArchiveStore.StateOf(e.cardId) : CardArchiveStore.Unknown;
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
        line.gameObject.AddComponent<Image>().color = displayState == 2
            ? new Color(0.8f, 0.65f, 0.25f, 0.85f)
            : new Color(0.5f, 0.5f, 0.55f, 0.6f);

        float w = FaceWidth;
        string title = displayState == 0 ? "？？？" : (e.cardName ?? "？？？");
        var titleTxt = AddText(barRT, title, EnsureSize((int)infoBarFontSizes.x, 18),
            new Vector2(0, infoBarHeight * 0.30f), TextAlignmentOptions.Center, w);
        titleTxt.color = displayState == 0 ? new Color(0.55f, 0.55f, 0.55f) : Color.white;

        string desc = displayState == 0 ? "未解锁" : (displayState == 1 ? "已遇见 · 未获得" : "已解锁");
        var descTxt = AddText(barRT, desc, EnsureSize((int)infoBarFontSizes.y, 18),
            new Vector2(0, -infoBarHeight * 0.02f), TextAlignmentOptions.Center, w);
        descTxt.color = new Color(0.85f, 0.85f, 0.85f);

        if (displayState == 2)
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
                var bt = AddText(badge, "新", 14, Vector2.zero, TextAlignmentOptions.Center, 52);
                bt.color = Color.white;
                bt.fontStyle = FontStyles.Bold;
            }
        }
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
