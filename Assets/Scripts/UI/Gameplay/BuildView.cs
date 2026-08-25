using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 构筑界面（Build View）：游戏内暂停后查看卡组的统一面板。
/// 根据当前附身状态三态切换显示内容：
///   A. 灵魂态（未附身）→ 玩家本局全部已解锁卡（完整构筑）
///   B. 附身精英怪      → 精英怪自身的构筑（历史 BD 快照）
///   C. 附身普通怪      → 玩家已解锁、针对该怪物类型的卡
/// 卡片复用 CoreChoiceCard 预制体渲染为只读模式，经 CardArcLayout 弧形排布。
///
/// 构筑按钮为场景内静态对象：设计者在编辑器里把任意 Button（可带自定义图标/样式）摆到 UICanvas 上，并在本组件的 buildButton 字段指给它即可。面板本身仍由代码生成。
/// </summary>
public class BuildView : MonoBehaviour
{
    [Header("Card Source（缺省回退到 CoreChoiceUI.cardPrefab）")]
    [SerializeField] GameObject cardPrefab;

    [Header("Build Button（场景中静态摆放，由设计者调整样式/图标）")]
    [Tooltip("场景中已摆放好的构筑按钮（例如放在暂停键附近、带自定义图标）。点击即打开构筑界面。留空则 UIManager 回退为运行期自动创建一个纯文本按钮。")]
    public Button buildButton;

    [Header("Arc Layout（透传给 CardArcLayout）")]
    public float radius = 1000f;
    public float maxSpreadDeg = 100f;
    public float perCardDeg = 16f;
    public float baseYOffset = 360f;

    // 运行期构建的 UI（面板本身由代码生成，仅按钮改为场景静态对象）
    GameObject panelRoot;
    RectTransform cardParent;
    TMP_Text titleText;
    TMP_Text emptyHint;
    Button closeButton;
    CardArcLayout layout;

    readonly List<GameObject> cardInstances = new List<GameObject>();
    bool isOpen = false;

    public void Initialize()
    {
        BuildPanel();
        if (buildButton == null)
        {
            // 场景静态按钮：从 Canvas 根递归按名查找设计者摆放的 BuildButton
            // （BuildView 挂在 UIManager 下，而 BuildButton 在 UICanvas 下，故需向上找到 Canvas 再搜索）。
            var root = GetComponentInParent<Canvas>()?.transform ?? transform.root;
            var btnGO = root != null ? FindDescendant(root, "BuildButton") : null;
            if (btnGO != null) buildButton = btnGO.GetComponent<Button>();
        }
        if (buildButton != null)
        {
            // 避免重复绑定（Initialize 可能被多次调用）
            buildButton.onClick.RemoveListener(Show);
            buildButton.onClick.AddListener(Show);
        }
        else
        {
            Debug.LogWarning("[BuildView] buildButton 未找到：请在 UICanvas 下放置名为 BuildButton 的按钮（可带自定义图标）。");
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

    void BuildPanel()
    {
        panelRoot = new GameObject("BuildPanel", typeof(RectTransform), typeof(Image));
        panelRoot.transform.SetParent(transform, false);
        var prt = panelRoot.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = prt.offsetMax = Vector2.zero;
        var bg = panelRoot.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.65f);
        bg.raycastTarget = true; // 拦截点击，避免穿透到世界
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

        // 卡片容器（屏幕底部居中）
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
        layout.scaleMultiplier = 1.5f; // 卡片整体放大到原始基准的 1.5 倍

        // 关闭按钮（返回）
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
        closeButton.onClick.AddListener(Hide);
    }

    public void Show()
    {
        if (isOpen) return;
        isOpen = true;
        if (buildButton != null) buildButton.gameObject.SetActive(false);

        // 必须先激活面板再布局：卡片 RectTransform 在 inactive 状态下 rect 不更新，
        // 会导致 CardArcLayout 实测不到真实尺寸、收缩失效（两侧卡溢出屏外）。
        panelRoot.SetActive(true);
        Populate();

        TimeScaleManager.Push(TimeDomain.Pause, 0f);
        PlayerController.SetGameplayInputBlocked(true, "BuildView");
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Hide()
    {
        if (!isOpen) return;
        isOpen = false;
        panelRoot.SetActive(false);
        ClearCards();
        if (buildButton != null) buildButton.gameObject.SetActive(true);

        TimeScaleManager.Pop(TimeDomain.Pause);
        PlayerController.SetGameplayInputBlocked(false, "BuildView");
    }

    void ClearCards()
    {
        foreach (var go in cardInstances) Destroy(go);
        cardInstances.Clear();
    }

    void Populate()
    {
        ClearCards();
        var result = Gather();
        titleText.text = result.title;
        emptyHint.text = result.hint;

        bool has = result.cards.Count > 0;
        emptyHint.gameObject.SetActive(!has);
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
            // 卡面定位点必须居中（0.5,0.5），否则 localPosition 不是卡中心，弧形排布/安全边距会偏移
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
            // 只读模式：隐藏确认/重抽按钮（回调已传 null，点击安全）
            if (card.confirmButton != null) card.confirmButton.gameObject.SetActive(false);
            if (card.rerollButton != null) card.rerollButton.gameObject.SetActive(false);
            // 仅显示卡片本身（卡面立绘）：隐藏选卡界面带的名称与描述文本
            if (card.cardText != null) card.cardText.gameObject.SetActive(false);
            if (card.descriptionText != null) card.descriptionText.gameObject.SetActive(false);
            // 移除选卡界面的 ChoiceCard：它的 LateUpdate 每帧把 localScale lerp 回 1.0，
            // 会覆盖 CardArcLayout 布局设置的收缩 scale，导致卡片以全尺寸（立绘 500×800）摆放而溢出屏幕。
            // 构筑界面为只读展示，悬停置顶/放大由下方 AddHoverToFront 负责。
            var choiceCard = go.GetComponent<ChoiceCard>();
            if (choiceCard != null) Destroy(choiceCard);
            AddHoverToFront(go);
            cardInstances.Add(go);
        }
        layout.Rebuild(cardInstances);
    }

    /// <summary>鼠标悬停某卡时把它置顶（移到兄弟最后，渲染在最前）并轻微放大，避免被堆叠的其它卡遮挡。</summary>
    void AddHoverToFront(GameObject go)
    {
        // 关闭卡面自身的点击交互，避免与置顶逻辑冲突（只读展示用）
        var ct = go.AddComponent<EventTrigger>();
        // 基准 scale 取布局后的值（CardArcLayout 设置的收缩比例），悬停放大/移出精确恢复，
        // 避免旧实现的"乘/除 1.12"浮点漂移破坏弧形布局。
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

    GameObject ResolvePrefab()
    {
        if (cardPrefab != null) return cardPrefab;
        if (CoreChoiceUI.Instance != null) return CoreChoiceUI.Instance.cardPrefab;
        return null;
    }

    struct GatherResult { public List<CardData> cards; public string title; public string hint; }

    GatherResult Gather()
    {
        var list = new List<CardData>();
        string title = "当前构筑";
        string hint = "尚未获得任何卡片";

        var body = (PossessionManager.Instance != null) ? PossessionManager.Instance.CurrentBody : null;
        if (body == null)
        {
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
}
