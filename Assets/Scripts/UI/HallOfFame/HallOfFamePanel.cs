using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 荣誉殿堂面板（Canonical Meta_Progression §5：纯展示系统，只做构筑回顾与长线成就感，不提供任何战斗加成）。
///
/// 数据与展示（§5.4/§5.7）：
///   - 原始 Run 表现：本地 HallOfFameStore（对局内滚动更新、终态冻结）；
///   - 异步传播战绩：先显示本地缓存，进入时后台联网刷新（GET /api/elite/stats），支持手动刷新；
///   - 两类数据分开展示，不混用同一字段名（§5.4）。
///
/// 排序（§5.6）：保存时间（默认倒序）/ 本局击杀 / 致 Run Fail 数 / BD 卡牌数量，头部按钮循环切换。
///
/// 装配：主菜单经 MainMenuController 克隆设置按钮注入入口（零场景编辑）；
/// 面板本体为纯代码 UGUI（自建 Overlay Canvas，模式扩展自 EliteNetworkStatusUI），常驻跨场景。
/// 调试：F6 直接开关（仅非正式流程；CardProgressPanel 同款门禁），便于直接 Play 查看数据效果。
/// </summary>
public class HallOfFamePanel : MonoBehaviour
{
    public static HallOfFamePanel Instance { get; private set; }

    [Header("服务器（默认值；对局中自动取 EliteBuildDirector 配置）")]
    public string serverUrl = "http://127.0.0.1:8080";
    public int timeoutSeconds = 5;

    enum SortKey { SavedTime, Kills, RunFail, BdCount }
    static readonly string[] SortLabels = { "排序：保存时间", "排序：本局击杀", "排序：Run Fail", "排序：BD 卡数" };
    SortKey sortKey = SortKey.SavedTime; // §5.6 默认按保存时间倒序

    GameObject panelRoot;
    Button refreshButton;
    Button sortButton;
    TMP_Text statusLabel;
    TMP_Text emptyLabel;
    Transform contentRoot;
    ScrollRect scrollRect;

    bool refreshing;
    bool built;

    // ── 生命周期 ──

    public static HallOfFamePanel EnsureInstance()
    {
        if (Instance != null) return Instance;
        var existing = FindObjectOfType<HallOfFamePanel>();
        if (existing != null) return existing; // Awake 已注册 Instance
        var go = new GameObject("[HallOfFamePanel]");
        DontDestroyOnLoad(go);
        return go.AddComponent<HallOfFamePanel>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BuildUI();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (panelRoot == null || !panelRoot.activeSelf) return;
        if (Input.GetKeyDown(KeyCode.Escape)) Hide();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // 调试入口：非正式流程下 F6 直接开关（正式流程走主菜单按钮；CardProgressPanel 同款门禁）。
    // 注意开关统一在此处理，Update 只管 ESC，避免同帧双重响应。
    void LateUpdate()
    {
        if (GameManager.IsFormalFlow) return;
        if (Input.GetKeyDown(KeyCode.F6))
        {
            if (IsVisible()) Hide(); else Show();
        }
    }
#endif

    public bool IsVisible() => panelRoot != null && panelRoot.activeSelf;

    public void Show()
    {
        EnsureBuilt();
        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
        RenderLocal();          // §5.7：先显示本地缓存
        _ = RefreshFromServer(); // 再后台联网刷新（不强制；失败静默保持缓存）
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ── 数据渲染 ──

    void RenderLocal()
    {
        var entries = HallOfFameStore.EntriesBySavedTimeDesc();
        entries = ApplySort(entries);

        if (emptyLabel != null)
            emptyLabel.gameObject.SetActive(entries.Count == 0);

        // 倒序销毁旧条目（CoreChoiceUI.RefreshCards 同模式）
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        foreach (var e in entries)
        {
            var text = MakeText(contentRoot, FormatEntry(e), 22, new Color(0.92f, 0.93f, 0.96f));
            var le = text.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 96f; // 多行条目的滚动列表最小高度
        }

        if (statusLabel != null && !refreshing)
            statusLabel.text = $"共 {entries.Count} 条记录";
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    List<HallOfFameEntry> ApplySort(List<HallOfFameEntry> list)
    {
        switch (sortKey)
        {
            case SortKey.Kills: list.Sort((a, b) => b.kills.CompareTo(a.kills)); break;
            case SortKey.RunFail: list.Sort((a, b) => b.runFail.CompareTo(a.runFail)); break;
            case SortKey.BdCount: list.Sort((a, b) => b.bdCount.CompareTo(a.bdCount)); break;
            default: break; // SavedTime：EntriesBySavedTimeDesc 已排序
        }
        return list;
    }

    async Task RefreshFromServer()
    {
        if (refreshing) return;
        refreshing = true;
        SetStatus("战绩刷新中…");
        refreshButton.interactable = false;
        try
        {
            // 对局中同步 EliteBuildDirector 的服务器配置（主菜单直开时用面板默认值）
            var director = EliteBuildDirector.Instance;
            var client = new EliteNetClient(
                director != null ? director.serverUrl : serverUrl,
                director != null ? director.timeoutSeconds : timeoutSeconds,
                director != null && director.logRawResponses);
            var resp = await client.FetchStats(DeviceIdentity.Id);
            int applied = resp != null && resp.stats != null
                ? HallOfFameStore.ApplyStats(resp.stats) : 0;
            SetStatus($"已刷新 {NowClock()}（更新 {applied} 条战绩）");
        }
        catch (Exception e)
        {
            // §5.7/§5.10：断网仍可查看本地荣誉记录——刷新失败静默保持缓存
            SetStatus("刷新失败——显示本地缓存（离线可查看）");
            Debug.Log($"[HallOfFame] 战绩刷新失败（保持本地缓存）：{e.Message}");
        }
        finally
        {
            refreshing = false;
            if (refreshButton != null) refreshButton.interactable = true;
            RenderLocal();
        }
    }

    void SetStatus(string text)
    {
        if (statusLabel != null) statusLabel.text = text;
    }

    void CycleSort()
    {
        sortKey = (SortKey)(((int)sortKey + 1) % SortLabels.Length);
        if (sortButton != null) sortButton.GetComponentInChildren<TMP_Text>().text = SortLabels[(int)sortKey];
        RenderLocal();
    }

    // ── 条目格式化（§5.4 两段式：原始表现与异步战绩分开展示）──

    string FormatEntry(HallOfFameEntry e)
    {
        string sinName = SinDisplay(e.sin);
        string phase = PhaseText(e);
        string cards = e.cardIds != null && e.cardIds.Count > 0 ? string.Join("、", e.cardIds) : "（无清单）";
        string staleMark = HasStaleCards(e) ? "（部分卡牌已失效 / 历史版本）" : ""; // §5.9
        string statsTime = e.statsUpdatedAtUnix > 0
            ? $"（{FormatClock(e.statsUpdatedAtUnix)} 更新）" : "（未同步）";

        return $"<b>◆ {sinName}（{e.sin}）  {FormatClock(e.savedAtUnix)}  {phase}</b>\n" +
               $"<color=#9fd4ff>── 原始 Run 表现 ──</color>\n" +
               $"BD 深度 {e.bdCount}｜控制 {e.controlSeconds:F0} 秒｜本局击杀 {e.kills}\n" +
               $"{cards}{staleMark}\n" +
               $"<color=#ffd79f>── 异步战绩{statsTime}──</color>\n" +
               $"被投放 {e.deployed}｜被击杀 {e.fatal}｜被附身 {e.possessed}｜致 Body Fatal {e.bodyFatal}｜致 Run Fail {e.runFail}";
    }

    static string SinDisplay(string wire)
    {
        if (string.IsNullOrEmpty(wire)) return wire;
        if (!Enum.TryParse(wire, true, out SinType sin) || sin == SinType.None) return wire;
        var catalog = Resources.Load<EliteMonsterCatalog>("EliteMonsterCatalog");
        var entry = catalog != null ? catalog.Find(sin) : null;
        return entry != null && !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : wire;
    }

    static string PhaseText(HallOfFameEntry e)
    {
        if (!string.IsNullOrEmpty(e.endPhase))
        {
            string result = e.endPhase == "Result" ? "胜利"
                : e.endPhase == "Failed" ? "失败"
                : e.endPhase == "Aborted" ? "中途退出"
                : e.endPhase == "NewRunInterrupt" ? "开新局打断" : e.endPhase;
            return e.reachedWave > 0 ? $"{result} · 到达第 {e.reachedWave} 波" : result;
        }
        if (e.stage == "final") return "记录于 Final（对局中）";
        return e.reachedWave > 0 ? $"记录于第 {e.reachedWave} 波（对局中）" : "对局中";
    }

    /// <summary>历史卡牌失效标记（§5.9）：清单中存在当前牌池不认识的 ID。CardManager 不在（主菜单）时跳过校验。</summary>
    static bool HasStaleCards(HallOfFameEntry e)
    {
        var cm = CardManager.Instance;
        if (cm == null || e.cardIds == null) return false;
        foreach (var id in e.cardIds)
            if (cm.FindCard(id) == null) return true;
        return false;
    }

    static string FormatClock(long unix)
    {
        if (unix <= 0) return "--";
        return DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().ToString("MM-dd HH:mm");
    }

    static string NowClock() =>
        DateTimeOffset.Now.ToString("HH:mm");

    // ── UI 构建（纯代码，扩展自 EliteNetworkStatusUI 模式）──

    void EnsureBuilt()
    {
        if (built) return;
        BuildUI();
    }

    void BuildUI()
    {
        built = true;

        // 自建 Overlay Canvas（不依赖场景 Canvas，主菜单/对局场景均可挂）
        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
        }

        // 全屏压暗背景
        panelRoot = new GameObject("PanelRoot", typeof(RectTransform), typeof(Image));
        panelRoot.transform.SetParent(transform, false);
        Stretch(panelRoot.GetComponent<RectTransform>());
        panelRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
        panelRoot.SetActive(false);

        // 面板底板
        var panelBg = new GameObject("PanelBg", typeof(RectTransform), typeof(Image));
        panelBg.transform.SetParent(panelRoot.transform, false);
        var bgRect = panelBg.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(1080, 700);
        bgRect.anchoredPosition = Vector2.zero;
        panelBg.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.12f, 0.97f);

        // 标题
        var title = MakeText(panelBg.transform, "荣誉殿堂", 34, new Color(0.95f, 0.85f, 0.55f));
        Place(title.rectTransform, new Vector2(0f, -34f), new Vector2(600f, 44f), TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;

        // 关闭按钮（右上）
        var close = MakeButton(panelBg.transform, "✕", 46f, 46f);
        Place(close.GetComponent<RectTransform>(), new Vector2(500f, -34f), new Vector2(46f, 46f));
        close.onClick.AddListener(Hide);

        // 工具行：刷新 / 排序 / 状态
        refreshButton = MakeButton(panelBg.transform, "刷新战绩", 150f, 42f);
        Place(refreshButton.GetComponent<RectTransform>(), new Vector2(-440f, -86f), new Vector2(150f, 42f));
        refreshButton.onClick.AddListener(() => _ = RefreshFromServer());

        sortButton = MakeButton(panelBg.transform, SortLabels[(int)sortKey], 210f, 42f);
        Place(sortButton.GetComponent<RectTransform>(), new Vector2(-255f, -86f), new Vector2(210f, 42f));
        sortButton.onClick.AddListener(CycleSort);

        statusLabel = MakeText(panelBg.transform, "", 20, new Color(0.75f, 0.78f, 0.85f));
        Place(statusLabel.rectTransform, new Vector2(60f, -86f), new Vector2(620f, 42f), TextAlignmentOptions.Right);

        // 滚动列表区（项目内首个 UGUI ScrollRect，纯代码构建）
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollGo.transform.SetParent(panelBg.transform, false);
        var scrollRect2 = scrollGo.GetComponent<RectTransform>();
        scrollRect2.anchorMin = new Vector2(0f, 0f);
        scrollRect2.anchorMax = new Vector2(1f, 1f);
        scrollRect2.offsetMin = new Vector2(18f, 18f);
        scrollRect2.offsetMax = new Vector2(-18f, -118f);
        scrollGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.04f);
        scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 30f;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGo.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        scrollRect.viewport = viewport.GetComponent<RectTransform>();

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);
        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRect;
        contentRoot = content.transform;

        // 空状态（§5.9）
        emptyLabel = MakeText(panelBg.transform,
            "暂无荣誉记录\n\n完成一局携带卡牌的战斗后，你的构筑将载入殿堂；\n被其他玩家遇到的精英也会在此积累战绩。", 24,
            new Color(0.7f, 0.72f, 0.8f));
        Place(emptyLabel.rectTransform, Vector2.zero, new Vector2(700f, 200f), TextAlignmentOptions.Center);
        emptyLabel.gameObject.SetActive(false);
    }

    // ── UI 小工具 ──

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size, TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static TMP_Text MakeText(Transform parent, string text, float size, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.raycastTarget = false;
        try { tmp.font = UiFontAssets.ChineseOrDefault; } catch { /* 字体资产异常时用 TMP 默认 */ }
        return tmp;
    }

    static Button MakeButton(Transform parent, string label, float w, float h)
    {
        var go = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = new Color(0.20f, 0.24f, 0.34f, 0.95f);
        var button = go.GetComponent<Button>();
        var text = MakeText(go.transform, label, 22, Color.white);
        Stretch(text.rectTransform);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return button;
    }
}
