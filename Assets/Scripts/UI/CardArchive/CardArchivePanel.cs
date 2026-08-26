using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 卡牌图鉴面板（局外系统 §4）：纯代码 UI，主菜单入口打开。
/// 三态渲染：Unknown=剪影(???) / Known=卡面但置灰 / Unlocked=完整+时间戳+次数+新解锁角标。
/// 分类页签：全部 / 七宗罪 / 通用。进度分母取自 CardArchiveStore.ValidCardTotal（Run 内刷新）。
/// </summary>
public class CardArchivePanel : MonoBehaviour
{
    static CardArchivePanel instance;
    GameObject canvasGO;
    RectTransform contentRoot;
    TextMeshProUGUI titleText, progressText, statusText;
    Transform tabRow;
    string currentTab = "all";
    bool visible;
    public System.Action onClose;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static CardArchivePanel EnsureInstance()
    {
        if (instance == null)
        {
            var existing = Object.FindObjectOfType<CardArchivePanel>();
            if (existing != null)
            {
                instance = existing;
            }
            else
            {
                var go = new GameObject(nameof(CardArchivePanel));
                instance = go.AddComponent<CardArchivePanel>();
            }
            DontDestroyOnLoad(instance.gameObject);
            instance.BuildUI();
        }
        return instance;
    }

    void BuildUI()
    {
        canvasGO = new GameObject("CardArchiveCanvas");
        var cv = canvasGO.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 100;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();

        var root = new GameObject("Root");
        root.transform.SetParent(canvasGO.transform);
        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(860, 620);
        rt.anchoredPosition = Vector2.zero;
        var img = root.AddComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.1f, 0.97f);

        titleText = AddText(root.transform, "卡牌图鉴", 28, new Vector2(0, 288), TextAlignmentOptions.Center, 760);
        progressText = AddText(root.transform, "", 16, new Vector2(0, 258), TextAlignmentOptions.Center, 760);
        statusText = AddText(root.transform, "", 13, new Vector2(0, -296), TextAlignmentOptions.Center, 760);

        // 关闭按钮
        var closeBtn = new GameObject("Close");
        closeBtn.transform.SetParent(root.transform);
        var crt = closeBtn.AddComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(1, 1);
        crt.pivot = new Vector2(1, 1);
        crt.anchoredPosition = new Vector2(-14, -14);
        crt.sizeDelta = new Vector2(90, 38);
        closeBtn.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f);
        var cbtn = closeBtn.AddComponent<Button>();
        cbtn.onClick.AddListener(Hide);
        AddText(closeBtn.transform, "关闭", 16, Vector2.zero, TextAlignmentOptions.Center, 90);

        // 页签行
        var tabGO = new GameObject("Tabs");
        tabGO.transform.SetParent(root.transform);
        tabRow = tabGO.transform;
        var trt = tabGO.AddComponent<RectTransform>();
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1);
        trt.pivot = new Vector2(0.5f, 1);
        trt.anchoredPosition = new Vector2(0, -54);
        trt.sizeDelta = new Vector2(820, 36);
        var thlg = tabGO.AddComponent<HorizontalLayoutGroup>();
        thlg.spacing = 6; thlg.childAlignment = TextAnchor.UpperCenter;
        thlg.padding = new RectOffset(4, 4, 4, 4);

        // 滚动区
        var scrollGO = new GameObject("Scroll");
        scrollGO.transform.SetParent(root.transform);
        var srt = scrollGO.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0, 0);
        srt.anchorMax = new Vector2(1, 1);
        srt.offsetMin = new Vector2(16, 84);
        srt.offsetMax = new Vector2(-16, -96);
        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.vertical = true; scroll.horizontal = false;

        var viewport = new GameObject("Viewport").AddComponent<RectTransform>();
        viewport.SetParent(scrollGO.transform);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewport.gameObject.AddComponent<Image>().color = Color.white;
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        scroll.viewport = viewport;

        contentRoot = new GameObject("Content").AddComponent<RectTransform>();
        contentRoot.SetParent(viewport);
        contentRoot.anchorMin = new Vector2(0, 1);
        contentRoot.anchorMax = new Vector2(1, 1);
        contentRoot.pivot = new Vector2(0, 1);
        contentRoot.sizeDelta = new Vector2(0, 0);
        var grid = contentRoot.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(190, 130);
        grid.spacing = new Vector2(10, 10);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.childAlignment = TextAnchor.UpperLeft;
        var fit = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = contentRoot;

        BuildTabs();
        canvasGO.SetActive(false);
        if (FontRegistry.Instance != null) FontRegistry.Instance.ApplyToTree(canvasGO.transform);
    }

    void BuildTabs()
    {
        var tabs = new List<string> { "all" };
        foreach (SinType s in System.Enum.GetValues(typeof(SinType)))
            if (s != SinType.None) tabs.Add(s.ToString());
        tabs.Add("Universal");

        foreach (var t in tabs)
        {
            var btn = new GameObject("Tab_" + t);
            btn.transform.SetParent(tabRow);
            btn.AddComponent<Image>().color = new Color(0.22f, 0.22f, 0.28f);
            var b = btn.AddComponent<Button>();
            var label = t == "all" ? "全部" : (t == "Universal" ? "通用" : SinDisplayName(t));
            AddText(btn.transform, label, 15, Vector2.zero, TextAlignmentOptions.Center, 90);
            var captured = t;
            b.onClick.AddListener(() => { currentTab = captured; Refresh(); });
        }
    }

    static string SinDisplayName(string s) => s;

    public void Show()
    {
        EnsureInstance();
        CardArchiveStore.MarkAllRead();   // 打开即视为已读（清除新解锁角标）
        canvasGO.SetActive(true);
        visible = true;
        Refresh();
    }

    public void Hide()
    {
        if (canvasGO != null) canvasGO.SetActive(false);
        visible = false;
        onClose?.Invoke();
        onClose = null;   // 一次性回调：避免 DDOL 单例跨场景后调用已销毁主菜单方法
    }

    public bool IsVisible() => visible;

    void Refresh()
    {
        if (contentRoot == null) return;
        // 清空
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Object.Destroy(contentRoot.GetChild(i).gameObject);

        var entries = CardArchiveStore.AllEntries();
        int knownOrUnlocked = 0;
        foreach (var e in entries)
        {
            if (!TabMatches(e)) continue;
            RenderTile(e);
            knownOrUnlocked++;
        }

        // 未知占位（仅「全部」页展示，用分母补足）
        int unknownCount = 0;
        if (currentTab == "all")
        {
            int total = CardArchiveStore.ValidCardTotal;
            unknownCount = Mathf.Max(0, total - entries.Count);
            for (int i = 0; i < unknownCount; i++) RenderUnknown();
        }

        int unlocked = CardArchiveStore.UnlockedCount();
        int totalAll = CardArchiveStore.ValidCardTotal;
        progressText.text = $"已解锁 {unlocked} / 总计 {totalAll}";
        statusText.text = currentTab == "all"
            ? $"本页：已知/已解锁 {knownOrUnlocked} 张，未解锁 {unknownCount} 张"
            : $"本页：{knownOrUnlocked} 张";
    }

    bool TabMatches(CardArchiveEntry e)
    {
        if (currentTab == "all") return true;
        if (currentTab == "Universal") return e.sin == "Universal" || string.IsNullOrEmpty(e.sin);
        return e.sin == currentTab;
    }

    void RenderTile(CardArchiveEntry e)
    {
        var go = new GameObject("Card_" + e.cardId);
        go.transform.SetParent(contentRoot);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(190, 130);
        var bg = go.AddComponent<Image>();
        Color bgColor = e.state == 2 ? new Color(0.16f, 0.22f, 0.16f)
                : e.state == 1 ? new Color(0.18f, 0.18f, 0.22f)
                : new Color(0.12f, 0.12f, 0.14f);
        bg.color = bgColor;

        string title = e.state == 0 ? "？？？" : (e.cardName ?? "？？？");
        var titleTxt = AddText(go.transform, title, 15, new Vector2(0, 46), TextAlignmentOptions.Center, 180);
        titleTxt.color = e.state == 0 ? new Color(0.5f, 0.5f, 0.5f) : Color.white;

        string desc = e.state == 0 ? "未解锁" : (e.state == 1 ? "已遇见 · 未获得" : "已获得");
        var descTxt = AddText(go.transform, desc, 12, new Vector2(0, 14), TextAlignmentOptions.Center, 180);
        descTxt.color = new Color(0.75f, 0.75f, 0.75f);

        if (e.state == 2)
        {
            var meta = AddText(go.transform,
                $"×{e.selectedCount}  {UnixToDate(e.firstUnlockedAtUnix)}", 10,
                new Vector2(0, -40), TextAlignmentOptions.Center, 180);
            meta.color = new Color(0.6f, 0.9f, 0.6f);
            if (e.isNewUnread)
            {
                var badge = AddText(go.transform, "NEW", 12, new Vector2(70, 52), TextAlignmentOptions.Center, 40);
                badge.color = new Color(1f, 0.8f, 0.3f);
            }
        }
    }

    void RenderUnknown()
    {
        var go = new GameObject("Unknown");
        go.transform.SetParent(contentRoot);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(190, 130);
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.12f);
        AddText(go.transform, "？？？", 15, new Vector2(0, 0), TextAlignmentOptions.Center, 180).color = new Color(0.4f, 0.4f, 0.4f);
    }

    // 避免外部依赖 CardArchiveEntry 的常量
    static int CardArchiveEntryState(CardArchiveEntry e) => e.state;

    static string UnixToDate(long u)
    {
        if (u <= 0) return "--";
        var dt = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)
            .AddSeconds(u).ToLocalTime();
        return dt.ToString("yyyy-MM-dd");
    }

    TextMeshProUGUI AddText(Transform parent, string txt, int size, Vector2 pos, TextAlignmentOptions align, float width)
    {
        var go = new GameObject("Txt");
        go.transform.SetParent(parent);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, 40);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = txt;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        return t;
    }
}
