using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 卡牌图鉴（局外系统 §4）数据层。
/// 三态：Unknown(仅剪影) / Known(卡面+名+效果) / Unlocked(标记+时间+次数)。
/// 字段对齐需求：firstSeenAt/firstUnlockedAt/selectedCount/isNewUnread/lastSeenRunId。
/// 持久化走 MetaProfileStore（长期 Profile，与 Run 存档分离）。
/// </summary>
public static class CardArchiveStore
{
    public const int Unknown = 0;
    public const int Known = 1;
    public const int Unlocked = 2;

    static Dictionary<string, CardArchiveEntry> _map;

    static Dictionary<string, CardArchiveEntry> Map
    {
        get { if (_map == null) Reload(); return _map; }
    }

    static void Reload()
    {
        _map = new Dictionary<string, CardArchiveEntry>();
        foreach (var e in MetaProfileStore.CardArchive)
            if (e != null && !string.IsNullOrEmpty(e.cardId)) _map[e.cardId] = e;
    }

    static void Persist()
    {
        MetaProfileStore.CardArchive.Clear();
        foreach (var e in _map.Values) MetaProfileStore.CardArchive.Add(e);
        MetaProfileStore.Save();
    }

    static string CurrentRunId() => RunSession.Instance != null ? RunSession.Instance.RunId : null;
    static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>记录「已知」（出现在选卡候选中，尚未解锁）。首次出现写入展示元数据（名称/描述/宗罪）。</summary>
    public static void RecordSeen(string effectId)
    {
        if (string.IsNullOrEmpty(effectId)) return;
        var e = GetOrCreate(effectId);
        if (e.state == Unknown) e.state = Known;
        if (e.firstSeenAtUnix == 0) e.firstSeenAtUnix = NowUnix();
        e.lastSeenRunId = CurrentRunId();
        ApplyCardMeta(e, effectId);
        Persist();
    }

    /// <summary>记录「已解锁」（选卡确认）。状态升为 Unlocked、首次解锁时间、次数+1、标记新解锁。</summary>
    public static void RecordUnlocked(string effectId)
    {
        if (string.IsNullOrEmpty(effectId)) return;
        var e = GetOrCreate(effectId);
        if (e.state != Unlocked)
        {
            e.state = Unlocked;
            if (e.firstSeenAtUnix == 0) e.firstSeenAtUnix = NowUnix();
            if (e.firstUnlockedAtUnix == 0) e.firstUnlockedAtUnix = NowUnix();
            e.isNewUnread = true;
        }
        e.selectedCount++;
        e.lastSeenRunId = CurrentRunId();
        ApplyCardMeta(e, effectId);
        Persist();
    }

    /// <summary>从 CardManager（Run 内可用）补全展示元数据到 entry，使主菜单无场景时也能渲染。</summary>
    static void ApplyCardMeta(CardArchiveEntry e, string effectId)
    {
        var cm = CardManager.Instance;
        if (cm == null) return;
        var data = cm.FindCard(effectId);
        if (data == null) return;
        e.cardName = data.cardName;
        e.description = data.description;
        e.sin = data.monsterType == SinType.None ? "Universal" : data.monsterType.ToString();
    }

    static CardArchiveEntry GetOrCreate(string id)
    {
        if (!Map.TryGetValue(id, out var e))
        {
            e = new CardArchiveEntry { cardId = id, state = Unknown };
            Map[id] = e;
        }
        return e;
    }

    public static CardArchiveEntry GetEntry(string id) => Map.TryGetValue(id, out var e) ? e : null;
    public static bool IsUnlocked(string id) => Map.TryGetValue(id, out var e) && e.state == Unlocked;
    public static bool IsKnown(string id) => Map.TryGetValue(id, out var e) && e.state >= Known;
    public static int StateOf(string id) => Map.TryGetValue(id, out var e) ? e.state : Unknown;

    /// <summary>查看后清除新解锁标记。</summary>
    public static void MarkRead(string id)
    {
        if (Map.TryGetValue(id, out var e) && e.isNewUnread)
        {
            e.isNewUnread = false;
            Persist();
        }
    }

    public static void MarkAllRead()
    {
        bool changed = false;
        foreach (var e in Map.Values)
            if (e.isNewUnread) { e.isNewUnread = false; changed = true; }
        if (changed) Persist();
    }

    public static List<CardArchiveEntry> AllEntries() => new List<CardArchiveEntry>(Map.Values);

    // ── 进度分母：当前有效 Card 总数（排除禁用/删除）。
    // Run 内由 RefreshValidTotal 固化到 meta（持久化，主菜单无场景也能读取）。──
    public static int ValidCardTotal => MetaProfileStore.ValidCardTotal;

    public static void RefreshValidTotal()
    {
        int t = ComputeValidTotal();
        if (t > 0) MetaProfileStore.ValidCardTotal = t;
    }

    static int ComputeValidTotal()
    {
        var cm = CardManager.Instance;
        if (cm == null || cm.cardLibrary == null || cm.cardLibrary.cards == null) return 0;
        int n = 0;
        foreach (var c in cm.cardLibrary.cards)
            if (c != null && cm.cardLibrary.IsEffectEnabled(c.effectId)) n++;
        return n;
    }

    public static int UnlockedCount()
    {
        int n = 0;
        foreach (var e in Map.Values) if (e.state == Unlocked) n++;
        return n;
    }
}

/// <summary>卡牌图鉴条目（长期持久化）。展示元数据在 Run 内补全，供主菜单无场景渲染。</summary>
[Serializable]
public class CardArchiveEntry
{
    public string cardId;
    public int state;                // 0 Unknown / 1 Known / 2 Unlocked
    public long firstSeenAtUnix;
    public long firstUnlockedAtUnix;
    public int selectedCount;
    public bool isNewUnread;
    public string lastSeenRunId;
    // 展示元数据（Run 内补全）
    public string cardName;
    public string description;
    public string sin;               // SinType 名，通用=Universal
}

/// <summary>
/// 运行时采集器：订阅 CardManager 事件，将「已知/已解锁」写入 CardArchiveStore。
/// 与面板解耦——即使图鉴面板未打开，对局内的选卡也会持续记录（端到端持久化）。
/// </summary>
public class CardArchiveTracker : MonoBehaviour
{
    static CardArchiveTracker instance;

    /// <summary>挂载到 GameManager（随常驻对象 DDOL；已挂则复用），与 CardFaceBrowser 同模式。
    /// 不使用 RuntimeInitializeOnLoadMethod 自建：项目启用了 Enter Play Mode 无域重载，
    /// 自建+静态 instance 存在悬空不重建的隐患（曾致 CardFaceBrowser 失效）。</summary>
    public static CardArchiveTracker EnsureOnGameManager()
    {
        var gm = GameManager.Instance;
        if (gm == null) return null;
        var existing = gm.GetComponent<CardArchiveTracker>();
        return existing != null ? existing : gm.gameObject.AddComponent<CardArchiveTracker>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void OnEnable()
    {
        CardManager.OnCardOffered += OnOffered;
        CardManager.OnEffectUnlocked += OnUnlocked;
        CardManager.OnCardRerolled += OnRerolled;
    }

    void OnDisable()
    {
        CardManager.OnCardOffered -= OnOffered;
        CardManager.OnEffectUnlocked -= OnUnlocked;
        CardManager.OnCardRerolled -= OnRerolled;
    }

    void OnOffered()
    {
        CardArchiveStore.RefreshValidTotal();
        MarkCurrentPicksSeen();
    }

    void OnRerolled()
    {
        MarkCurrentPicksSeen();
    }

    void OnUnlocked(CardData card)
    {
        if (card != null) CardArchiveStore.RecordUnlocked(card.effectId);
    }

    void MarkCurrentPicksSeen()
    {
        var cm = CardManager.Instance;
        if (cm == null || cm.currentPicks == null) return;
        foreach (var c in cm.currentPicks)
            if (c != null) CardArchiveStore.RecordSeen(c.effectId);
    }
}
