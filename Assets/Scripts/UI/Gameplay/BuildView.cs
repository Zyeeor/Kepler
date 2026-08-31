using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 显示构筑信息组件（Build View）。
/// 这是一个自包含组件：把它挂到任意 UI GameObject（如 UICanvas）上，该物体即拥有“显示构筑”的能力；
/// 所有显示配置（扇形参数、左上角迷你卡条参数、卡牌预制体等）直接在本组件的 Inspector 上编辑。
///
/// 交互：点按“构筑”按钮循环三态：
///   0 左上展开（迷你卡横向排开，常驻 HUD，不暂停）
///   1 放大展开（CardArcLayout 弧形排布，暂停查看）
///   2 左上收起（所有迷你卡完全重叠，并置于构筑卡面下方）

/// 卡片复用 CoreChoiceCard 预制体渲染为只读模式（隐藏文本/按钮）。
///
/// 构筑按钮为场景内静态对象：设计者在 UICanvas 下摆放一个命名为 BuildButton 的 Button（或把引用拖到 buildButton 字段），
/// 本组件按名自发现；点击即循环切换显示模式。
/// </summary>
public class BuildView : MonoBehaviour
{
    [Header("卡牌来源（缺省回退到 CoreChoiceUI.cardPrefab）")]
    [Tooltip("用于渲染每张卡的预制体（只读模式会隐藏其文本/按钮）。留空则自动使用 CoreChoiceUI.cardPrefab。一般不需要改。")]
    [SerializeField] GameObject cardPrefab;

    [Header("Build Button（场景中静态摆放，由设计者调整样式/图标）")]
    [Tooltip("场景中已摆放好的构筑按钮（命名为 BuildButton）。点击循环切换显示模式。也可直接把 Button 拖到这里。")]
    public Button buildButton;

    [Header("扇形布局（模式 1：半屏扇形放大，按构筑第二次进入）")]
    [Tooltip("控制点击构筑后第二次进入的“半屏扇形放大”面板。")]
    public float radius = 1000f;           // 弧线半径：越大卡片离屏幕中心越远、排得越开
    public float maxSpreadDeg = 100f;      // 全部卡片的最大总张角（度），限制扇形展开的最大宽度
    public float perCardDeg = 16f;         // 相邻两张卡之间的夹角（度），即扇形上卡片的“间隔”
    public float baseYOffset = 360f;       // 扇形整体相对屏幕中心的竖直上移量（像素），调整扇形高低
    public float scaleMultiplier = 1.2f;   // 扇形模式卡片相对原始卡面的放大倍率

    [Header("迷你卡条（模式 0：左上角一排，默认常驻 HUD）")]
    [Tooltip("控制默认左上角常驻的迷你卡条。")]
    public float miniScale = 0.15f;       // 迷你卡相对原始卡面的缩放（原始卡面约 100×100）；调大卡更大更宽
    float miniCardW = 80f;         // 实际视觉卡面宽度 × miniScale，用于 hover 命中层
    float miniCardH = 80f;         // 实际视觉卡面高度 × miniScale，用于 hover 命中层
    float miniSlotW = 15f;         // 原始卡片根宽度 × miniScale，用于保持既有布局位置
    float miniSlotH = 15f;         // 原始卡片根高度 × miniScale，用于保持既有布局位置
    Vector2 miniVisualCenter;
    public float miniSpacing = 60f;       // ★ 迷你卡之间的间隔（像素）。想让模式0卡片排得更松/更紧，调这个值
    public Vector2 miniAnchor = new Vector2(76f, -32f); // 迷你卡条距屏幕左上角的偏移（X 右移，Y 上移）；默认让卡条落在构筑按钮右侧并与其垂直居中

    [Header("左上收起（模式 2：所有卡叠在构筑卡面下方）")]
    [Tooltip("兼容旧场景的堆叠偏移配置；左上收起状态固定完全重叠，不使用该偏移。")]
    public Vector2 stackOffset = Vector2.zero;
    [Tooltip("左上角两种模式（迷你一排 / 堆叠）要隐藏的卡面子物体名：卡面预制体里溢出卡框的装饰图层（如 Image (1)）缩小后会变成碍眼的色块，这里按需裁掉。留空则不裁剪；扇形放大模式不受影响。")]
    public List<string> miniHiddenChildren = new List<string> { "Image (1)" };
    [Tooltip("仅「左上收起」（模式 2）额外隐藏的卡面子物体名：收起时所有卡完全重叠，卡面立绘与装饰图层（如 monster、若干 Image）会糊成一片，这里按需裁掉。迷你一排与扇形放大模式不受影响，切回即恢复显示。名字按全名匹配，同名物体会全部隐藏。")]
    public List<string> stackHiddenChildren = new List<string> { "monster", "Image", "Image (1)", "Image (2)" };
    [Tooltip("左上收起状态的卡面中心位置（屏幕空间 RectTransform）。留空则回退到 miniAnchor 位置。")]
    public RectTransform stackAnchor;

    [Header("模式切换动效")]
    [Tooltip("单张卡过渡到目标位置与缩放的时长（秒）。启用时间上限后，该值与 transitionStagger 的比值决定「每张卡时间片」的分配比例。")]
    public float transitionDuration = 0.2f;
    [Tooltip("相邻两张卡之间的过渡延迟（秒），实现逐张依次飞入/飞出。启用时间上限后按同一比例缩放。")]
    public float transitionStagger = 0.05f;

    [Header("模式切换动效：各布局转换总时长上限（秒）")]
    [Tooltip("模式 0（左上展开/迷你卡条）转换总时长上限。每张卡时长 = 上限 / 卡牌数量。<=0 表示不设上限，沿用上面的单卡时长。")]
    public float miniTransitionTimeCap = 0.6f;
    [Tooltip("模式 1（半屏扇形放大）转换总时长上限。每张卡时长 = 上限 / 卡牌数量。<=0 表示不设上限。")]
    public float fanTransitionTimeCap = 0.6f;
    [Tooltip("模式 2（左上收起/堆叠）转换总时长上限。每张卡时长 = 上限 / 卡牌数量。<=0 表示不设上限。")]
    public float stackTransitionTimeCap = 0.6f;

    [Header("Debug Toggles（调试开关）")]
    [Tooltip("没有卡的时候是否显示提示文本（如“尚未获得任何卡片”）。关闭则无卡时什么都不显示。")]
    public bool showEmptyHint = true;
    [Tooltip("灵魂态（未附身、CurrentBody 为 null）时是否显示构筑。关闭则灵魂态下不展示任何构筑信息。")]
    public bool showInSoulState = true;

    // 运行期 UI
    GameObject panelRoot;                 // 模式 1 的整屏扇形面板
    RectTransform cardParent;
    TMP_Text titleText;
    TMP_Text emptyHint;
    Button closeButton;
    CardArcLayout layout;

    GameObject miniBar;                   // 模式 0 的左上角迷你卡条容器
    RectTransform miniCardParent;
    TMP_Text miniEmptyHint;

    readonly List<GameObject> cardInstances = new List<GameObject>();
    struct CardPose { public Vector3 worldPos; public Vector3 localScale; }
    readonly List<CardPose> lastPoses = new List<CardPose>();
    readonly List<CardPose> targetPoses = new List<CardPose>();
    Coroutine transitionRoutine;
    int mode = 0;                         // 0=左上展开 1=放大展开 2=左上收起
    bool paused = false;
    bool initialized = false;
    bool _soundArmed;                     // 构筑切换音守卫：跳过 Initialize 默认 SetMode 的首次误响

    // 附身状态追踪：附身怪 / 灵魂态 / 换身 变化时实时刷新构筑（Update 轮询，与 AbilityCooldownUI 同模式）
    MonsterActor trackedBody;
    PossessionManager.SwitchState trackedState = PossessionManager.SwitchState.Idle;
    bool trackedStateInited = false;

    const int ModeMini = 0;
    const int ModeFan = 1;
    const int ModeStack = 2;

    void Start()
    {
        if (!initialized) Initialize();
    }

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;

        BuildPanel();
        BuildMiniBar();

        if (buildButton == null)
        {
            // BuildView 挂在 UIManager/UICanvas 下，而 BuildButton 在 UICanvas 下，故向上找到 Canvas 再搜索
            var root = GetComponentInParent<Canvas>()?.transform ?? transform.root;
            var btnGO = root != null ? FindDescendant(root, "BuildButton") : null;
            if (btnGO != null) buildButton = btnGO.GetComponent<Button>();
        }
        if (buildButton != null)
        {
            // 按钮在所有模式下都要可点（用于循环切换），故置到 Canvas 最后渲染于最上层
            buildButton.transform.SetAsLastSibling();
            buildButton.onClick.RemoveListener(CycleMode);
            buildButton.onClick.AddListener(CycleMode);
        }
        else
        {
            Debug.LogWarning("[BuildView] buildButton 未找到：请在 UICanvas 下放置名为 BuildButton 的按钮（可带自定义图标），或把 Button 拖到本组件的 buildButton 字段。");
        }

        // 卡池变化时，若当前停留在迷你条则实时刷新
        CardManager.OnEffectUnlocked -= OnCardsChanged;
        CardManager.OnEffectUnlocked += OnCardsChanged;

        SetMode(ModeMini); // 默认：左上角迷你卡条常驻
    }

    void OnDestroy()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }
        CardManager.OnEffectUnlocked -= OnCardsChanged;
    }

    void OnCardsChanged(CardData card)
    {
        if (mode == ModeMini || mode == ModeStack) RefreshMini(IsStackMode);
    }

    /// <summary>附身状态变化（附身 / 离开附身 / 更换附身）时实时刷新当前模式的构筑。
    /// 用轮询而非事件：无需处理 PossessionManager 初始化时序，且天然覆盖附身怪死亡等边角。</summary>
    void Update()
    {
        var pm = PossessionManager.Instance;
        if (pm == null) return;
        if (!trackedStateInited || pm.CurrentBody != trackedBody || pm.State != trackedState)
        {
            trackedStateInited = true;
            trackedBody = pm.CurrentBody;
            trackedState = pm.State;
            if (mode == ModeMini || mode == ModeStack) RefreshMini(IsStackMode);
            else if (mode == ModeFan) PopulateFan();
        }
    }

    /// <summary>在 root 子树中按名递归查找（含自身）。</summary>
    static Transform FindDescendant(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindDescendant(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    // ───────────────────────── 模式 1：扇形放大面板 ─────────────────────────
    void BuildPanel()
    {
        panelRoot = new GameObject("BuildPanel", typeof(RectTransform), typeof(Image));
        panelRoot.transform.SetParent(transform, false);
        var prt = panelRoot.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = prt.offsetMax = Vector2.zero;
        var bg = panelRoot.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.65f);
        bg.raycastTarget = true;
        panelRoot.SetActive(false);

        // 标题
        titleText = new GameObject("Title", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        var tt = titleText.rectTransform;
        tt.SetParent(prt, false);
        tt.anchorMin = tt.anchorMax = new Vector2(0.5f, 1f);
        tt.pivot = new Vector2(0.5f, 1f);
        tt.anchoredPosition = new Vector2(0f, -40f);
        tt.sizeDelta = new Vector2(900f, 80f);
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 44;
        titleText.color = new Color(1f, 0.85f, 0.6f);
        if (FontRegistry.Instance != null) FontRegistry.Instance.ApplyFontToText(titleText, FontSlots.Default);

        // 空状态提示
        emptyHint = new GameObject("EmptyHint", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        var eh = emptyHint.rectTransform;
        eh.SetParent(prt, false);
        eh.anchorMin = eh.anchorMax = new Vector2(0.5f, 0.5f);
        eh.pivot = new Vector2(0.5f, 0.5f);
        eh.anchoredPosition = Vector2.zero;
        eh.sizeDelta = new Vector2(900f, 120f);
        emptyHint.alignment = TextAlignmentOptions.Center;
        emptyHint.fontSize = 32;
        emptyHint.color = new Color(1f, 1f, 1f, 0.7f);
        emptyHint.text = TextCatalog.Get("ui.build.empty_all");
        if (FontRegistry.Instance != null) FontRegistry.Instance.ApplyFontToText(emptyHint, FontSlots.Default);
        emptyHint.gameObject.SetActive(false);

        // 卡片容器（屏幕底部居中，经 CardArcLayout 弧形排布）
        var cp = new GameObject("CardContainer", typeof(RectTransform)).GetComponent<RectTransform>();
        cp.SetParent(prt, false);
        cp.anchorMin = cp.anchorMax = new Vector2(0.5f, 0f);
        cp.pivot = new Vector2(0.5f, 0f);
        cp.anchoredPosition = Vector2.zero;
        cp.sizeDelta = new Vector2(1920f, 1080f);
        cardParent = cp;
        layout = cp.gameObject.AddComponent<CardArcLayout>();
        layout.radius = radius;
        layout.maxSpreadDeg = maxSpreadDeg;
        layout.perCardDeg = perCardDeg;
        layout.baseYOffset = baseYOffset;
        layout.safeMargin = 40f;
        layout.scaleMultiplier = scaleMultiplier; // 扇形放大模式：卡片放大倍率（来自本组件配置）

        // 返回按钮：从扇形模式回到左上角迷你条
        closeButton = new GameObject("CloseButton", typeof(RectTransform), typeof(Button), typeof(Image)).GetComponent<Button>();
        var cb = closeButton.GetComponent<RectTransform>();
        cb.SetParent(prt, false);
        cb.anchorMin = cb.anchorMax = new Vector2(1f, 1f);
        cb.pivot = new Vector2(1f, 1f);
        cb.anchoredPosition = new Vector2(-60f, -60f);
        cb.sizeDelta = new Vector2(160f, 64f);
        closeButton.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f, 0.9f);
        var cl = new GameObject("Label", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        var clr = cl.rectTransform;
        clr.SetParent(cb, false);
        clr.anchorMin = Vector2.zero; clr.anchorMax = Vector2.one;
        clr.offsetMin = clr.offsetMax = Vector2.zero;
        cl.text = TextCatalog.Get("ui.build.back"); cl.alignment = TextAlignmentOptions.Center; cl.fontSize = 26; cl.color = Color.white;
        if (FontRegistry.Instance != null) FontRegistry.Instance.ApplyFontToText(cl, FontSlots.Default);
        closeButton.onClick.AddListener(() => SetMode(ModeMini));
    }

    // ───────────────────────── 模式 0：左上角迷你卡条 ─────────────────────────
    void BuildMiniBar()
    {
        miniBar = new GameObject("MiniBar", typeof(RectTransform));
        miniBar.transform.SetParent(transform, false);
        var mb = miniBar.GetComponent<RectTransform>();
        mb.anchorMin = new Vector2(0f, 1f);
        mb.anchorMax = new Vector2(0f, 1f);
        mb.pivot = new Vector2(0f, 1f);
        mb.anchoredPosition = miniAnchor;
        ComputeCardSize();
        mb.sizeDelta = new Vector2(0f, miniSlotH + 8f);

        miniCardParent = mb;

        miniEmptyHint = new GameObject("MiniEmpty", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        var me = miniEmptyHint.rectTransform;
        me.SetParent(mb, false);
        // 提示文本跟随 miniAnchor：锚定 miniBar 左上角（miniBar 本身位于 miniAnchor 偏移处），
        // 调 miniAnchor 时提示文本与迷你卡条一起移动
        me.anchorMin = me.anchorMax = new Vector2(0f, 1f);
        me.pivot = new Vector2(0f, 1f);
        me.anchoredPosition = Vector2.zero;
        me.sizeDelta = new Vector2(200f, 40f);
        miniEmptyHint.alignment = TextAlignmentOptions.Left;
        miniEmptyHint.fontSize = 22;
        miniEmptyHint.color = new Color(1f, 1f, 1f, 0.7f);
        miniEmptyHint.text = TextCatalog.Get("ui.build.empty");
        if (FontRegistry.Instance != null) FontRegistry.Instance.ApplyFontToText(miniEmptyHint, FontSlots.Default);

        miniBar.SetActive(false);
    }

    // ───────────────────────── 模式切换 ─────────────────────────
    /// <summary>点按构筑按钮：循环切换三态（左上展开 → 放大展开 → 左上收起）。</summary>
    public void CycleMode()
    {
        SetMode((mode + 1) % 3);
    }

    /// <summary>当前是否处于「左上角堆叠」模式（迷你卡叠在一起，与 Mini 共用容器）。</summary>
    bool IsStackMode => mode == ModeStack;

    void SetMode(int m)
    {
        int prevMode = mode;
        CaptureCardPoses();
        mode = m;
        ApplyMode();
        PlayTransition();

        // 构筑展开/收起音：仅在真正进入/离开扇形放大面板时响，迷你条内部切换（Mini↔Stack）不响。
        // _soundArmed 跳过 Initialize 的默认 SetMode(ModeMini)，避免开局误响收起音。
        if (_soundArmed)
        {
            if (m == ModeFan && prevMode != ModeFan)
                AudioManager.Instance?.Play(SfxId.BuildExpand);
            else if (prevMode == ModeFan && m != ModeFan)
                AudioManager.Instance?.Play(SfxId.BuildCollapse);
        }
        _soundArmed = true;
    }

    void CaptureCardPoses()
    {
        lastPoses.Clear();
        foreach (var go in cardInstances)
        {
            if (go == null) continue;
            var rt = go.GetComponent<RectTransform>();
            var face = ResolveCardFace(go);
            lastPoses.Add(new CardPose
            {
                worldPos = rt != null ? rt.position : Vector3.zero,
                localScale = face != null ? face.localScale : Vector3.one
            });
        }
    }

    RectTransform ResolveCardFace(GameObject instance)
    {
        if (instance == null) return null;
        var rt = instance.GetComponent<RectTransform>();
        if (rt == null) return null;
        if (instance.GetComponent<CoreChoiceCard>() != null) return rt;
        if (rt.childCount > 0) return rt.GetChild(0) as RectTransform;
        return rt;
    }

    void PlayTransition()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }
        ResolveTransitionTiming(out float perCardDuration, out float perCardStagger);
        if (perCardDuration <= 0f) return; // 无动画：保持目标布局，不闪跳
        CaptureTargetPoses();
        ApplyStartPoses(); // 同步把卡对齐到源姿态，避免切换当帧先显示目标再跳回起点
        transitionRoutine = StartCoroutine(TransitionRoutine(perCardDuration, perCardStagger));
    }

    /// <summary>当前布局（mode）对应的转换总时长上限（秒）。</summary>
    float GetTransitionTimeCap()
    {
        switch (mode)
        {
            case ModeFan: return fanTransitionTimeCap;
            case ModeStack: return stackTransitionTimeCap;
            default: return miniTransitionTimeCap;
        }
    }

    /// <summary>
    /// 计算每张卡的过渡时长与间隔延迟：
    /// 每张卡的时间片 = 该布局的时间上限 / 卡牌数量；片内按原「过渡时长 : 间隔延迟」比例分配，
    /// 因此 N 张卡串起来的总时长严格不超过上限（不随卡牌数量线性膨胀）。
    /// 上限 &lt;= 0 时沿用原本的单卡时长与间隔（旧行为）。
    /// </summary>
    void ResolveTransitionTiming(out float perCardDuration, out float perCardStagger)
    {
        float cap = GetTransitionTimeCap();
        if (cap <= 0f)
        {
            perCardDuration = transitionDuration;
            perCardStagger = transitionStagger;
            return;
        }

        float perCard = cap / Mathf.Max(1, cardInstances.Count);
        float total = Mathf.Max(0.0001f, transitionDuration + transitionStagger);
        perCardDuration = perCard * (transitionDuration / total);
        perCardStagger = perCard * (transitionStagger / total);
    }

    void CaptureTargetPoses()
    {
        targetPoses.Clear();
        foreach (var go in cardInstances)
        {
            if (go == null) continue;
            var rt = go.GetComponent<RectTransform>();
            var face = ResolveCardFace(go);
            targetPoses.Add(new CardPose
            {
                worldPos = rt != null ? rt.position : Vector3.zero,
                localScale = face != null ? face.localScale : Vector3.one
            });
        }
    }

    void ApplyStartPoses()
    {
        int count = Mathf.Min(cardInstances.Count, lastPoses.Count);
        for (int i = 0; i < count; i++)
        {
            var go = cardInstances[i];
            if (go == null) continue;
            var rt = go.GetComponent<RectTransform>();
            var face = ResolveCardFace(go);
            if (rt != null) rt.position = lastPoses[i].worldPos;
            if (face != null) face.localScale = lastPoses[i].localScale;
        }
    }

    IEnumerator TransitionRoutine(float perCardDuration, float perCardStagger)
    {
        for (int i = 0; i < cardInstances.Count; i++)
        {
            var root = cardInstances[i];
            if (root == null) continue;
            var rootRT = root.GetComponent<RectTransform>();
            var face = ResolveCardFace(root);
            if (rootRT == null) continue;

            Vector3 targetPos = i < targetPoses.Count ? targetPoses[i].worldPos : rootRT.position;
            Vector3 targetScale = i < targetPoses.Count ? targetPoses[i].localScale : (face != null ? face.localScale : Vector3.one);
            Vector3 startPos = rootRT.position; // ApplyStartPoses 已对齐到源
            Vector3 startScale = face != null ? face.localScale : Vector3.one;

            float t = 0f;
            while (t < perCardDuration)
            {
                if (root == null || rootRT == null) yield break;
                t += Time.unscaledDeltaTime;
                // SmoothStep 三次缓动：起手加速、收尾减速（ease-in-out）。
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / perCardDuration));
                rootRT.position = Vector3.Lerp(startPos, targetPos, u);
                if (face != null) face.localScale = Vector3.Lerp(startScale, targetScale, u);
                yield return null;
            }
            if (root == null || rootRT == null) yield break;
            rootRT.position = targetPos;
            if (face != null) face.localScale = targetScale;

            if (i + 1 < cardInstances.Count && perCardStagger > 0f)
            {
                float waited = 0f;
                while (waited < perCardStagger)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }
        transitionRoutine = null;
    }

    void ApplyMode()
    {
        switch (mode)
        {
            case ModeMini: ShowMiniRow(); break;
            case ModeFan: ShowFan(); break;
            default: ShowStack(); break;
        }
    }

    void ShowMiniRow() => ShowMini(false);

    void ShowStack() => ShowMini(true);

    /// <summary>两种左上角模式共用同一套迷你卡渲染：stacked=false 横向排开，true 叠在一起。</summary>
    void ShowMini(bool stacked)
    {
        PopPause();
        if (panelRoot != null) panelRoot.SetActive(false);
        if (miniBar != null) miniBar.SetActive(true);
        RefreshMini(stacked);
    }

    void ShowFan()
    {
        if (miniBar != null) miniBar.SetActive(false);
        // 必须先激活面板再布局：卡片 RectTransform 在 inactive 状态下 rect 不更新
        if (panelRoot != null) panelRoot.SetActive(true);
        PopulateFan();
        PushPause();
    }

    void PushPause()
    {
        if (paused) return;
        paused = true;
        TimeScaleManager.Push(TimeDomain.Pause, 0f);
        PlayerController.SetGameplayInputBlocked(true, "BuildView");
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    void PopPause()
    {
        if (!paused) return;
        paused = false;
        TimeScaleManager.Pop(TimeDomain.Pause);
        PlayerController.SetGameplayInputBlocked(false, "BuildView");
    }

    // ───────────────────────── 迷你条渲染 ─────────────────────────
    // 分离布局尺寸和视觉命中尺寸：布局沿用原始根节点，命中层覆盖完整可见卡面。
    void ComputeCardSize()
    {
        float rootW = 100f, rootH = 100f;
        float visualW = rootW, visualH = rootH;
        miniVisualCenter = Vector2.zero;
        var prefab = ResolvePrefab();
        if (prefab != null)
        {
            var prefabRect = prefab.GetComponent<RectTransform>();
            if (prefabRect != null)
            {
                rootW = prefabRect.sizeDelta.x;
                rootH = prefabRect.sizeDelta.y;
            }

            var sample = Instantiate(prefab, transform);
            var sampleRect = sample.GetComponent<RectTransform>();
            if (sampleRect != null)
            {
                sampleRect.localPosition = Vector3.zero;
                sampleRect.localRotation = Quaternion.identity;
                sampleRect.localScale = Vector3.one;
                Canvas.ForceUpdateCanvases();
                Bounds visualBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(transform, sampleRect);
                if (visualBounds.size.x > 1f && visualBounds.size.y > 1f)
                {
                    visualW = Mathf.Abs(visualBounds.size.x);
                    visualH = Mathf.Abs(visualBounds.size.y);
                    Vector3 worldCenter = transform.TransformPoint(visualBounds.center);
                    miniVisualCenter = sampleRect.InverseTransformPoint(worldCenter);
                }
            }
            sample.SetActive(false);
            Destroy(sample);
        }

        miniSlotW = rootW * miniScale;
        miniSlotH = rootH * miniScale;
        miniCardW = visualW * miniScale;
        miniCardH = visualH * miniScale;
    }

    /// <summary>堆叠模式把 MiniBar 对齐到 stackAnchor（最左卡落在该 Transform 位置）；其余情况恢复 miniAnchor 默认定位。</summary>
    void PositionMiniBar(bool stacked)
    {
        if (miniBar == null) return;
        var mb = miniBar.GetComponent<RectTransform>();
        if (mb == null) return;
        if (stacked && stackAnchor != null)
        {
            mb.position = stackAnchor.position;
            return;
        }
        mb.anchorMin = mb.anchorMax = new Vector2(0f, 1f);
        mb.pivot = new Vector2(0f, 1f);
        mb.anchoredPosition = miniAnchor;
    }

    /// <summary>第 idx 张卡沿用原始根节点的布局中心；slot 尺寸只负责覆盖完整视觉卡面。</summary>
    Vector2 MiniSlotPos(int idx, bool stacked)
    {
        float x = stacked ? miniSlotW * 0.5f : idx * (miniSlotW + miniSpacing) + miniSlotW * 0.5f;
        return new Vector2(x + miniVisualCenter.x * miniScale, miniVisualCenter.y * miniScale);
    }

    // 手动排布迷你卡槽：布局位置沿用原始根尺寸，命中层覆盖实际视觉 bounds。
    void ApplyMiniLayout(bool stacked = false)
    {
        if (miniCardParent == null) return;
        PositionMiniBar(stacked);
        if (miniBar != null)
        {
            var mb = miniBar.GetComponent<RectTransform>();
            if (mb != null) mb.sizeDelta = new Vector2(mb.sizeDelta.x, miniSlotH + 8f);
        }
        int idx = 0;
        foreach (Transform child in miniCardParent)
        {
            if (miniEmptyHint != null && child == miniEmptyHint.transform) continue;
            var rt = child as RectTransform;
            if (rt == null) continue;
            rt.sizeDelta = new Vector2(miniCardW, miniCardH);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = MiniSlotPos(idx, stacked);
            idx++;
        }
    }

    // 运行时在 Inspector 改动参数（如 miniSpacing / miniScale）即时重建布局，方便调试
    void OnValidate()
    {
        if (!Application.isPlaying) return;
        ComputeCardSize();
        ApplyMiniLayout(IsStackMode);
    }

    void RefreshMini(bool stacked = false)
    {
        if (miniEmptyHint != null) miniEmptyHint.gameObject.SetActive(false);
        ClearCards();
        ComputeCardSize();
        ApplyMiniLayout(stacked);
        var result = Gather();
        bool has = result.cards.Count > 0;
        if (miniEmptyHint != null) miniEmptyHint.gameObject.SetActive(!has && showEmptyHint);
        if (!has) return;

        var prefab = ResolvePrefab();
        if (prefab == null)
        {
            if (miniEmptyHint != null) { miniEmptyHint.text = TextCatalog.Get("ui.build.card_unavailable"); miniEmptyHint.gameObject.SetActive(true); }
            return;
        }

        for (int i = 0; i < result.cards.Count; i++)
        {
            var slot = new GameObject("MiniSlot", typeof(RectTransform));
            slot.transform.SetParent(miniCardParent, false);
            var srt = slot.GetComponent<RectTransform>();
            srt.sizeDelta = new Vector2(miniCardW, miniCardH);
            srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = MiniSlotPos(i, stacked);

            var data = result.cards[i];
            var go = Instantiate(prefab, slot.transform);
            var crt = go.GetComponent<RectTransform>();
            if (crt != null)
            {
                crt.anchorMin = new Vector2(0.5f, 0.5f);
                crt.anchorMax = new Vector2(0.5f, 0.5f);
                crt.pivot = new Vector2(0.5f, 0.5f);
                crt.anchoredPosition = -miniVisualCenter * miniScale;
                crt.localScale = Vector3.one * miniScale;
            }
            if (FontRegistry.Instance != null) FontRegistry.Instance.ApplyFontToTree(go.transform, FontSlots.Card);
            var card = go.GetComponent<CoreChoiceCard>();
            if (card == null) card = go.AddComponent<CoreChoiceCard>();
            card.Init(i, data.ResolveCardName(), data.image, data.ResolveDescription() ?? "", null, null, data);
            AttachCardTooltip(slot, data);
            if (card.confirmButton != null) card.confirmButton.gameObject.SetActive(false);
            if (card.rerollButton != null) card.rerollButton.gameObject.SetActive(false);
            if (card.cardText != null) card.cardText.gameObject.SetActive(false);
            if (card.descriptionText != null) card.descriptionText.gameObject.SetActive(false);
            var choiceCard = go.GetComponent<ChoiceCard>();
            if (choiceCard != null) Destroy(choiceCard);
            HideCardChildren(go, stacked);
            // 迷你图标纯展示，关闭卡面内部所有 Graphic 的射线，只保留 MiniSlot 作为完整命中区域。
            var graphics = go.GetComponentsInChildren<Graphic>(true);
            foreach (var graphic in graphics) graphic.raycastTarget = false;
            Image hitArea = slot.GetComponent<Image>();
            if (hitArea != null) hitArea.raycastTarget = true;

            cardInstances.Add(slot);
        }
        if (miniEmptyHint != null) miniEmptyHint.gameObject.SetActive(false);
        SetMiniBarLayer(stacked);
    }

    void SetMiniBarLayer(bool stacked)
    {
        if (miniBar == null) return;
        if (!stacked)
        {
            miniBar.transform.SetAsLastSibling();
            return;
        }

        if (buildButton != null && miniBar.transform.parent == buildButton.transform.parent)
        {
            int targetIndex = buildButton.transform.GetSiblingIndex();
            int currentIndex = miniBar.transform.GetSiblingIndex();
            // SetSiblingIndex 是「先移除再插入」：miniBar 被移除后，buildButton 的索引会前移一位。
            // 因此 miniBar 原本排在 buildButton 之前时要减 1，否则每次调用都会把两者顺序翻转一次
            // ——收起态获得新卡会再次调用本方法，翻到错误一侧就成了「新卡盖住卡背」。
            int insertIndex = currentIndex < targetIndex ? targetIndex - 1 : targetIndex;
            if (insertIndex >= 0) miniBar.transform.SetSiblingIndex(insertIndex);
            return;
        }

        miniBar.transform.SetAsFirstSibling();
    }

    /// <summary>
    /// 左上角两种模式（迷你一排 / 堆叠）的卡面裁剪：按名隐藏卡面预制体里指定的子物体。
    /// 这些图层（如 Image (1)）在原始卡面尺寸下是卡外光效，缩小后会糊成一团碍眼的色块。
    /// 仅在实例化后立即调用；扇形放大模式保持完整卡面，不做裁剪。
    /// </summary>
    void HideCardChildren(GameObject go, bool stacked = false)
    {
        // miniHiddenChildren 沿用历史语义：只隐藏第一个同名物体。
        // 卡面里存在多个 "Image (1)"，若一并隐藏，左上展开会少显示一个图层。
        ApplyHiddenChildren(go, miniHiddenChildren, onlyFirstMatch: true);
        // 收起态卡片完全重叠，额外裁掉立绘与装饰图层（同名全部隐藏，确保裁干净）。
        if (stacked) ApplyHiddenChildren(go, stackHiddenChildren, onlyFirstMatch: false);
    }

    /// <summary>
    /// 按全名隐藏卡面子孙。
    /// onlyFirstMatch=true 只隐藏第一个同名（历史行为，用于两种迷你态共用的裁剪）；
    /// false 则隐藏所有同名（收起态需要把重复图层全部裁掉）。
    /// </summary>
    static void ApplyHiddenChildren(GameObject go, List<string> names, bool onlyFirstMatch)
    {
        if (go == null || names == null || names.Count == 0) return;
        for (int n = 0; n < names.Count; n++)
        {
            var name = names[n];
            if (string.IsNullOrEmpty(name)) continue;

            if (onlyFirstMatch)
            {
                var first = FindDescendant(go.transform, name);
                if (first != null && first != go.transform) first.gameObject.SetActive(false);
                continue;
            }

            var all = go.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t == go.transform) continue;   // 不隐藏卡面根，避免整张卡消失
                if (t.name == name) t.gameObject.SetActive(false);
            }
        }
    }

    // ───────────────────────── 扇形模式渲染 ─────────────────────────
    void PopulateFan()
    {
        ClearCards();
        var result = Gather();
        titleText.text = result.title;
        emptyHint.text = result.hint;

        bool has = result.cards.Count > 0;
        emptyHint.gameObject.SetActive(!has && showEmptyHint);
        cardParent.gameObject.SetActive(has);
        if (!has) return;

        var prefab = ResolvePrefab();
        if (prefab == null)
        {
            Debug.LogError("[BuildView] cardPrefab 未配置且 CoreChoiceUI 不存在，无法渲染卡片。");
            emptyHint.text = TextCatalog.Get("ui.build.card_unavailable");
            emptyHint.gameObject.SetActive(true);
            cardParent.gameObject.SetActive(false);
            return;
        }

        for (int i = 0; i < result.cards.Count; i++)
        {
            var data = result.cards[i];
            var go = Instantiate(prefab, cardParent);
            var cardRect = go.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                cardRect.anchorMin = new Vector2(0.5f, 0.5f);
                cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                cardRect.pivot = new Vector2(0.5f, 0.5f);
                cardRect.anchoredPosition = Vector2.zero;
            }
            if (FontRegistry.Instance != null) FontRegistry.Instance.ApplyFontToTree(go.transform, FontSlots.Card);
            var card = go.GetComponent<CoreChoiceCard>();
            if (card == null) card = go.AddComponent<CoreChoiceCard>();
            card.Init(i, data.ResolveCardName(), data.image, data.ResolveDescription() ?? "", null, null, data);
            AttachCardTooltip(go, data);
            if (card.confirmButton != null) card.confirmButton.gameObject.SetActive(false);
            if (card.rerollButton != null) card.rerollButton.gameObject.SetActive(false);
            if (card.cardText != null) card.cardText.gameObject.SetActive(false);
            if (card.descriptionText != null) card.descriptionText.gameObject.SetActive(false);
            var choiceCard = go.GetComponent<ChoiceCard>();
            if (choiceCard != null) Destroy(choiceCard);
            AddHoverToFront(go);
            cardInstances.Add(go);
        }
        layout.Rebuild(cardInstances);
    }

    void AttachCardTooltip(GameObject go, CardData data)
    {
        if (go == null) return;
        GameplayTooltipTarget target = go.GetComponent<GameplayTooltipTarget>();
        if (target == null) target = go.AddComponent<GameplayTooltipTarget>();
        target.SetTooltip(FindObjectOfType<PossessionImprintTooltip>(true));
        target.BindCard(data);
    }

    /// <summary>鼠标悬停某卡时把它置顶并轻微放大，避免被堆叠的其它卡遮挡。</summary>
    void AddHoverToFront(GameObject go)
    {
        var ct = go.AddComponent<EventTrigger>();
        float baseScale = 0f;
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener((_) =>
        {
            go.transform.SetAsLastSibling();
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                if (baseScale <= 0f) baseScale = rt.localScale.x;
                rt.localScale = new Vector3(baseScale * 1.12f, baseScale * 1.12f, 1f);
            }
        });
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener((_) =>
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt != null && baseScale > 0f)
                rt.localScale = new Vector3(baseScale, baseScale, 1f);
        });
        ct.triggers.Add(enter);
        ct.triggers.Add(exit);
    }

    void ClearCards()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }
        foreach (var go in cardInstances) Destroy(go);
        cardInstances.Clear();
    }

    GameObject ResolvePrefab()
    {
        if (cardPrefab != null) return cardPrefab;
        if (CoreChoiceUI.Instance != null) return CoreChoiceUI.Instance.cardPrefab;
        return null;
    }

    // ───────────────────────── 数据收集 ─────────────────────────
    struct GatherResult { public List<CardData> cards; public string title; public string hint; }

    GatherResult Gather()
    {
        var list = new List<CardData>();
        string title = "当前构筑";
        string hint = TextCatalog.Get("ui.build.empty_all");

        var body = (PossessionManager.Instance != null) ? PossessionManager.Instance.CurrentBody : null;
        if (body == null)
        {
            // 灵魂态（未附身）：是否显示构筑由调试开关控制
            if (!showInSoulState)
                return new GatherResult { cards = list, title = "", hint = "" };
            title = "当前构筑";
            hint = TextCatalog.Get("ui.build.empty_all");
            CollectUnlocked(list);
        }
        else if (body.IsElite)
        {
            title = "精英构筑";
            hint = "该精英暂无构筑";
            var carrier = EliteBuildCarrier.Get(body);
            if (carrier != null)
            {
                foreach (var id in carrier.CardIds)
                {
                    var c = CardManager.Instance != null ? CardManager.Instance.FindCard(id) : null;
                    if (c != null && !list.Contains(c)) list.Add(c);
                }
            }
        }
        else
        {
            title = (body.sinType != SinType.None) ? $"针对卡组 · {body.sinType}" : "针对卡组";
            hint = "该类型暂无针对卡";
            var abilities = body.GetComponentsInChildren<EnemyAbility>(true);
            CollectUnlockedFiltered(list, abilities);
        }
        return new GatherResult { cards = list, title = title, hint = hint };
    }

    void CollectUnlocked(List<CardData> outList)
    {
        if (RunSession.Instance == null || CardManager.Instance == null) return;
        var seen = new HashSet<string>();
        foreach (var id in RunSession.Instance.UnlockedEffects)
        {
            if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
            var c = CardManager.Instance.FindCard(id);
            if (c != null) outList.Add(c);
        }
    }

    void CollectUnlockedFiltered(List<CardData> outList, EnemyAbility[] abilities)
    {
        if (RunSession.Instance == null || CardManager.Instance == null || abilities == null) return;
        var seen = new HashSet<string>();
        foreach (var id in RunSession.Instance.UnlockedEffects)
        {
            if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
            var c = CardManager.Instance.FindCard(id);
            if (c == null) continue;
            foreach (var a in abilities)
            {
                if (CardManager.DoesCardTargetAbility(c, a)) { outList.Add(c); break; }
            }
        }
    }

    // ───────────────────────── 兼容旧调用（若有） ─────────────────────────
    public void Show() => SetMode(ModeFan);
    public void Hide() => SetMode(ModeStack);
}
