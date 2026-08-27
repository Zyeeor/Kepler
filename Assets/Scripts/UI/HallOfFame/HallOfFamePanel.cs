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
    // 统一文本目录：排序标签（TextCatalog，运行时取文本；策划改文案不动代码）
    static readonly string[] SortLabelKeys = { "ui.hof.sort.saved_time", "ui.hof.sort.kills", "ui.hof.sort.run_fail", "ui.hof.sort.bd_count" };
    static string SortLabel(int i) => TextCatalog.Get(SortLabelKeys[i]);
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
        // 老档 / 词表改版补生成称号（方案 §7.2）：无词缀的条目用当前词表生成一次并写入
        HallOfFameStore.BackfillMissingEpithets();

        var entries = HallOfFameStore.EntriesBySavedTimeDesc();
        entries = ApplySort(entries);
        generationIndex = BuildGenerationIndex(entries);

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
            statusLabel.text = TextCatalog.Get("ui.hof.status.count", entries.Count);
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
        SetStatus(TextCatalog.Get("ui.hof.status.refreshing"));
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
            SetStatus(TextCatalog.Get("ui.hof.status.refreshed", NowClock(), applied));
        }
        catch (Exception e)
        {
            // §5.7/§5.10：断网仍可查看本地荣誉记录——刷新失败静默保持缓存
            SetStatus(TextCatalog.Get("ui.hof.status.offline"));
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
        sortKey = (SortKey)(((int)sortKey + 1) % SortLabelKeys.Length);
        if (sortButton != null) sortButton.GetComponentInChildren<TMP_Text>().text = SortLabel((int)sortKey);
        RenderLocal();
    }

    // ── 条目格式化（§5.4 两段式：原始表现与异步战绩分开展示）──

    string FormatEntry(HallOfFameEntry e)
    {
        // 统一文本目录：条目模板（ui.hof.entry.*，{0} 占位符）；颜色标记保留在代码（富文本标记非文案）
        // 标题改为**词缀名**（方案 Phase 2）：同名构筑靠词缀区分，Sin 种类名降级为副行。
        string epithetName = EpithetName(e);
        string sinName = SinDisplay(e.sin);
        string speciesLine = TextCatalog.Get("ui.hof.entry.species", sinName);
        string phase = PhaseText(e);
        string cards = e.cardIds != null && e.cardIds.Count > 0 ? string.Join("、", e.cardIds) : TextCatalog.Get("ui.hof.entry.no_cards");
        string staleMark = HasStaleCards(e) ? TextCatalog.Get("ui.hof.entry.stale_cards") : ""; // §5.9
        string statsTime = e.statsUpdatedAtUnix > 0
            ? TextCatalog.Get("ui.hof.entry.synced_at", FormatClock(e.statsUpdatedAtUnix)) : TextCatalog.Get("ui.hof.entry.not_synced");

        return TextCatalog.Get("ui.hof.entry.header", epithetName, e.sin, FormatClock(e.savedAtUnix), phase) + "\n" +
               speciesLine + "\n" +
               "<color=#9fd4ff>" + TextCatalog.Get("ui.hof.entry.raw_section") + "</color>\n" +
               TextCatalog.Get("ui.hof.entry.raw_line", e.bdCount, e.controlSeconds.ToString("F0"), e.kills) + "\n" +
               cards + staleMark + "\n" +
               "<color=#ffd79f>" + TextCatalog.Get("ui.hof.entry.stats_section", statsTime) + "</color>\n" +
               TextCatalog.Get("ui.hof.entry.stats_line", e.deployed, e.fatal, e.possessed, e.bodyFatal, e.runFail);
    }

    // 同名世代标记：key = 成品名（不含序号），value = 该条在其同名组中的次序（1-based）
    Dictionary<string, int> generationIndex = new Dictionary<string, int>();

    /// <summary>
    /// 构建同名世代索引（方案 §7.4）。
    /// 判定基准用**成品名**（即生成后的词序列），而非 cardIds 集合——
    /// 过滤失效卡后不同 BD 可能产出相同词序列，按名判定才与玩家看到的一致。
    /// 只在真正重名时标记（单例永不带序号）。
    /// </summary>
    static Dictionary<string, int> BuildGenerationIndex(List<HallOfFameEntry> entries)
    {
        var counts = new Dictionary<string, int>();
        foreach (var e in entries)
        {
            if (e == null) continue;
            string name = RawEpithetName(e);
            counts.TryGetValue(name, out int n);
            counts[name] = n + 1;
        }

        var seen = new Dictionary<string, int>();
        var index = new Dictionary<string, int>();
        // 按 savedAt 升序编号：最早的第 1 条不带序号，其后 II / III …
        var sorted = new List<HallOfFameEntry>(entries);
        sorted.Sort((a, b) => a.savedAtUnix.CompareTo(b.savedAtUnix));
        foreach (var e in sorted)
        {
            if (e == null) continue;
            string name = RawEpithetName(e);
            if (counts.TryGetValue(name, out int total) && total < 2) continue; // 不重名则完全不标记
            seen.TryGetValue(name, out int k);
            k++;
            seen[name] = k;
            index[Key(e)] = k;
        }
        return index;
    }

    static string Key(HallOfFameEntry e) => e.runId + "|" + e.sin;

    /// <summary>成品名（不含世代序号）。</summary>
    static string RawEpithetName(HallOfFameEntry e)
    {
        var words = CardEpithetGenerator.DecodeCache(e.epithetCache);
        int valid = e.cardIds != null ? e.cardIds.Count : 0;
        return CardEpithetGenerator.Format(e.sin, words, valid, CardEpithetCatalog.Instance);
    }

    /// <summary>
    /// 条目标题：词缀名 + 世代序号（重名时）。
    /// 无词缀（老档 / 卡池无词）时回退为 Sin 种类名，不虚构词缀、不显示空「之傲慢」（方案 §7.3）。
    /// </summary>
    string EpithetName(HallOfFameEntry e)
    {
        string name = RawEpithetName(e);
        bool hasWords = !string.IsNullOrEmpty(e.epithetCache);
        if (!hasWords) return SinDisplay(e.sin);   // 兜底：只有中心词

        if (generationIndex != null && generationIndex.TryGetValue(Key(e), out int gen) && gen > 1)
            return name + " · " + RomanNumeral(gen);
        return name;
    }

    static string RomanNumeral(int n)
    {
        switch (n)
        {
            case 2: return "II";
            case 3: return "III";
            case 4: return "IV";
            case 5: return "V";
            case 6: return "VI";
            case 7: return "VII";
            case 8: return "VIII";
            case 9: return "IX";
            default: return n.ToString();
        }
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
        // 统一文本目录：阶段文案（ui.hof.phase.*）
        if (!string.IsNullOrEmpty(e.endPhase))
        {
            string result = e.endPhase == "Result" ? TextCatalog.Get("ui.hof.phase.victory")
                : e.endPhase == "Failed" ? TextCatalog.Get("ui.hof.phase.failed")
                : e.endPhase == "Aborted" ? TextCatalog.Get("ui.hof.phase.aborted")
                : e.endPhase == "NewRunInterrupt" ? TextCatalog.Get("ui.hof.phase.newrun") : e.endPhase;
            return e.reachedWave > 0 ? TextCatalog.Get("ui.hof.phase.reached_wave", result, e.reachedWave) : result;
        }
        if (e.stage == "final") return TextCatalog.Get("ui.hof.phase.in_final");
        return e.reachedWave > 0 ? TextCatalog.Get("ui.hof.phase.in_wave", e.reachedWave) : TextCatalog.Get("ui.hof.phase.in_run");
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

        // 标题（TextCatalog 统一管理）
        var title = MakeText(panelBg.transform, TextCatalog.Get("ui.hof.title"), 34, new Color(0.95f, 0.85f, 0.55f));
        Place(title.rectTransform, new Vector2(0f, -34f), new Vector2(600f, 44f), TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;

        // 关闭按钮（右上）
        var close = MakeButton(panelBg.transform, "✕", 46f, 46f);
        Place(close.GetComponent<RectTransform>(), new Vector2(500f, -34f), new Vector2(46f, 46f));
        close.onClick.AddListener(Hide);

        // 工具行：刷新 / 排序 / 状态（TextCatalog 统一管理）
        refreshButton = MakeButton(panelBg.transform, TextCatalog.Get("ui.hof.refresh"), 150f, 42f);
        Place(refreshButton.GetComponent<RectTransform>(), new Vector2(-440f, -86f), new Vector2(150f, 42f));
        refreshButton.onClick.AddListener(() => _ = RefreshFromServer());

        sortButton = MakeButton(panelBg.transform, SortLabel((int)sortKey), 210f, 42f);
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

        // 空状态（§5.9；TextCatalog 统一管理）
        emptyLabel = MakeText(panelBg.transform,
            TextCatalog.Get("ui.hof.empty"), 24,
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
        try { UiFontAssets.ApplyTo(tmp); } catch { /* 字体资产异常时用 TMP 默认 */ }
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
