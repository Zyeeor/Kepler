#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    GameObject previewCanvasGo;
    Canvas previewCanvas;
    GameObject previewCardGo;
    RectTransform previewRect;
    CoreChoiceCard previewCC;   // 复用克隆卡的多层素材应用逻辑（按钮隐藏，不触发选卡回调）
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
            string cardName = GetCardName(card);
            bool match = string.IsNullOrEmpty(f)
                || (cardName != null && cardName.ToLowerInvariant().Contains(f))
                || (card.effectId != null && card.effectId.ToLowerInvariant().Contains(f));
            if (!match) continue;

            string unlockedMark = cm != null && cm.IsEffectUnlocked(card.effectId) ? " ✓" : "";
            string label = $"{cardName}  [{card.category}/{card.monsterType}]{unlockedMark}";
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
            GUI.Label(new Rect(x + pad, bottomY, listW - 16f, lineH), GetCardName(card), BoldStyle());
            GUI.Label(new Rect(x + pad, bottomY + lineH, listW - 16f, lineH), $"{card.effectId} · {card.category}/{card.monsterType}");

            var choiceUI = ResolveChoiceUI();
            bool drafting = choiceUI != null && choiceUI.IsDrafting;
            GUI.enabled = drafting;
            if (GUI.Button(new Rect(x + pad, bottomY + lineH * 2, listW - 16f, 30f),
                drafting ? "替换选卡界面第 1 张（立即生效）" : "（选卡弹窗未开启）"))
            {
                if (cm != null && choiceUI != null)
                {
                    cm.DebugReplacePick(0, card);
                    choiceUI.RefreshCards();
                }
            }
            GUI.enabled = true;
        }

        // ── 预览卡：复用正式选卡 prefab 的完整内部布局，根锚点放在中间卡位置 ──
        if (selectedIndex >= 0 && selectedIndex < pool.Count)
        {
            var card = pool[selectedIndex];
            bool hasLivePreview = EnsurePreviewCard(card, selectedIndex);
            PositionPreviewCard(Screen.width * 0.5f, Screen.height * 0.5f);
            if (!hasLivePreview)
            {
                // 无选卡模板回退：屏幕中央静态多层叠画（仅无 prefab 时使用）
                float fw = 300f * previewScale, fh = 600f * previewScale;
                var faceRect = new Rect(Screen.width * 0.5f - fw * 0.5f, Screen.height * 0.5f - fh * 0.5f, fw, fh);
                bool anyLayer = false;
                if (!card.hideBackgroundLayer && card.backgroundSprite != null) { DrawSprite(card.backgroundSprite, faceRect); anyLayer = true; }
                if (!card.hideBackgroundLayer && card.extraBackgroundSprites != null) foreach (var s in card.extraBackgroundSprites) if (s != null) { DrawSprite(s, faceRect); anyLayer = true; }
                if (!card.hideMiddlegroundLayer && card.middlegroundSprite != null) { DrawSprite(card.middlegroundSprite, faceRect); anyLayer = true; }
                if (!card.hideMiddlegroundLayer && card.extraMiddlegroundSprites != null) foreach (var s in card.extraMiddlegroundSprites) if (s != null) { DrawSprite(s, faceRect); anyLayer = true; }
                if (!card.hideForegroundLayer && card.foregroundSprite != null) { DrawSprite(card.foregroundSprite, faceRect); anyLayer = true; }
                if (!card.hideForegroundLayer && card.extraForegroundSprites != null) foreach (var s in card.extraForegroundSprites) if (s != null) { DrawSprite(s, faceRect); anyLayer = true; }
                if (!card.hideBorderLayer && card.borderSprite != null) { DrawSprite(card.borderSprite, faceRect); anyLayer = true; }
                if (!card.hideBorderLayer && card.extraBorderSprites != null) foreach (var s in card.extraBorderSprites) if (s != null) { DrawSprite(s, faceRect); anyLayer = true; }
                if (!anyLayer) DrawSprite(card.image, faceRect);
                DrawFallbackCardText(card, faceRect);
            }

            // ── 预览卡右侧：解锁此卡按钮（F3 调试）──
            // 无论选卡弹窗是否开启都可解锁（即时注入本局已解锁卡，并应用到现存怪）。
            float bw = 180f, bh = 44f;
            float bx = Mathf.Min(Screen.width * 0.5f + 360f, Screen.width - bw - 12f);
            float by = Screen.height * 0.5f - bh * 0.5f;
            bool unlockedNow = cm != null && cm.IsEffectUnlocked(card.effectId);
            GUI.enabled = cm != null && !unlockedNow;
            if (GUI.Button(new Rect(bx, by, bw, bh), unlockedNow ? "已解锁 ✓" : "解锁此卡"))
            {
                if (cm != null)
                {
                    cm.UnlockEffect(card.effectId);
                    Debug.Log($"[CardFaceBrowser] 调试解锁卡：{GetCardName(card)} ({card.effectId})");
                }
            }
            GUI.enabled = true;
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
        var ui = ResolveChoiceUI();
        if (ui == null || ui.cardPrefab == null)
        {
            DestroyPreviewCard();
            return false;
        }

        if (previewCardGo == null)
        {
            // F3 预览不能依赖选卡 Canvas：该 Canvas 在正式流程中可能 inactive 或 localScale=0。
            // 创建独立的 ScreenSpaceOverlay，保证调试预览始终可见。
            EnsurePreviewCanvas();
            var wrapper = new GameObject("CardFaceBrowserPreviewRoot", typeof(RectTransform));
            previewCardGo = wrapper;
            previewRect = wrapper.GetComponent<RectTransform>();
            previewRect.SetParent(previewCanvas.transform, false);
            previewRect.anchorMin = new Vector2(0.5f, 0.5f);
            previewRect.anchorMax = new Vector2(0.5f, 0.5f);
            previewRect.pivot = new Vector2(0.5f, 0.5f);
            previewRect.anchoredPosition = Vector2.zero;

            var go = Instantiate(ui.cardPrefab, previewRect, false);
            go.name = "CardFaceBrowserPreview";
            // 正式选卡由 HorizontalLayoutGroup 放置卡根；独立预览没有该布局组，
            // 将 prefab 卡根锚到预览根中心，避免其左下锚点把整张卡（含文字）偏移半个 100x100 根节点。
            var cardRect = go.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                cardRect.anchorMin = new Vector2(0.5f, 0.5f);
                cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                cardRect.pivot = new Vector2(0.5f, 0.5f);
                cardRect.anchoredPosition = Vector2.zero;
            }
            previewCC = go.GetComponent<CoreChoiceCard>();
            if (previewCC == null)
            {
                DestroyPreviewCard();
                return false;
            }

            previewTitle = previewCC.cardText;
            previewDesc = previewCC.descriptionText;
            if (previewCC.confirmButton != null) previewCC.confirmButton.gameObject.SetActive(false);
            if (previewCC.rerollButton != null) previewCC.rerollButton.gameObject.SetActive(false);
            if (previewCC.confirmedMark != null) previewCC.confirmedMark.SetActive(false);
            if (previewCC.rerolledMark != null) previewCC.rerolledMark.SetActive(false);
            // 不重排文本：预制体本身就是正式选卡界面的布局来源，保留其父级、锚点、坐标和尺寸。
            previewCC.enabled = true;
            previewCardIndex = -1;
        }

        // 每帧刷新缩放：正式卡片可用时继承 cardParent，否则使用独立预览默认缩放。
        SyncPreviewScale(ui);

        if (previewCardIndex != poolIndex)
        {
            previewCardIndex = poolIndex;
            previewCC.ApplyLayers(card);
            if (previewTitle != null) previewTitle.text = GetCardName(card);
            if (previewDesc != null) previewDesc.text = GetCardDescription(card);
            if (FontRegistry.Instance != null)
                FontRegistry.Instance.ApplyFontToTree(previewCardGo.transform, FontSlots.Card);
        }
        return true;
    }

    CoreChoiceUI ResolveChoiceUI()
    {
        if (CoreChoiceUI.Instance != null) return CoreChoiceUI.Instance;

        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var candidates = Resources.FindObjectsOfTypeAll<CoreChoiceUI>();
        for (int i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            if (candidate == null) continue;
            var scene = candidate.gameObject.scene;
            if (scene.IsValid() && scene == activeScene)
                return candidate;
        }
        return null;
    }

    void EnsurePreviewCanvas()
    {
        if (previewCanvas != null && previewCanvasGo != null) return;

        previewCanvasGo = new GameObject(
            "CardFaceBrowserPreviewCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        previewCanvas = previewCanvasGo.GetComponent<Canvas>();
        previewCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        previewCanvas.overrideSorting = true;
        previewCanvas.sortingOrder = 32000;

        var scaler = previewCanvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    static string GetCardName(CardData card)
    {
        if (card == null) return string.Empty;
        string resolved = card.ResolveCardName();
        return string.IsNullOrEmpty(resolved) ? card.cardName ?? string.Empty : resolved;
    }

    static string GetCardDescription(CardData card)
    {
        if (card == null) return string.Empty;
        string resolved = card.ResolveDescription();
        return string.IsNullOrEmpty(resolved) ? card.description ?? string.Empty : resolved;
    }

    /// <summary>
    /// 把预览卡根定位到正式选卡中间卡的锚点位置。
    /// 注意：正式卡的文字在卡根下方，不能把视觉卡片主体再居中校正，否则会改变文字相对位置。
    /// </summary>
    void PositionPreviewCard(float screenCenterX, float screenCenterY)
    {
        if (previewRect == null || !(previewRect.parent is RectTransform parent)) return;

        Vector2 screenPt = new Vector2(screenCenterX, screenCenterY + previewOffsetY);
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screenPt, null, out Vector3 world))
            previewRect.position = world;
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
        if (previewCanvasGo != null)
            Destroy(previewCanvasGo);
        else if (previewCardGo != null)
            Destroy(previewCardGo);

        previewCanvasGo = null;
        previewCanvas = null;
        previewCardGo = null;
        previewRect = null;
        previewCC = null;
        previewTitle = previewDesc = null;
        previewCardIndex = -1;
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
    static GUIStyle fallbackTitleStyle;
    static GUIStyle fallbackDescriptionStyle;

    static void DrawFallbackCardText(CardData card, Rect faceRect)
    {
        var titleRect = new Rect(faceRect.x + 18f, faceRect.y + 28f, faceRect.width - 36f, 72f);
        var descRect = new Rect(faceRect.x + 22f, faceRect.y + 128f, faceRect.width - 44f, faceRect.height - 170f);
        GUI.Label(titleRect, GetCardName(card), FallbackTitleStyle());
        GUI.Label(descRect, GetCardDescription(card), FallbackDescriptionStyle());
    }

    static GUIStyle FallbackTitleStyle()
    {
        if (fallbackTitleStyle == null)
        {
            fallbackTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 22,
                wordWrap = true
            };
            fallbackTitleStyle.normal.textColor = Color.white;
        }
        return fallbackTitleStyle;
    }

    static GUIStyle FallbackDescriptionStyle()
    {
        if (fallbackDescriptionStyle == null)
        {
            fallbackDescriptionStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 16,
                wordWrap = true
            };
            fallbackDescriptionStyle.normal.textColor = Color.white;
        }
        return fallbackDescriptionStyle;
    }

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
