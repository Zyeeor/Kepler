#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 已获得卡片能力面板（OnGUI，屏幕右侧，F2 切换）：
/// 显示本局已解锁的卡牌能力（卡名 + effectId + 描述），按卡库顺序排列（稳定）。
/// 与 MapDebugHUD 共用 F2 键：调试面板一键开关（地图信息 + 卡片进度）。
/// 低频刷新缓存（refreshInterval），避免每帧遍历卡库；运行时自动确保实例
/// （场景加载后创建，同 MapDebugHUD 模式：主菜单 → 对局不会失效）。
/// </summary>
public class CardProgressPanel : MonoBehaviour
{
    [Tooltip("是否显示面板（F2 切换）。")]
    public bool showPanel = false;
    [Tooltip("切换快捷键（与 MapDebugHUD 一致）。")]
    public KeyCode toggleKey = KeyCode.F2;
    [Tooltip("缓存刷新间隔（秒），避免每帧遍历卡库。")]
    [Min(0.05f)] public float refreshInterval = 0.25f;

    static CardProgressPanel instance;

    // ── 低频缓存的已解锁卡列表（按卡库顺序，含卡名/effectId/描述） ──
    readonly List<CardData> unlockedCards = new List<CardData>();
    float nextRefreshTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureInstance()
    {
        EnsureInScene();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadedEnsure;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadedEnsure;
    }

    static void OnSceneLoadedEnsure(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        EnsureInScene();
    }

    static void EnsureInScene()
    {
        if (instance == null)
            new GameObject(nameof(CardProgressPanel)).AddComponent<CardProgressPanel>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (GameManager.IsFormalFlow) return; // 正式流程屏蔽调试面板
        if (Input.GetKeyDown(toggleKey))
            showPanel = !showPanel;
        if (!showPanel || !Application.isPlaying) return;
        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + refreshInterval;
            RefreshCards();
        }
    }

    /// <summary>按卡库顺序收集已解锁卡（UnlockedEffects 是 HashSet 无序，需按卡库稳定排序）。</summary>
    void RefreshCards()
    {
        unlockedCards.Clear();
        var cm = CardManager.Instance;
        if (cm == null || cm.cardLibrary == null || cm.cardLibrary.cards == null) return;
        foreach (var card in cm.cardLibrary.cards)
        {
            if (card == null || string.IsNullOrEmpty(card.effectId)) continue;
            if (cm.IsEffectUnlocked(card.effectId)) unlockedCards.Add(card);
        }
    }

    void OnGUI()
    {
        if (!showPanel || !Application.isPlaying || GameManager.IsFormalFlow) return; // 正式流程屏蔽

        const float w = 360f;
        const float lineH = 18f;
        int cardCount = unlockedCards.Count;
        float descLines = 0f;
        for (int i = 0; i < cardCount; i++)
        {
            string desc = unlockedCards[i].description;
            if (!string.IsNullOrEmpty(desc)) descLines += 2f; // 描述占两行
        }
        float height = 38f + cardCount * lineH + descLines * lineH + 14f;
        float x = Screen.width - w - 10f;
        float y = (Screen.height - height) * 0.5f; // 右侧垂直居中

        GUI.Box(new Rect(x, y, w, height), $"已获得卡片能力（F2） {cardCount} 张");
        y += lineH + 4f;

        if (cardCount == 0)
        {
            GUI.Label(new Rect(x + 8f, y, w - 16f, lineH), "（暂无，波次结束后选卡解锁）");
            return;
        }

        for (int i = 0; i < cardCount; i++)
        {
            CardData card = unlockedCards[i];
            GUI.Label(new Rect(x + 8f, y, w - 16f, lineH), $"{card.cardName}  [{card.effectId}]");
            y += lineH;
            if (!string.IsNullOrEmpty(card.description))
            {
                GUI.Label(new Rect(x + 16f, y, w - 32f, lineH * 2f), card.description);
                y += lineH * 2f;
            }
        }
    }
}
#endif
