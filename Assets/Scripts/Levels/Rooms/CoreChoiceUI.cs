using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// 升级选卡弹窗。触发方调 Show(onClosed, doublePick) 打开，UI 不依赖触发方类型。
/// 弹窗期间游戏暂停（timeScale=0）+ 屏蔽玩家输入；界面可隐藏查看场景（仍暂停），continue 按钮切换显隐。
/// 选卡会话进行中由 IsDrafting 标识（与面板可见性 IsOpen 分离：隐藏界面仍算会话进行中）。
/// </summary>
public class CoreChoiceUI : MonoBehaviour
{
    public static CoreChoiceUI Instance { get; private set; }

    [Header("UI Root")]
    public GameObject panelRoot;

    [Header("Card Template")]
    [Tooltip("A prefab with ChoiceCard + Text + Image + Confirm/Reroll buttons + marks. Instantiated N times.")]
    public GameObject cardPrefab;
    [Tooltip("How many cards to show each time.")]
    public int cardCount = 3;

    [Header("Card Parent")]
    [Tooltip("Parent transform with HorizontalLayoutGroup where cards are spawned.")]
    public RectTransform cardParent;

    [Header("Global")]
    public Button confirmAllButton;
    public TextMeshProUGUI titleText;

    // State
    private CoreChoiceCard[] cards;
    private int selectedIndex = -1;
    private int picksRemaining = 1;
    private bool doublePick;
    private Action onClosed;
    private float timeScaleBeforeOpen = 1f;
    private bool _isDrafting;

    /// <summary>面板当前是否可见（隐藏查看场景时 = false）。</summary>
    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    /// <summary>选卡会话是否进行中（Show 置 true，Close 置 false；与面板可见性无关）。</summary>
    public bool IsDrafting => _isDrafting;

    /// <summary>
    /// 调整选卡界面 Canvas 的渲染层级。
    /// 暂停时降到负值（让暂停菜单在上层），退出暂停恢复原值。
    /// </summary>
    public void SetCanvasSortingOrder(int order)
    {
        var canvas = GetComponent<Canvas>();
        if (canvas != null) canvas.sortingOrder = order;
    }

    void Awake()
    {
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
        // continue 按钮随弹窗初始隐藏（在 panelRoot 外，显示时见 Show）
        if (confirmAllButton != null)
        {
            confirmAllButton.onClick.AddListener(OnConfirmAll);
            confirmAllButton.interactable = true;
            confirmAllButton.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 打开选卡弹窗。
    /// </summary>
    /// <param name="onClosed">弹窗关闭回调（触发方注入房间流程等；null 则仅关闭）。</param>
    /// <param name="doublePick">true=双选（可选 2 张），false=单选。</param>
    /// <param name="keepPicks">true=保留 CardManager.currentPicks（读档补弹用，不重新随机抽卡）。</param>
    /// <param name="waveIndex">波次号（种子确定性：本波抽卡序列由 种子+波次 派生，同种子可复现）。</param>
    public void Show(Action onClosed = null, bool doublePick = false, bool keepPicks = false, int waveIndex = -1)
    {
        if (_isDrafting) return;   // 会话进行中忽略重复打开
        _isDrafting = true;

        this.onClosed = onClosed;
        this.doublePick = doublePick;
        this.picksRemaining = doublePick ? 2 : 1;

        // 暂停特判：记录弹窗打开前 timeScale，关闭时恢复
        timeScaleBeforeOpen = Time.timeScale;

        // Clear old cards
        if (cardParent != null)
        {
            for (int i = cardParent.childCount - 1; i >= 0; i--)
                Destroy(cardParent.GetChild(i).gameObject);
        }

        // 种子确定性：本波弹卡前固定卡牌随机流（种子+波次号派生），同一种子整局卡牌可复现
        if (CardManager.Instance != null && waveIndex >= 0)
            CardManager.Instance.PrepareCardSession(waveIndex);

        // Draw random cards from CardManager（读档补弹时保留已有候选，不重新随机）
        if (CardManager.Instance != null)
        {
            if (keepPicks && CardManager.Instance.currentPicks != null
                && CardManager.Instance.currentPicks.Length > 0
                && CardManager.Instance.currentPicks[0] != null)
            {
                // 已有候选：保留（读档补弹，候选与退出时一致）
            }
            else
            {
                CardManager.Instance.DrawCards(cardCount);
            }
        }
        else
            Debug.LogWarning("[CoreChoiceUI] CardManager.Instance is null — no cards will be shown. Add CardManager to the scene.");

        RefreshCards();

        Debug.Log($"[CoreChoiceUI] Show called: doublePick={doublePick}, panelRoot={(panelRoot != null ? panelRoot.name : "NULL")}, cardPrefab={(cardPrefab != null ? cardPrefab.name : "NULL")}, cardParent={(cardParent != null ? cardParent.name : "NULL")}");
        if (panelRoot != null) panelRoot.SetActive(true);
        else Debug.LogError("[CoreChoiceUI] panelRoot is NULL — drag the UI Panel into this field!");

        // continue 按钮在 panelRoot 外，始终显示（供 toggle 显隐选卡界面）
        if (confirmAllButton != null)
        {
            confirmAllButton.gameObject.SetActive(true);
            confirmAllButton.onClick.RemoveAllListeners();
            confirmAllButton.onClick.AddListener(OnConfirmAll);
            confirmAllButton.interactable = true;
        }

        // 暂停特判：弹窗期间暂停 + 屏蔽玩家输入（不做全局时间仲裁）
        Time.timeScale = 0f;
        PlayerController.SetGameplayInputBlocked(true, "CoreChoiceUI");
    }

    /// <summary>根据 CardManager.currentPicks 重建卡片 UI。</summary>
    private void RefreshCards()
    {
        if (cardParent != null)
        {
            for (int i = cardParent.childCount - 1; i >= 0; i--)
                Destroy(cardParent.GetChild(i).gameObject);
        }

        var picks = CardManager.Instance != null ? CardManager.Instance.currentPicks : null;
        cards = new CoreChoiceCard[cardCount];
        for (int i = 0; i < cardCount; i++)
        {
            string name = "Option " + (char)('A' + i);
            string desc = "";
            Sprite sprite = null;
            if (picks != null && i < picks.Length && picks[i] != null)
            {
                name = picks[i].cardName;
                desc = picks[i].description ?? "";
                sprite = picks[i].image;
            }

            var go = cardPrefab != null ? Instantiate(cardPrefab, cardParent) : null;
            if (go == null) continue;
            var card = go.GetComponent<CoreChoiceCard>();
            if (card == null) card = go.AddComponent<CoreChoiceCard>();
            card.Init(i, name, sprite, desc, OnCardConfirm, OnCardReroll);
            cards[i] = card;
        }
    }

    void OnCardConfirm(int index)
    {
        if (cards == null || index < 0 || index >= cards.Length)
        {
            Debug.LogWarning($"[CoreChoiceUI] Select rejected: invalid index={index}, cards={(cards != null ? cards.Length : 0)}");
            return;
        }
        if (cards[index] == null)
        {
            Debug.LogWarning($"[CoreChoiceUI] Select rejected: card instance is null at index={index}");
            return;
        }

        // 点卡即选即生效（双选中更符合直觉）；解锁该卡
        if (CardManager.Instance != null)
            CardManager.Instance.SelectCard(index);

        picksRemaining--;
        if (picksRemaining > 0 && doublePick)
        {
            // 双选第二轮：保留会话排除（第一轮已出现/已选的卡不再出现）
            if (CardManager.Instance != null)
                CardManager.Instance.DrawCards(cardCount, keepSession: true);
            RefreshCards();
            selectedIndex = -1;
        }
        else
        {
            Close();
        }
    }

    void OnCardReroll(int index)
    {
        if (CardManager.Instance == null)
        {
            Debug.LogWarning("[CoreChoiceUI] Reroll rejected: CardManager.Instance is null");
            return;
        }
        if (cards == null || index < 0 || index >= cards.Length)
        {
            Debug.LogWarning($"[CoreChoiceUI] Reroll rejected: invalid index={index}, cards={(cards != null ? cards.Length : 0)}");
            return;
        }
        if (cards[index] == null)
        {
            Debug.LogWarning($"[CoreChoiceUI] Reroll rejected: card instance is null at index={index}");
            return;
        }

        var newCard = CardManager.Instance.DrawOneReroll(index);
        if (newCard != null)
        {
            Sprite sprite = null;
            if (newCard.image != null) sprite = newCard.image;
            cards[index].Replace(newCard.cardName, sprite, newCard.description ?? "");
            // currentPicks[index] 已由 DrawOneReroll 内部更新，UI 不再直写
            if (selectedIndex == index) selectedIndex = -1;
            Debug.Log($"[CoreChoiceUI] Card rerolled: index={index}, name={newCard.cardName}");
        }
        else
        {
            Debug.LogWarning($"[CoreChoiceUI] Reroll produced no card: index={index}");
        }
    }

    /// <summary>
    /// 下方按钮：切换选卡界面显隐（隐藏看场景，仍暂停；再点又弹出）。
    /// 注意：按钮在 panelRoot 外（UIChoicePopupCanvas 下），隐藏面板时按钮仍可见。
    /// </summary>
    void OnConfirmAll()
    {
        Debug.Log("[CoreChoiceUI] Toggle panel visibility");
        if (panelRoot != null)
        {
            panelRoot.SetActive(!panelRoot.activeSelf);
            // 隐藏面板看场景，游戏仍保持暂停（不恢复 timeScale）
        }
    }

    private void Close()
    {
        _isDrafting = false;
        if (panelRoot != null) panelRoot.SetActive(false);
        // continue 按钮随弹窗关闭隐藏
        if (confirmAllButton != null) confirmAllButton.gameObject.SetActive(false);
        // 恢复弹窗打开前的 timeScale + 恢复玩家输入
        Time.timeScale = timeScaleBeforeOpen;
        PlayerController.SetGameplayInputBlocked(false, "CoreChoiceUI");

        // 触发方回调（房间流程等）；先摘抄后清空，避免回调内重入 Show
        var cb = onClosed;
        onClosed = null;
        cb?.Invoke();
    }

    /// <summary>关闭选卡弹窗（跳过剩余选择直接关闭，不推进房间流程的调用方自行处理）。</summary>
    public void CloseChoiceUI()
    {
        if (!_isDrafting) return;
        Close();
    }
}
