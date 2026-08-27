using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 显示构筑信息组件（Build View）。
/// 这是一个自包含组件：把它挂到任意 UI GameObject（如 UICanvas）上，该物体即拥有“显示构筑”的能力；
/// 所有显示配置（扇形参数、左上角迷你卡条参数、卡牌预制体等）直接在本组件的 Inspector 上编辑。
///
/// 交互：点按“构筑”按钮循环三态：
///   0 左上角一排（迷你图标横向排开，常驻 HUD，不暂停）
///   1 半屏扇形放大（CardArcLayout 弧形排布，暂停查看）
///   2 左上角堆叠（沿用迷你卡的尺寸与位置，全部卡片叠在一起，只露出堆叠边缘）
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
    float miniCardW = 80f;         // 由 cardPrefab 根尺寸 × miniScale 自动推导，无需手填
    float miniCardH = 80f;         // 由 cardPrefab 根尺寸 × miniScale 自动推导，无需手填
    public float miniSpacing = 60f;       // ★ 迷你卡之间的间隔（像素）。想让模式0卡片排得更松/更紧，调这个值
    public Vector2 miniAnchor = new Vector2(76f, -32f); // 迷你卡条距屏幕左上角的偏移（X 右移，Y 上移）；默认让卡条落在构筑按钮右侧并与其垂直居中

    [Header("堆叠模式（模式 2：左上角缩小态基础上把所有卡叠在一起）")]
    [Tooltip("堆叠模式下每张卡相对前一张的像素偏移。默认只做水平错位、上下对齐（y=0），让叠层横向铺开；y 非 0 会形成斜向堆叠。0,0 = 完全重合只看到最上面一张。")]
    public Vector2 stackOffset = new Vector2(12f, 0f);
    [Tooltip("左上角两种模式（迷你一排 / 堆叠）要隐藏的卡面子物体名：卡面预制体里溢出卡框的装饰图层（如 Image (1)）缩小后会变成碍眼的色块，这里按需裁掉。留空则不裁剪；扇形放大模式不受影响。")]
    public List<string> miniHiddenChildren = new List<string> { "Image (1)" };

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
    int mode = 0;                         // 0=迷你一排 1=扇形放大 2=收回
    bool paused = false;
    bool initialized = false;

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
        emptyHint.text = "尚未获得任何卡片";
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
        cl.text = "返回"; cl.alignment = TextAlignmentOptions.Center; cl.fontSize = 26; cl.color = Color.white;
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
        mb.sizeDelta = new Vector2(0f, miniCardH + 8f);

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
        miniEmptyHint.text = "尚未获得卡片";
        if (FontRegistry.Instance != null) FontRegistry.Instance.ApplyFontToText(miniEmptyHint, FontSlots.Default);

        miniBar.SetActive(false);
    }

    // ───────────────────────── 模式切换 ─────────────────────────
    /// <summary>点按构筑按钮：循环切换三态（迷你一排 → 扇形放大 → 左上角堆叠）。</summary>
    public void CycleMode()
    {
        SetMode((mode + 1) % 3);
    }

    /// <summary>当前是否处于「左上角堆叠」模式（迷你卡叠在一起，与 Mini 共用容器）。</summary>
    bool IsStackMode => mode == ModeStack;

    void SetMode(int m)
    {
        mode = m;
        ApplyMode();
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
    // 根据 cardPrefab 根尺寸 × miniScale 推导每张迷你卡的实际像素尺寸
    void ComputeCardSize()
    {
        float bw = 100f, bh = 100f;
        var prefab = ResolvePrefab();
        if (prefab != null)
        {
            var prt = prefab.GetComponent<RectTransform>();
            if (prt != null) { bw = prt.sizeDelta.x; bh = prt.sizeDelta.y; }
        }
        miniCardW = bw * miniScale;
        miniCardH = bh * miniScale;
    }

    /// <summary>第 idx 张卡在两种左上角模式下的锚点偏移：横排模式按卡宽+间距递进，堆叠模式按 stackOffset 递进。</summary>
    Vector2 MiniSlotPos(int idx, bool stacked)
    {
        return stacked
            ? new Vector2(idx * stackOffset.x, idx * stackOffset.y)
            : new Vector2(idx * (miniCardW + miniSpacing), 0f);
    }

    // 手动排布迷你卡槽：左对齐、卡间仅保留 miniSpacing 间隙，不依赖布局组重建时序
    void ApplyMiniLayout(bool stacked = false)
    {
        if (miniCardParent == null) return;
        if (miniBar != null)
        {
            var mb = miniBar.GetComponent<RectTransform>();
            if (mb != null) mb.sizeDelta = new Vector2(mb.sizeDelta.x, miniCardH + 8f);
        }
        int idx = 0;
        foreach (Transform child in miniCardParent)
        {
            if (miniEmptyHint != null && child == miniEmptyHint.transform) continue;
            var rt = child as RectTransform;
            if (rt == null) continue;
            rt.sizeDelta = new Vector2(miniCardW, miniCardH);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
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
            if (miniEmptyHint != null) { miniEmptyHint.text = "卡片预制体缺失"; miniEmptyHint.gameObject.SetActive(true); }
            return;
        }

        for (int i = 0; i < result.cards.Count; i++)
        {
            var slot = new GameObject("MiniSlot", typeof(RectTransform));
            slot.transform.SetParent(miniCardParent, false);
            var srt = slot.GetComponent<RectTransform>();
            srt.sizeDelta = new Vector2(miniCardW, miniCardH);
            srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.anchoredPosition = MiniSlotPos(i, stacked);

            var data = result.cards[i];
            var go = Instantiate(prefab, slot.transform);
            var crt = go.GetComponent<RectTransform>();
            if (crt != null)
            {
                crt.anchorMin = new Vector2(0.5f, 0.5f);
                crt.anchorMax = new Vector2(0.5f, 0.5f);
                crt.pivot = new Vector2(0.5f, 0.5f);
                crt.anchoredPosition = Vector2.zero;
                crt.localScale = Vector3.one * miniScale;
            }
            if (FontRegistry.Instance != null) FontRegistry.Instance.ApplyFontToTree(go.transform, FontSlots.Card);
            var card = go.GetComponent<CoreChoiceCard>();
            if (card == null) card = go.AddComponent<CoreChoiceCard>();
            card.Init(i, data.ResolveCardName(), data.image, data.ResolveDescription() ?? "", null, null, data);
            if (card.confirmButton != null) card.confirmButton.gameObject.SetActive(false);
            if (card.rerollButton != null) card.rerollButton.gameObject.SetActive(false);
            if (card.cardText != null) card.cardText.gameObject.SetActive(false);
            if (card.descriptionText != null) card.descriptionText.gameObject.SetActive(false);
            var choiceCard = go.GetComponent<ChoiceCard>();
            if (choiceCard != null) Destroy(choiceCard);
            HideCardChildren(go);
            // 迷你图标纯展示，关闭射线拦截避免遮挡世界点击
            var imgs = go.GetComponentsInChildren<Image>(true);
            foreach (var img in imgs) img.raycastTarget = false;

            cardInstances.Add(slot);
        }
    }

    /// <summary>
    /// 左上角两种模式（迷你一排 / 堆叠）的卡面裁剪：按名隐藏卡面预制体里指定的子物体。
    /// 这些图层（如 Image (1)）在原始卡面尺寸下是卡外光效，缩小后会糊成一团碍眼的色块。
    /// 仅在实例化后立即调用；扇形放大模式保持完整卡面，不做裁剪。
    /// </summary>
    void HideCardChildren(GameObject go)
    {
        if (miniHiddenChildren == null || miniHiddenChildren.Count == 0) return;
        for (int i = 0; i < miniHiddenChildren.Count; i++)
        {
            var name = miniHiddenChildren[i];
            if (string.IsNullOrEmpty(name)) continue;
            var t = FindDescendant(go.transform, name);
            if (t != null) t.gameObject.SetActive(false);
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
            emptyHint.text = "卡片预制体缺失";
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
        string hint = "尚未获得任何卡片";

        var body = (PossessionManager.Instance != null) ? PossessionManager.Instance.CurrentBody : null;
        if (body == null)
        {
            // 灵魂态（未附身）：是否显示构筑由调试开关控制
            if (!showInSoulState)
                return new GatherResult { cards = list, title = "", hint = "" };
            title = "当前构筑";
            hint = "尚未获得任何卡片";
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
