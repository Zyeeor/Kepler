using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 荣誉殿堂面板（Canonical Meta_Progression §5 + 《荣誉殿堂-简单功能开发案》2026-08-28 Owner 拍板版）。
///
/// 本期实现（开发案 §2/§2.1/§6 本期范围；Owner 三项拍板）：
///   - 记录列表：本地缓存优先 + 分页（每页 5 条，上一页/下一页/页码）+ 滚动条（内容不足自动隐藏）；
///   - 记录详情：整条可点 → 列表内延伸展开（完整构筑 + 两区块完整字段，展开行插入该条目之后；再点收起 / 切换 / ESC / 刷新时收起）
///   - 排序（§5.6 四键循环，默认保存时间倒序）；标题保留词缀名 + 世代序号（Owner 拍板 2）；
///   - 异步战绩 UI 不再展示 bodyFatal 列（Owner 拍板 1；数据字段与服务器回传/ApplyStats 不变，仅展示层移除）；
///   - 在线用真实战绩、离线用模拟战绩展示（Owner 拍板 3）：刷新失败时按 (runId,sin) 哈希生成
///     确定性模拟值（每次打开一致、不落盘、不写入 HallOfFameStore），并明确标注「离线模拟」；
///   - 罪别彩条：条目左侧竖条按 Sin 着色（仅身份识别，不表示稀有度；开发案 §5）。
///
/// 美术接入（SystemUI @ Assets/Resources/SystemUI，参考图 Hall Of Record.png）：
///   - 已接：HOR BG 面板底图（整图）、Page Buttons 翻页箭头（Multiple 子 sprite）、
///     Tips 怪物基础横幅（Multiple 子 sprite Tips_0~6，按 Sin 运行时映射 + 文字区遮罩，开发案 §3.2/§5）、
///     Slider-V 滚动条（Multiple：槽 + handle）；
///   - 待美术切分后接：Order Buttons（排序两态，Single 未切）、Func Buttons（关闭/刷新图标，Single 未切）
///     ——切分（SpriteMode=Multiple + 坐标）属美术侧 Sprite Editor 操作，此前维持运行时估测裁剪。
///
/// 装配：主菜单经 MainMenuController 克隆设置按钮注入入口（零场景编辑）；
/// 面板本体为纯代码 UGUI（自建 Overlay Canvas，模式扩展自 EliteNetworkStatusUI），常驻跨场景。
/// 调试：F6 直接开关（仅非正式流程；CardProgressPanel 同款门禁）。
/// </summary>
public class HallOfFamePanel : MonoBehaviour
{
    public static HallOfFamePanel Instance { get; private set; }

    [Header("服务器（默认值；对局中自动取 EliteBuildDirector 配置）")]
    public string serverUrl = "http://127.0.0.1:8080";
    public int timeoutSeconds = 5;

    /// <summary>列表显示模式：全量滚动（用户需求 2026-08-28：滚动显示替代分页；原 PageSize 分页已移除）。</summary>

    enum SortKey { SavedTime, Kills, RunFail, BdCount }
    // 统一文本目录：排序标签（TextCatalog，运行时取文本；策划改文案不动代码）
    static readonly string[] SortLabelKeys = { "ui.hof.sort.saved_time", "ui.hof.sort.kills", "ui.hof.sort.run_fail", "ui.hof.sort.bd_count" };
    static string SortLabel(int i) => TextCatalog.Get(SortLabelKeys[i]);
    SortKey sortKey = SortKey.SavedTime; // §5.6 默认按保存时间倒序

    GameObject panelRoot;
    Button refreshButton;
    Button[] sortButtons;
    TMP_Text statusLabel;
    TMP_Text emptyLabel;
    Transform contentRoot;
    ScrollRect scrollRect;

    // 分页（开发案 §2）
    Button prevButton;
    Button nextButton;
    TMP_Text pageLabel;
    int currentPage = 1;
    int totalPages = 1;

    // 条目内展开详情（点击条目在列表内延伸显示完整内容，替代全屏弹层；同一时刻仅展开一条）
    GameObject expandedRow;
    string expandedKey;

    /// <summary>离线模拟战绩展示开关（Owner 拍板 3：不在线时用模拟数据渲染，明确标注、不落盘）。</summary>
    bool usingMockStats;

    // SystemUI 美术资源（Assets/Resources/SystemUI；加载失败时回退纯色，保证功能可用）
    Sprite horBgSprite;
    Sprite pageLeftSprite;
    Sprite pageRightSprite;
    /// <summary>殿堂专用字体（Owner 拍板 B：仅本面板用思源黑体 Heavy，庄严档案感；
    /// 其余 UI 保持全局字体。缺失时回退全局字体）。</summary>
    TMP_FontAsset archiveFont;
    /// <summary>Func Buttons（134×66，Single 未切分；左=刷新 ↻ 右=关闭 ✕）的运行时裁剪子 sprite。
    /// SpriteMode=Single 不支持子 sprite 加载，用 Sprite.Create 按估测 rect 切（左 0~66 / 右 68~134），
    /// 不可读纹理也能 Create（仅 UV 映射，不读像素）。坐标若偏，用户反馈后迭代。</summary>
    Sprite funcRefreshSprite;
    Sprite funcCloseSprite;
    /// <summary>Order Buttons（328×234，Single 未切分；左列 4 灰=未选 / 右列 4 金=选中）。
    /// 按估测 rect 运行时裁剪为 4 键×2 态=8 子 sprite。</summary>
    Sprite[,] orderSprites = new Sprite[4, 2];
    /// <summary>Slider-V（Multiple：_0 = 滑块 handle 17×70，_1 = 槽 track 15×642）。</summary>
    Sprite sliderHandleSprite;
    Sprite sliderTrackSprite;
    /// <summary>Tips.png（1868×1287，已 Multiple 切分；7 条目卡模板，按 Sin 顺序 0~6 = Pride→Greed）。</summary>
    Sprite[] tipsSprites = new Sprite[7];
    /// <summary>Card Filters.png（328×422，spriteMode=2 但 meta sprites=空未切分；8 行×2 列=16 运行时裁剪子 sprite；
    /// 行序：傲慢/嫉妬/怠惰/色欲/暴怒/暴食/贪婪/通用，列序：灰/金）。</summary>
    Sprite[,] cardFilterSprites = new Sprite[8, 2];

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
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (expandedRow != null) CollapseExpanded();
            else Hide();
        }
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
        _ = RefreshFromServer(); // 再后台联网刷新（失败 → 离线模拟展示，Owner 拍板 3）
    }

    public void Hide()
    {
        CollapseExpanded();
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ── 列表数据与渲染 ──

    void RenderLocal()
    {
        // 老档 / 词表改版补生成称号（方案 §7.2）：无词缀的条目用当前词表生成一次并写入
        HallOfFameStore.BackfillMissingEpithets();

        var entries = HallOfFameStore.EntriesBySavedTimeDesc();
        entries = ApplySort(entries);
        generationIndex = BuildGenerationIndex(entries);

        if (emptyLabel != null)
            emptyLabel.gameObject.SetActive(entries.Count == 0);

        RenderList(entries);

        if (statusLabel != null && !refreshing)
            statusLabel.text = TextCatalog.Get("ui.hof.status.count", entries.Count);
    }

    /// <summary>全量渲染列表（滚动显示，不分页；倒序销毁旧条目，CoreChoiceUI.RefreshCards 同模式）。</summary>
    void RenderList(List<HallOfFameEntry> entries)
    {
        CollapseExpanded(); // 列表重建（排序/刷新）前收起展开行，避免悬垂引用
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        for (int i = 0; i < entries.Count; i++)
        {
            // 条目内容（怪物横幅 + 罪别彩条 + 罪名 + 正文）已在 MakeEntryRow 内构建
            var row = MakeEntryRow(entries[i]);
            row.transform.SetParent(contentRoot, false);
        }

        // 强制立即重建 content 布局 → ScrollRect 重新计算 handle size（避免初次 handle=viewport 的 bug）
        if (contentRoot != null)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)contentRoot);
            Canvas.ForceUpdateCanvases();
        }
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            // 内容未超出可视区时隐藏滚动条（开发案 §2：内容不足不显示/禁用滚动条，避免满轨道滑块误导）
            var sb = scrollRect.verticalScrollbar;
            if (sb != null)
            {
                var viewH = scrollRect.viewport != null ? scrollRect.viewport.rect.height : 0f;
                var contentH = scrollRect.content != null ? scrollRect.content.rect.height : 0f;
                sb.gameObject.SetActive(contentH > viewH + 1f);
            }
        }
    }

    void UpdatePager()
    {
        // 分页已移除（列表全量滚动显示）；保留方法避免外部调用点报错
    }

    /// <summary>底部 ◀ ▶ 按钮：循环切换排序（用户需求 2026-08-28：分页按钮改为排序切换）。</summary>
    void ShiftSort(int delta)
    {
        int n = SortLabelKeys.Length;
        int next = ((int)sortKey + delta + n) % n;
        SelectSort(next);
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
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // 对局中同步 EliteBuildDirector 的服务器配置（主菜单直开时用面板默认值）
            var director = EliteBuildDirector.Instance;
            string url = director != null ? director.serverUrl : serverUrl;
            var client = new EliteNetClient(
                url,
                director != null ? director.timeoutSeconds : timeoutSeconds,
                director != null && director.logRawResponses);
            Debug.Log($"[HallOfFame] 战绩刷新请求：player={DeviceIdentity.Id} server={url}");
            var resp = await client.FetchStats(DeviceIdentity.Id);
            int fetched = resp != null && resp.stats != null ? resp.stats.Count : 0;
            int applied = resp != null && resp.stats != null
                ? HallOfFameStore.ApplyStats(resp.stats) : 0;
            usingMockStats = false; // 在线：真实数据（Owner 拍板 3）
            Debug.Log($"[HallOfFame] 战绩刷新完成：server 返回 {fetched} 条，本地匹配 {applied} 条，耗时 {sw.ElapsedMilliseconds}ms。");
            SetStatus(TextCatalog.Get("ui.hof.status.refreshed", NowClock(), applied));
        }
        catch (Exception e)
        {
            // §5.7/§5.10：断网仍可查看本地荣誉记录；Owner 拍板 3：离线改用模拟战绩展示（仅显示层，不落盘）
            usingMockStats = true;
            SetStatus(TextCatalog.Get("ui.hof.status.offline"));
            Debug.Log($"[HallOfFame] 战绩刷新失败（切换为离线模拟战绩展示）：{e.Message}");
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

    void SelectSort(int index)
    {
        if ((int)sortKey == index) return;
        sortKey = (SortKey)index;
        RefreshSortButtons();
        RenderLocal();
    }

    /// <summary>排序栏选中态：sprite 两态切换（Order Buttons 左列灰=未选 / 右列金=选中）。</summary>
    void RefreshSortButtons()
    {
        if (sortButtons == null) return;
        for (int i = 0; i < sortButtons.Length; i++)
        {
            if (sortButtons[i] == null) continue;
            var img = sortButtons[i].image;
            bool spriteReady = orderSprites != null && orderSprites[i, 0] != null && orderSprites[i, 1] != null;
            if (spriteReady)
            {
                img.sprite = (i == (int)sortKey) ? orderSprites[i, 1] : orderSprites[i, 0];
                img.color = Color.white;
                img.preserveAspect = true;
                img.type = Image.Type.Simple;
            }
            // sprite 缺失时按钮保持空白（安全回退，不显示半生不熟的 TMP 文字与美术字不一致）
        }
    }

    // ── 条目格式化（§5.4 两段式摘要；Owner 拍板 1：不展示 bodyFatal 列）──
    // 注：战绩行与分页/详情等新文案暂为代码内嵌（bodyFatal 移除后 ui.hof.entry.stats_line 的 5 参数模板
    // 不再适用），待策划增补 TextCatalog 键（ui.hof.stats_line4 / ui.hof.pager.* / ui.hof.detail.*）后迁回。

    string FormatEntry(HallOfFameEntry e)
    {
        // 列表摘要（开发案 §2 字段 + §5 两区块：冷蓝"原始 Run 表现" / 暖橙"异步战绩"；
        // 卡牌完整清单进详情页，列表不展示——§5"列表默认仅展示摘要"）
        //   第 1 行：词缀名 + 阶段（含日期）
        //   第 2 行：种类
        //   第 3~4 行：原始 Run 表现区块（构筑深度 / 控制时长 / 本局击杀）
        //   第 5~6 行：异步战绩区块（四计数器 + 同步状态）
        string epithetName = EpithetName(e);
        string sinName = SinDisplay(e.sin);
        string phase = PhaseText(e);
        string statsTag = usingMockStats ? "（离线模拟）"
            : e.statsUpdatedAtUnix > 0 ? $"（{FormatClock(e.statsUpdatedAtUnix)}）"
            : TextCatalog.Get("ui.hof.entry.not_synced");
        int deployed, fatal, possessed, runFail;
        if (usingMockStats)
        {
            var m = MockStatsFor(e);
            deployed = m[0]; fatal = m[1]; possessed = m[2]; runFail = m[3];
        }
        else
        {
            deployed = e.deployed; fatal = e.fatal; possessed = e.possessed; runFail = e.runFail;
        }

        return $"<b>{epithetName}</b> · {phase} · {FormatClock(e.savedAtUnix)}\n" +
               $"{sinName}\n" +
               "<color=#9fd4ff>──── 原始 Run 表现 ────</color>\n" +
               $"构筑深度 {e.bdCount}│控制 {e.controlSeconds:F0} 秒│本局击杀 {e.kills}\n" +
               $"<color=#ffd79f>──── 异步战绩 {statsTag} ────</color>\n" +
               $"被投放 {deployed}│被击杀 {fatal}│被附身 {possessed}│杀敌 {runFail}";
    }

    /// <summary>战绩区块标题：在线 = 异步战绩（同步时间/未同步）；离线 = 异步战绩（离线模拟）。</summary>
    string StatsSectionTitle(HallOfFameEntry e)
    {
        if (usingMockStats) return "──── 异步战绩（离线模拟）────";
        string statsTime = e.statsUpdatedAtUnix > 0
            ? TextCatalog.Get("ui.hof.entry.synced_at", FormatClock(e.statsUpdatedAtUnix))
            : TextCatalog.Get("ui.hof.entry.not_synced");
        return TextCatalog.Get("ui.hof.entry.stats_section", statsTime);
    }

    /// <summary>战绩行：四计数器（Owner 拍板 1：bodyFatal 不展示；数据层照常接收存储）。</summary>
    string StatsLine(HallOfFameEntry e)
    {
        int deployed, fatal, possessed, runFail;
        if (usingMockStats)
        {
            var m = MockStatsFor(e);
            deployed = m[0]; fatal = m[1]; possessed = m[2]; runFail = m[3];
        }
        else
        {
            deployed = e.deployed; fatal = e.fatal; possessed = e.possessed; runFail = e.runFail;
        }
        return $"被投放 {deployed}│被击杀 {fatal}│被附身 {possessed}│杀敌 {runFail}";
    }

    // ── 条目内展开详情（开发案 §2.1 完整字段；点击条目在列表内延伸展示，替代全屏弹层）──

    /// <summary>点击条目：同一条目再点收起，点击其他条目切换；展开行插入到该条目行之后。</summary>
    void ToggleExpand(HallOfFameEntry e)
    {
        if (e == null || contentRoot == null) return;
        string key = e.runId + "|" + e.sin;
        if (expandedRow != null && expandedKey == key)
        {
            CollapseExpanded();
            return;
        }
        CollapseExpanded();
        var row = MakeExpandRow(e);
        if (row == null) return;
        expandedRow = row;
        expandedKey = key;
        // 立即重算布局，ScrollRect / 滚动条同步更新
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)contentRoot);
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>收起当前展开行（销毁对象；列表刷新/隐藏/ESC/切换时调用）。</summary>
    void CollapseExpanded()
    {
        if (expandedRow != null)
        {
            Destroy(expandedRow);
            expandedRow = null;
        }
        expandedKey = null;
    }

    /// <summary>
    /// 构建展开行：深色底 + 左缘罪别色条 + 完整构筑与两区块统计（FormatDetail），
    /// 插入到被点击条目行之后（SetSiblingIndex），列表上下文内延伸展示。
    /// </summary>
    GameObject MakeExpandRow(HallOfFameEntry e)
    {
        string want = EntryRowName(e);
        Transform target = null;
        for (int i = 0; i < contentRoot.childCount; i++)
        {
            if (contentRoot.GetChild(i).name == want)
            {
                target = contentRoot.GetChild(i);
                break;
            }
        }
        if (target == null) return null; // 条目行不存在（列表已刷新）则不展开

        var go = new GameObject("ExpandedRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(contentRoot, false);
        go.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.12f, 0.95f); // 深色底，与条目卡片区分

        // 左缘罪别色条（与条目行同识别色）
        var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(go.transform, false);
        var barImg = bar.GetComponent<Image>();
        barImg.color = SinUIColor(e.sin);
        barImg.raycastTarget = false;
        var barRt = bar.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 0f);
        barRt.anchorMax = new Vector2(0f, 1f);
        barRt.pivot = new Vector2(0f, 0.5f);
        barRt.sizeDelta = new Vector2(8f, 0f);

        // 完整内容文字：Stretch 铺满展开行（避免固定锚点 + offsetMax/Min 产生负 sizeDelta 导致文字宽度退化）
        var text = MakeText(go.transform, FormatDetail(e), 24, new Color(0.95f, 0.95f, 0.98f));
        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.pivot = new Vector2(0f, 1f);
        trt.anchoredPosition = Vector2.zero;
        trt.offsetMin = new Vector2(24f, 12f);
        trt.offsetMax = new Vector2(-24f, -12f);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;

        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 300f; // 容纳 8~10 行完整内容（24 号）；换行超出时按 preferred 自动撑高
        le.flexibleHeight = 0f;
        go.transform.SetSiblingIndex(target.GetSiblingIndex() + 1);
        return go;
    }

    /// <summary>条目行命名（runId+sin 唯一标识，供展开行定位插入位置）。</summary>
    static string EntryRowName(HallOfFameEntry e) => "EntryRow_" + e.runId + "_" + e.sin;

    string FormatDetail(HallOfFameEntry e)
    {
        string epithetName = EpithetName(e);
        string sinName = SinDisplay(e.sin);
        string cards = e.cardIds != null && e.cardIds.Count > 0 ? string.Join("、", e.cardIds) : TextCatalog.Get("ui.hof.entry.no_cards");
        string staleMark = HasStaleCards(e) ? "  " + TextCatalog.Get("ui.hof.entry.stale_cards") : "";

        return $"<size=30><b>{epithetName}</b></size>\n" +
               $"{sinName}（{e.sin}） · {FormatClock(e.savedAtUnix)} · {PhaseText(e)}\n" +
               "<color=#9fd4ff>──── 原始 Run 表现 ────</color>\n" +
               $"构筑深度：{e.bdCount}\n" +
               $"本局控制时长：{e.controlSeconds:F0} 秒\n" +
               $"本局击杀数：{e.kills}\n" +
               $"卡牌清单（{(e.cardIds != null ? e.cardIds.Count : 0)} 张）：{cards}{staleMark}\n" +
               "<color=#ffd79f>──── " + (usingMockStats ? "异步战绩（离线模拟）" :
                   (e.statsUpdatedAtUnix > 0
                       ? TextCatalog.Get("ui.hof.entry.stats_section", TextCatalog.Get("ui.hof.entry.synced_at", FormatClock(e.statsUpdatedAtUnix)))
                       : TextCatalog.Get("ui.hof.entry.stats_section", TextCatalog.Get("ui.hof.entry.not_synced")))).Trim('─', ' ') + " ────</color>\n" +
               StatsLine(e);
    }

    /// <summary>离线模拟战绩（Owner 拍板 3）：按 (runId,sin) FNV-1a 哈希生成确定性数值——
    /// 每次打开一致、不落盘、不覆盖真实战绩；仅渲染层展示并明确标注。</summary>
    static int[] MockStatsFor(HallOfFameEntry e)
    {
        uint h = Fnv1a(e.runId + "|" + e.sin);
        int deployed = 2 + (int)(h % 8);                        // 2..9
        int fatal = (int)((h >> 8) % (uint)(deployed + 1));     // 0..deployed
        int possessed = (int)((h >> 16) % 4u);                  // 0..3
        int runFail = (int)((h >> 24) % (uint)(fatal + 1));     // 0..fatal
        return new[] { deployed, fatal, possessed, runFail };
    }

    static uint Fnv1a(string s)
    {
        uint h = 2166136261;
        if (s == null) return h;
        foreach (var c in s) { h ^= c; h *= 16777619; }
        return h;
    }

    // ── 同名世代标记（方案 §7.4）──

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

    /// <summary>Sin wire name → Tips/Card Filters sprite 索引（按美术切分的视觉顺序）：
    /// 0=傲慢 1=嫉妒 2=怠惰 3=色欲 4=暴怒 5=暴食 6=贪婪 7=通用（未识别回退）。</summary>
    static readonly Dictionary<string, int> SinToSpriteIndex = new Dictionary<string, int>
    {
        { "pride",    0 }, { "envy",    1 }, { "sloth",  2 }, { "lust",     3 },
        { "wrath",    4 }, { "gluttony",5 }, { "greed",  6 }, { "",         7 },
    };

    /// <summary>wire 名 → sprite 索引（未识别回退到 7=通用）。</summary>
    static int SinSpriteIndex(string wire)
    {
        if (string.IsNullOrEmpty(wire)) return 7;
        return SinToSpriteIndex.TryGetValue(wire.ToLowerInvariant(), out int i) ? i : 7;
    }

    /// <summary>罪别 UI 色（开发案 §5 左侧识别条；仅身份识别不表稀有度）。
    /// 色系按 SystemUI 参考图（Hall Of Record.png）：紫蓝/紫/红/橙/金/青/玫红。</summary>
    static Color SinUIColor(string wire)
    {
        if (Enum.TryParse(wire, true, out SinType sin) && sin != SinType.None)
        {
            switch (sin)
            {
                case SinType.Pride: return new Color(0.75f, 0.40f, 0.90f);    // 紫
                case SinType.Wrath: return new Color(0.90f, 0.25f, 0.20f);    // 红
                case SinType.Gluttony: return new Color(0.90f, 0.55f, 0.15f); // 橙
                case SinType.Greed: return new Color(0.95f, 0.78f, 0.25f);    // 金
                case SinType.Envy: return new Color(0.25f, 0.85f, 0.80f);     // 青
                case SinType.Lust: return new Color(0.95f, 0.35f, 0.55f);     // 玫红
                case SinType.Sloth: return new Color(0.45f, 0.50f, 0.95f);    // 紫蓝
            }
        }
        return new Color(0.55f, 0.57f, 0.62f); // 未识别：中性灰（历史记录回退）
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
        LoadSystemUISprites();

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

        // 全屏背景（美术意图：HOR BG 1920×1080 与 Canvas 参考分辨率 1:1 无失真铺满；
        // 无资源时回退半透明压暗）
        panelRoot = new GameObject("PanelRoot", typeof(RectTransform), typeof(Image));
        panelRoot.transform.SetParent(transform, false);
        Stretch(panelRoot.GetComponent<RectTransform>());
        var rootImg = panelRoot.GetComponent<Image>();
        if (horBgSprite != null)
        {
            rootImg.sprite = horBgSprite;
            rootImg.color = Color.white;
        }
        else
        {
            rootImg.color = new Color(0f, 0f, 0f, 0.85f);
        }
        panelRoot.SetActive(false);
        Transform ui = panelRoot.transform; // 全屏根（原 1080×700 中间底板已移除）

        // 标题（参考图：左上角，避开 HOR BG 左侧金边装饰；边框避让 ~90px）
        var title = MakeText(ui, TextCatalog.Get("ui.hof.title"), 66, new Color(0.72f, 0.55f, 0.30f));
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Left;
        var titleRect = title.rectTransform;
        titleRect.anchorMin = titleRect.anchorMax = titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(90f, -40f);
        titleRect.sizeDelta = new Vector2(860f, 92f);

        // 标题下金色横线（颜色与 HOR BG 边框一致；从左边框到右边框）
        var titleLine = new GameObject("TitleLine", typeof(RectTransform), typeof(Image));
        titleLine.transform.SetParent(ui, false);
        var lineRect = titleLine.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0f, 1f);
        lineRect.anchorMax = new Vector2(1f, 1f);
        lineRect.offsetMin = new Vector2(90f, -141f);
        lineRect.offsetMax = new Vector2(-90f, -138f);
        titleLine.GetComponent<Image>().color = new Color(0.90f, 0.78f, 0.48f, 0.9f);
        titleLine.GetComponent<Image>().raycastTarget = false;

        // 顶部右侧：刷新 + 关闭（Func Buttons 运行时裁剪的子 sprite；sprite 缺失回退文字）
        refreshButton = MakeButton(ui, funcRefreshSprite != null ? "" : "↻", 46f, 46f);
        PlaceTopRight(refreshButton.GetComponent<RectTransform>(), -150f, -58f, 46f, 46f);
        refreshButton.onClick.AddListener(() => _ = RefreshFromServer());
        ApplyIconButton(refreshButton, funcRefreshSprite);

        var close = MakeButton(ui, funcCloseSprite != null ? "" : "✕", 46f, 46f);
        PlaceTopRight(close.GetComponent<RectTransform>(), -90f, -58f, 46f, 46f);
        close.onClick.AddListener(Hide);
        ApplyIconButton(close, funcCloseSprite);

        // 排序栏（参考图：横线下方、左缘与标题对齐；4 键并排透明底、大字号，
        // 选中金色+下划线；Order Buttons sprite 切分后替换为图形态两态样式）
        // 排序栏（参考图 Order Buttons sprite：左列灰=未选 / 右列金=选中；按钮内为美术预设字，无 TMP）
        sortButtons = new Button[SortLabelKeys.Length];
        const float keyW = 164f, keyGap = 14f, keyH = 58f; // 贴合 sprite 164:58 比例，让按钮容器 = sprite 宽，左对齐横线
        for (int i = 0; i < sortButtons.Length; i++)
        {
            var go = new GameObject("SortKey_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(ui, false);
            var img = go.GetComponent<Image>();
            img.preserveAspect = true; // sprite 比例居中（容器 200×58，sprite 164×58 等比缩放）
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f); // 顶部左锚（pivot=左上角）
            rt.anchoredPosition = new Vector2(90f + i * (keyW + keyGap), -158f); // 左缘与标题（90px）对齐
            rt.sizeDelta = new Vector2(keyW, keyH);
            int idx = i; // 闭包捕获拷贝
            btn.onClick.AddListener(() => SelectSort(idx));
            sortButtons[i] = btn;
        }
        RefreshSortButtons();

        // 滚动列表区（全屏布局：四边避开 HOR BG 边框装饰 ~90px；顶部让出标题+排序栏）
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollGo.transform.SetParent(ui, false);
        var scrollRect2 = scrollGo.GetComponent<RectTransform>();
        scrollRect2.anchorMin = new Vector2(0f, 0f);
        scrollRect2.anchorMax = new Vector2(1f, 1f);
        scrollRect2.offsetMin = new Vector2(90f, 120f);    // 左边框 + 底部分页栏
        scrollRect2.offsetMax = new Vector2(-98f, -245f); // 右侧滚动条 + 顶部标题/横线/排序栏
        scrollGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f); // 无底色（原 4% 白蒙层已移除；保留透明 Image 维持射线接收）
        scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 30f;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(scrollGo.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // RectMask2D 不依赖像素 alpha（硬矩形裁剪）
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
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRect;
        contentRoot = content.transform;

        // 滚动条（开发案 §2：滑块随内容量动态调整；
        // 用 Permanent 可见——AutoHide 在条目入列表前 ScrollRect 误判 content 不超出导致永久隐藏，
        // 全屏布局：Stretch 右锚到列表区右侧 +10px，与列表区上下对齐）
        var sbGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        sbGo.transform.SetParent(ui, false);
        var sbRect = sbGo.GetComponent<RectTransform>();
        sbRect.anchorMin = new Vector2(1f, 0f);
        sbRect.anchorMax = new Vector2(1f, 1f);
        sbRect.pivot = new Vector2(0.5f, 0.5f);
        sbRect.offsetMin = new Vector2(-98f, 120f);
        sbRect.offsetMax = new Vector2(-90f, -245f);
        // 轨道槽（SystemUI/Slider-V.png _1 = track 15×642；缺失时回退纯色）
        var sbImg = sbGo.GetComponent<Image>();
        if (sliderTrackSprite != null)
        {
            sbImg.sprite = sliderTrackSprite;
            sbImg.color = Color.white;
            sbImg.type = Image.Type.Simple;
        }
        else
        {
            sbImg.color = new Color(1f, 1f, 1f, 0.12f);
        }
        var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.transform.SetParent(sbGo.transform, false);
        Stretch(handleGo.GetComponent<RectTransform>());
        var handleImg = handleGo.GetComponent<Image>();
        // 滑块（SystemUI/Slider-V.png _0 = handle 17×70；缺失时回退纯色）
        if (sliderHandleSprite != null)
        {
            handleImg.sprite = sliderHandleSprite;
            handleImg.color = Color.white;
            handleImg.type = Image.Type.Simple;
        }
        else
        {
            handleImg.color = new Color(0.85f, 0.88f, 0.95f, 0.85f);
        }
        var scrollbar = sbGo.GetComponent<Scrollbar>();
        // 必须显式赋值 handleRect：运行时创建不会自动查找子对象，缺失时滑块不跟随、拖动换算失效
        scrollbar.handleRect = handleGo.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handleImg;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        // 底部右下角箭头（用户需求 2026-08-28：从翻页改为排序循环切换；◀=上一个排序 ▶=下一个排序）
        prevButton = MakeButton(ui, pageLeftSprite != null ? "" : "◀ 排序", 120f, 36f);
        Place(prevButton.GetComponent<RectTransform>(), new Vector2(700f, -462f), new Vector2(120f, 36f));
        prevButton.onClick.AddListener(() => ShiftSort(-1));
        ApplyIconButton(prevButton, pageLeftSprite);

        nextButton = MakeButton(ui, pageRightSprite != null ? "" : "排序 ▶", 120f, 36f);
        Place(nextButton.GetComponent<RectTransform>(), new Vector2(840f, -462f), new Vector2(120f, 36f));
        nextButton.onClick.AddListener(() => ShiftSort(+1));
        ApplyIconButton(nextButton, pageRightSprite);

        // 空状态（§5.9；TextCatalog 统一管理）
        emptyLabel = MakeText(ui,
            TextCatalog.Get("ui.hof.empty"), 24,
            new Color(0.7f, 0.72f, 0.8f));
        Place(emptyLabel.rectTransform, Vector2.zero, new Vector2(760f, 220f), TextAlignmentOptions.Center);
        emptyLabel.gameObject.SetActive(false);
    }

    /// <summary>列表条目行：可点击整行展开详情（开发案 §2.1）+ 左侧罪别彩条（开发案 §5）。</summary>
    GameObject MakeEntryRow(HallOfFameEntry entry)
    {
        var go = new GameObject(EntryRowName(entry), typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        var img = go.GetComponent<Image>();
        // Tips sprite 作条目卡背景（拉伸到容器；已含左侧罪别彩条 + 中部怪物菱形图标，无需单独叠彩条）
        int tipIdx = SinSpriteIndex(entry.sin);
        if (tipIdx >= 0 && tipIdx < tipsSprites.Length && tipsSprites[tipIdx] != null)
        {
            img.sprite = tipsSprites[tipIdx];
            img.color = Color.white;
            img.type = Image.Type.Simple;
            img.preserveAspect = false; // 拉伸填满整条
        }
        else
        {
            img.color = new Color(1f, 1f, 1f, 0.10f); // sprite 缺失兜底
        }
        var button = go.GetComponent<Button>();
        button.targetGraphic = img;
        var captured = entry;
        button.onClick.AddListener(() => ToggleExpand(captured));

        // 半透明深色遮罩（开发案 §5：横幅上叠加遮罩，确保文字与数据清晰可读）
        var maskGo = new GameObject("Mask", typeof(RectTransform), typeof(Image));
        maskGo.transform.SetParent(go.transform, false);
        Stretch(maskGo.GetComponent<RectTransform>());
        var maskImg = maskGo.GetComponent<Image>();
        maskImg.color = new Color(0f, 0f, 0f, 0.45f);
        maskImg.raycastTarget = false;

        var text = MakeText(go.transform, FormatEntry(entry), 26, new Color(0.95f, 0.95f, 0.98f));
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(380f, 12f); // 左缘避开罪别彩条 + Tips 怪物菱形图标（容器宽约 1690，图标占 ~340px）
        text.rectTransform.offsetMax = new Vector2(-24f, -10f);
        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 210f; // 6 行两区块文字（26 号）+ Tips sprite 等比缩放
        le.flexibleHeight = 0f;
        return go;
    }

    // ── UI 小工具 ──

    /// <summary>加载 SystemUI 美术资源（Assets/Resources/SystemUI）。
    /// Page Buttons 为 Multiple 切图（子 sprite "Page Buttons_0/_1" = 左/右箭头）；
    /// Func Buttons 为 Single 未切分，运行时按估测 rect 裁剪为子 sprite（左=刷新 ↻ / 右=关闭 ✕）；
    /// 任何一项缺失都回退纯色/文字方案，不阻断面板功能。</summary>
    void LoadSystemUISprites()
    {
        horBgSprite = Resources.Load<Sprite>("SystemUI/HOR BG");
        var pageSprites = Resources.LoadAll<Sprite>("SystemUI/Page Buttons");
        foreach (var s in pageSprites)
        {
            if (s.name.EndsWith("_0")) pageLeftSprite = s;
            else if (s.name.EndsWith("_1")) pageRightSprite = s;
        }
        // Func Buttons 运行时裁剪（134×66，左 0~66 = 刷新 ↻，右 68~134 = 关闭 ✕；坐标为像素，左下原点）
        // 注意：sprite 类型导入的 PNG 须 Load<Sprite>().texture 取纹理（直接 Load<Texture2D> 返回 null）
        Sprite funcWhole = Resources.Load<Sprite>("SystemUI/Func Buttons");
        Texture2D funcTex = funcWhole != null ? funcWhole.texture : Resources.Load<Texture2D>("SystemUI/Func Buttons");
        if (funcTex != null)
        {
            funcRefreshSprite = Sprite.Create(funcTex, new Rect(2f, 2f, 62f, 62f), new Vector2(0.5f, 0.5f), 100f);
            funcCloseSprite = Sprite.Create(funcTex, new Rect(70f, 2f, 62f, 62f), new Vector2(0.5f, 0.5f), 100f);
        }
        // Order Buttons 运行时裁剪（328×234，左列 x=0~164 灰=未选，右列 x=164~328 金=选中；
        // y 左下原点：顶部行=保存时间 → y=175.5~234，底部行=构筑深度 → y=0~58.5；
        // 每行 pivot.y 独立——Order Buttons 图里 4 行的视觉中心不在 rect 几何中心，
        // 统一 pivot 0.5 会导致视觉错位，pivot 数组按图视觉位置估测）
        Sprite orderWhole = Resources.Load<Sprite>("SystemUI/Order Buttons");
        Texture2D orderTex = orderWhole != null ? orderWhole.texture : Resources.Load<Texture2D>("SystemUI/Order Buttons");
        if (orderTex != null)
        {
            const float rowH = 58.5f, texH = 234f;
            // 视觉中心 pivot.y（0=底，1=顶；>0.5 让 sprite 在 Image 内整体下移补偿原偏上的视觉内容）
            float[] pivotsY = { 0.62f, 0.42f, 0.50f, 0.60f }; // 保存时间/杀敌次数/入侵战绩/构筑深度
            for (int i = 0; i < 4; i++)
            {
                float yFromBottom = texH - (i + 1) * rowH;
                Vector2 pivot = new Vector2(0.5f, pivotsY[i]);
                orderSprites[i, 0] = Sprite.Create(orderTex,
                    new Rect(0f, yFromBottom, 164f, rowH), pivot, 100f);
                orderSprites[i, 1] = Sprite.Create(orderTex,
                    new Rect(164f, yFromBottom, 164f, rowH), pivot, 100f);
            }
        }
        // Slider-V 垂直滚动条（Multiple：_0 = handle 滑块 / _1 = track 槽）
        var sliders = Resources.LoadAll<Sprite>("SystemUI/Slider-V");
        if (sliders != null)
        {
            foreach (var s in sliders)
            {
                if (s.name.EndsWith("_0")) sliderHandleSprite = s;
                else if (s.name.EndsWith("_1")) sliderTrackSprite = s;
            }
        }

        // Tips.png（已切分 7 子 sprite Tips_0~Tips_6；Unity LoadAll 按 name 字符串排序 = 视觉顺序 傲慢→贪婪，
        // 与 Sin 视觉顺序一致：0=Pride 1=Envy 2=Sloth 3=Lust 4=Wrath 5=Gluttony 6=Greed）
        var tipSprites = Resources.LoadAll<Sprite>("SystemUI/Tips");
        for (int i = 0; i < tipSprites.Length && i < this.tipsSprites.Length; i++)
            this.tipsSprites[i] = tipSprites[i];

        // Card Filters.png（meta sprites=[] 未切分，运行时 Sprite.Create 裁 8 行×2 列=16 子 sprite；
        // 行序：傲慢/嫉妬/怠惰/色欲/暴怒/暴食/贪婪/通用，列：左灰 x=0~164 / 右金 x=164~328）
        var cfWhole = Resources.Load<Sprite>("SystemUI/Card Filters");
        Texture2D cfTex = cfWhole != null ? cfWhole.texture : Resources.Load<Texture2D>("SystemUI/Card Filters");
        if (cfTex != null)
        {
            const float rowH = 52.75f, texH = 422f, keyW = 164f;
            for (int i = 0; i < 8; i++)
            {
                float yFromBottom = texH - (i + 1) * rowH; // i=0（傲慢）取顶部行
                cardFilterSprites[i, 0] = Sprite.Create(cfTex, new Rect(0f, yFromBottom, keyW, rowH), new Vector2(0.5f, 0.5f), 100f);
                cardFilterSprites[i, 1] = Sprite.Create(cfTex, new Rect(keyW, yFromBottom, keyW, rowH), new Vector2(0.5f, 0.5f), 100f);
            }
        }
        if (horBgSprite == null || pageLeftSprite == null || pageRightSprite == null || funcRefreshSprite == null || funcCloseSprite == null)
            Debug.Log("[HallOfFame] SystemUI 资源部分缺失，对应控件回退纯色/文字样式。");

        // 殿堂专用字体（Owner 拍板 B：思源黑体 Heavy，仅本面板；缺失回退全局字体）
        // TODO: 临时禁用——该 SDF 资产烘焙时未包含中文字形，运行时全部中文显示为 □ □
        //       恢复方案见用户反馈（三选一：①美术在 Unity 里 Update Atlas 重新烘焙含中文字符集
        //       ②项目级开启 TMP Dynamic Update 让运行时按需补字符 ③改用已含中文的其他字体）
        archiveFont = null;
    }

    /// <summary>给文字按钮换图标 sprite（保持点击区域不变；清空文字、等比缩放）。</summary>
    static void ApplyIconButton(Button button, Sprite icon)
    {
        if (button == null || icon == null) return;
        button.image.sprite = icon;
        button.image.color = Color.white;
        button.image.preserveAspect = true;
        var label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = "";
    }

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

    /// <summary>顶部右锚定位（pivot 同锚点）：xOffset 为距右缘的负偏移，yOffset 为距顶缘的负偏移。</summary>
    static void PlaceTopRight(RectTransform rt, float xOffset, float yOffset, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(xOffset, yOffset);
        rt.sizeDelta = new Vector2(w, h);
    }

    TMP_Text MakeText(Transform parent, string text, float size, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.raycastTarget = false;
        // Owner 拍板 B：殿堂用思源黑体 Heavy（庄严档案感）；缺失回退全局字体
        try
        {
            if (archiveFont != null) tmp.font = archiveFont;
            else UiFontAssets.ApplyTo(tmp);
        }
        catch { /* 字体资产异常时用 TMP 默认 */ }
        return tmp;
    }

    Button MakeButton(Transform parent, string label, float w, float h)
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
