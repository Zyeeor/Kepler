#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// UI 卡面浏览器（Debug，F3 切换）：浏览 CardLibrary 全池每张卡的卡面（多层素材叠画 + 名称/描述）。
/// 选卡弹窗（CoreChoiceUI）打开期间，可将选中卡替换选卡界面的第一张候选（点选即生效）。
/// OnGUI 面板遵循项目 Debug 惯例：正式流程（GameManager.IsFormalFlow）屏蔽；
/// 运行时自动确保实例（场景加载后创建，主菜单→对局不失效），与 CardProgressPanel 同模式。
/// </summary>
public class CardFaceBrowser : MonoBehaviour
{
    [Tooltip("浏览器总开关（Inspector 可配；关闭后热键与面板均不工作）。")]
    public bool enableBrowser = true;
    [Tooltip("是否显示面板（F3 切换）。")]
    public bool showPanel = false;
    [Tooltip("切换快捷键。")]
    public KeyCode toggleKey = KeyCode.F3;
    [Tooltip("预览卡缩放（仅当无法取得选卡界面 cardParent 缩放时使用；正常情况自动继承 cardParent 缩放，与正式选卡完全一致）。")]
    [Range(0.2f, 2f)] public float previewScale = 0.7f;
    [Tooltip("预览卡相对屏幕中心的垂直偏移（像素，GUI 坐标向下为正）。")]
    public float previewOffsetY = 0f;

    static CardFaceBrowser instance;

    // ── 卡池缓存（低频刷新）──
    readonly List<CardData> pool = new List<CardData>();
    float nextRefreshTime;

    // ── UI 状态 ──
    Vector2 scrollPos;
    string filter = "";
    int selectedIndex = -1;

    // ── UGUI 预览卡（克隆 CoreChoiceUI 的真实卡片模板 → 100% 还原选卡界面多层视差/悬停效果）──
    GameObject previewCardGo;
    RectTransform previewRect;
    CoreChoiceCard previewCC;   // 复用克隆卡的多层素材应用逻辑（enabled=false 禁用交互回调）
    TextMeshProUGUI previewTitle, previewDesc;
    int previewCardIndex = -1;   // 预览卡当前展示的池索引（-1 = 无）

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
            new GameObject(nameof(CardFaceBrowser)).AddComponent<CardFaceBrowser>();
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
        DestroyPreviewCard();
        if (instance == this) instance = null;
    }

    void Update()
    {
        // 与项目其他调试组件一致：正式流程/开关关闭时完全禁用（含收起预览卡）
        if (GameManager.IsFormalFlow || !enableBrowser)
        {
            DestroyPreviewCard();
            return;
        }
        if (Input.GetKeyDown(toggleKey))
            showPanel = !showPanel;
    }

    /// <summary>按卡库顺序收集全池卡（低频刷新）。</summary>
    void RefreshPool()
    {
        pool.Clear();
        var cm = CardManager.Instance;
        if (cm == null || cm.cardLibrary == null || cm.cardLibrary.cards == null) return;
        foreach (var card in cm.cardLibrary.cards)
        {
            if (card == null || string.IsNullOrEmpty(card.effectId)) continue;
            pool.Add(card);
        }
    }

    void OnGUI()
    {
        if (!showPanel || !Application.isPlaying || GameManager.IsFormalFlow || !enableBrowser)
        {
            DestroyPreviewCard(); // 面板隐藏/不可用时收起动态预览卡
            return;
        }

        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + 0.5f;
            RefreshPool();
        }

        // ── 左侧窄面板：只保留列表（贴屏幕左缘）──
        float w = 320f;
        float h = Mathf.Min(Screen.height * 0.8f, 620f);
        float x = 12f;
        float y = (Screen.height - h) * 0.5f;
        GUI.Box(new Rect(x, y, w, h), $"卡面浏览器（F3）· 全池 {pool.Count} 张");

        const float listW = 296f;
        const float lineH = 22f;
        const float pad = 8f;

        // 过滤
        y += lineH + 4f;
        GUI.Label(new Rect(x + pad, y, 40f, lineH), "过滤:");
        filter = GUI.TextField(new Rect(x + 48f, y, listW - 56f, lineH), filter);

        // 列表（底部预留选中卡信息 + 替换按钮区）
        y += lineH + 4f;
        float bottomH = 84f;
        float listH = h - (lineH * 2 + 26f + bottomH);
        var viewRect = new Rect(0f, 0f, listW - 24f, pool.Count * lineH);
        scrollPos = GUI.BeginScrollView(new Rect(x + pad, y, listW, listH), scrollPos, viewRect);

        string f = filter.Trim().ToLowerInvariant();
        var cm = CardManager.Instance;
        int drawIndex = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            var card = pool[i];
            bool match = string.IsNullOrEmpty(f)
                || (card.cardName != null && card.cardName.ToLowerInvariant().Contains(f))
                || (card.effectId != null && card.effectId.ToLowerInvariant().Contains(f));
            if (!match) continue;

            string unlockedMark = cm != null && cm.IsEffectUnlocked(card.effectId) ? " ✓" : "";
            string label = $"{card.cardName}  [{card.category}/{card.monsterType}]{unlockedMark}";
            var rowRect = new Rect(0f, drawIndex * lineH, viewRect.width, lineH);
            if (GUI.Button(rowRect, label))
                selectedIndex = i;
            drawIndex++;
        }
        GUI.EndScrollView();

        // ── 面板底部：选中卡信息 + 替换按钮 ──
        float bottomY = y + listH + 8f;
        if (selectedIndex >= 0 && selectedIndex < pool.Count)
        {
            var card = pool[selectedIndex];
            GUI.Label(new Rect(x + pad, bottomY, listW - 16f, lineH), card.cardName, BoldStyle());
            GUI.Label(new Rect(x + pad, bottomY + lineH, listW - 16f, lineH), $"{card.effectId} · {card.category}/{card.monsterType}");

            bool drafting = CoreChoiceUI.Instance != null && CoreChoiceUI.Instance.IsDrafting;
            GUI.enabled = drafting;
            if (GUI.Button(new Rect(x + pad, bottomY + lineH * 2, listW - 16f, 30f),
                drafting ? "替换选卡界面第 1 张（立即生效）" : "（选卡弹窗未开启）"))
            {
                if (cm != null)
                {
                    cm.DebugReplacePick(0, card);
                    CoreChoiceUI.Instance.RefreshCards();
                }
            }
            GUI.enabled = true;
        }

        // ── 预览卡：屏幕中央，大小与正式选卡界面一致（继承 cardParent 缩放 0.7）──
        if (selectedIndex >= 0 && selectedIndex < pool.Count)
        {
            var card = pool[selectedIndex];
            bool hasLivePreview = EnsurePreviewCard(card, selectedIndex);
            PositionPreviewCard(Screen.width * 0.5f, Screen.height * 0.5f);
            if (!hasLivePreview)
            {
                // 无选卡模板回退：屏幕中央静态多层叠画（与正式卡同尺寸 300x600 × 0.7）
                float fw = 300f * previewScale, fh = 600f * previewScale;
                var faceRect = new Rect(Screen.width * 0.5f - fw * 0.5f, Screen.height * 0.5f - fh * 0.5f, fw, fh);
                bool anyLayer = false;
                if (card.backgroundSprite != null) { DrawSprite(card.backgroundSprite, faceRect); anyLayer = true; }
                if (card.extraBackgroundSprites != null) foreach (var s in card.extraBackgroundSprites) if (s != null) { DrawSprite(s, faceRect); anyLayer = true; }
                if (card.middlegroundSprite != null) { DrawSprite(card.middlegroundSprite, faceRect); anyLayer = true; }
                if (card.extraMiddlegroundSprites != null) foreach (var s in card.extraMiddlegroundSprites) if (s != null) { DrawSprite(s, faceRect); anyLayer = true; }
                if (card.foregroundSprite != null) { DrawSprite(card.foregroundSprite, faceRect); anyLayer = true; }
                if (card.extraForegroundSprites != null) foreach (var s in card.extraForegroundSprites) if (s != null) { DrawSprite(s, faceRect); anyLayer = true; }
                if (card.borderSprite != null) { DrawSprite(card.borderSprite, faceRect); anyLayer = true; }
                if (card.extraBorderSprites != null) foreach (var s in card.extraBorderSprites) if (s != null) { DrawSprite(s, faceRect); anyLayer = true; }
                if (!anyLayer) DrawSprite(card.image, faceRect);
            }
        }
        else
        {
            DestroyPreviewCard();
        }
    }

    // ── UGUI 动态预览卡 ──

    /// <summary>
    /// 确保预览卡存在且展示目标卡：克隆 CoreChoiceUI 的 cardPrefab（真实选卡模板），
    /// 移除选择组件与按钮、保留 ChoiceCard（悬停缩放摆动）+ UIParallaxCardTilt（多层视差 tilt），
    /// 应用多层素材 → 与选卡界面看到的效果完全一致。
    /// </summary>
    bool EnsurePreviewCard(CardData card, int poolIndex)
    {
        var ui = CoreChoiceUI.Instance;
        if (ui == null || ui.cardPrefab == null)
        {
            DestroyPreviewCard();
            return false;
        }

        if (previewCardGo == null)
        {
            // 外层定位/缩放容器（与正式界面同构：Midscreen 容器承载缩放，
            // 卡片根的 ChoiceCard 每帧 Lerp 自己的 localScale（1↔hoverScale）不受干扰）
            var wrapper = new GameObject("CardFaceBrowserPreviewRoot", typeof(RectTransform));
            previewRect = wrapper.GetComponent<RectTransform>();
            var canvas = ui.GetComponent<Canvas>() ?? ui.GetComponentInParent<Canvas>();
            previewRect.SetParent(canvas != null ? canvas.transform : ui.transform, false);

            var go = Instantiate(ui.cardPrefab, previewRect);
            go.name = "CardFaceBrowserPreview";
            // 与正式选卡一致：套用 FontRegistry card 槽统一字体（预览所见即所得）
            if (FontRegistry.Instance != null)
                FontRegistry.Instance.ApplyFontToTree(go.transform, FontSlots.Card);
            previewCC = go.GetComponent<CoreChoiceCard>();
            if (previewCC != null)
            {
                previewTitle = previewCC.cardText;
                previewDesc = previewCC.descriptionText;
                if (previewCC.confirmButton != null) previewCC.confirmButton.gameObject.SetActive(false);
                if (previewCC.rerollButton != null) previewCC.rerollButton.gameObject.SetActive(false);
                if (previewCC.confirmedMark != null) previewCC.confirmedMark.SetActive(false);
                if (previewCC.rerolledMark != null) previewCC.rerolledMark.SetActive(false);
                previewCC.enabled = false; // 禁用交互回调，仅复用其多层素材应用逻辑
            }
            previewCardGo = wrapper;
            previewCardIndex = -1;
        }

        // 每帧刷新缩放：正式卡片实际尺寸 = cardParent(Midscreen).lossyScale（含 CanvasScaler scaleFactor）；
        // 预览卡挂 Canvas 根下会再乘一次根 scaleFactor → 除以父级 lossyScale 抵消，保证 world scale 完全一致。
        SyncPreviewScale(ui);

        if (previewCardIndex != poolIndex)
        {
            previewCardIndex = poolIndex;
            if (previewCC != null) previewCC.ApplyLayers(card);   // 复用 CoreChoiceCard 的多层素材应用
            if (previewTitle != null) previewTitle.text = card.cardName;
            if (previewDesc != null) previewDesc.text = card.description;
        }
        return true;
    }

    /// <summary>把预览卡定位到屏幕中心（正式选卡界面中间卡的位置）。</summary>
    void PositionPreviewCard(float screenCenterX, float screenCenterY)
    {
        if (previewRect == null) return;
        Vector2 screenPt = new Vector2(screenCenterX, screenCenterY + previewOffsetY);
        if (previewRect.parent is RectTransform parent)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screenPt, null, out Vector3 world);
            previewRect.position = world;
        }
    }

    /// <summary>预览卡 world scale 与正式选卡卡片完全一致（每帧刷新，动态跟随）。</summary>
    void SyncPreviewScale(CoreChoiceUI ui)
    {
        if (previewRect == null) return;
        Vector3 scale = Vector3.one * previewScale;
        if (ui.cardParent != null && ui.cardParent.lossyScale.magnitude > 0.01f)
        {
            Vector3 parentWorld = previewRect.parent != null && previewRect.parent.lossyScale.magnitude > 0.01f
                ? previewRect.parent.lossyScale : Vector3.one;
            scale = new Vector3(
                ui.cardParent.lossyScale.x / parentWorld.x,
                ui.cardParent.lossyScale.y / parentWorld.y,
                ui.cardParent.lossyScale.z / parentWorld.z);
        }
        previewRect.localScale = scale;
    }

    void DestroyPreviewCard()
    {
        if (previewCardGo != null)
        {
            Destroy(previewCardGo);
            previewCardGo = null;
            previewRect = null;
            previewCC = null;
            previewTitle = previewDesc = null;
            previewCardIndex = -1;
        }
    }

    static GUIStyle BoldStyle()
    {
        if (boldStyle == null)
        {
            boldStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        }
        return boldStyle;
    }
    static GUIStyle boldStyle;

    /// <summary>按 Sprite 在纹理中的子矩形绘制（多层卡面叠画）。</summary>
    static void DrawSprite(Sprite s, Rect r)
    {
        if (s == null || s.texture == null) return;
        var tex = s.texture;
        var coords = new Rect(s.rect.x / tex.width, s.rect.y / tex.height,
                              s.rect.width / tex.width, s.rect.height / tex.height);
        GUI.DrawTextureWithTexCoords(r, tex, coords);
    }
}
#endif
