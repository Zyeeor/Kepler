using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

/// <summary>
/// A single choice card. Instantiated by CoreChoiceUI.
/// Has text, image, confirm/reroll buttons, and status marks.
/// </summary>
[DefaultExecutionOrder(100)]
public class CoreChoiceCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
{
    public int Index { get; private set; }
    public bool IsSelected { get; private set; }
    public bool IsRerolled { get; private set; }

    [Header("UI Elements (assign on prefab)")]
    public TextMeshProUGUI cardText;
    public Image cardImage;
    public TextMeshProUGUI descriptionText;
    public Button confirmButton;
    public Button rerollButton;
    public GameObject confirmedMark;
    public GameObject rerolledMark;

    [Header("Card Layers (assign on prefab, optional)")]
    public Image foregroundImage;
    public Image middlegroundImage;
    public Image backgroundImage;
    public Image borderImage;

    private Action<int> onSelectCallback;

    // Cache prefab defaults so a card switch cannot leave stale layers.
    private Sprite defaultForegroundSprite;
    private Sprite defaultMiddlegroundSprite;
    private Sprite defaultBackgroundSprite;
    private Sprite defaultBorderSprite;
    private bool defaultForegroundEnabled;
    private bool defaultMiddlegroundEnabled;
    private bool defaultBackgroundEnabled;
    private bool defaultBorderEnabled;
    private Vector3 defaultForegroundPosition;
    private Vector3 defaultMiddlegroundPosition;
    private Vector3 defaultBackgroundPosition;
    private Vector3 defaultBorderPosition;
    private Quaternion defaultForegroundRotation;
    private Quaternion defaultMiddlegroundRotation;
    private Quaternion defaultBackgroundRotation;
    private Quaternion defaultBorderRotation;
    private bool defaultLayersCached;

    [Header("Same-Layer Parallax")]
    [Tooltip("同层第一张额外素材跟随基础素材视差的比例。")]
    [SerializeField, Range(0f, 1f)] private float firstExtraParallaxMultiplier = 0.75f;
    [Tooltip("同层每往后一张额外素材减少的跟随比例。")]
    [SerializeField, Range(0f, 0.5f)] private float extraParallaxStep = 0.25f;
    [Tooltip("同层额外素材最低保留的视差跟随比例。")]
    [SerializeField, Range(0f, 1f)] private float minimumExtraParallaxMultiplier = 0.25f;

    private sealed class ExtraLayerMotionState
    {
        public RectTransform rect;
        public RectTransform source;
        public Vector3 basePosition;
        public Quaternion baseRotation;
        public Vector3 sourceBasePosition;
        public Quaternion sourceBaseRotation;
        public float multiplier;
    }

    private readonly System.Collections.Generic.List<ExtraLayerMotionState> _extraLayerMotionStates =
        new System.Collections.Generic.List<ExtraLayerMotionState>();

    void Awake()
    {
        EnsureDefaultLayersCached();
    }

    void EnsureDefaultLayersCached()
    {
        if (defaultLayersCached) return;

        CacheDefaultLayers();
        defaultLayersCached = true;
    }

    void CacheDefaultLayers()
    {
        if (foregroundImage != null)
        {
            defaultForegroundSprite = foregroundImage.sprite;
            defaultForegroundEnabled = foregroundImage.enabled;
            defaultForegroundPosition = foregroundImage.rectTransform.localPosition;
            defaultForegroundRotation = foregroundImage.rectTransform.localRotation;
        }
        if (middlegroundImage != null)
        {
            defaultMiddlegroundSprite = middlegroundImage.sprite;
            defaultMiddlegroundEnabled = middlegroundImage.enabled;
            defaultMiddlegroundPosition = middlegroundImage.rectTransform.localPosition;
            defaultMiddlegroundRotation = middlegroundImage.rectTransform.localRotation;
        }
        if (backgroundImage != null)
        {
            defaultBackgroundSprite = backgroundImage.sprite;
            defaultBackgroundEnabled = backgroundImage.enabled;
            defaultBackgroundPosition = backgroundImage.rectTransform.localPosition;
            defaultBackgroundRotation = backgroundImage.rectTransform.localRotation;
        }
        if (borderImage != null)
        {
            defaultBorderSprite = borderImage.sprite;
            defaultBorderEnabled = borderImage.enabled;
            defaultBorderPosition = borderImage.rectTransform.localPosition;
            defaultBorderRotation = borderImage.rectTransform.localRotation;
        }
    }

    void LateUpdate()
    {
        for (int i = 0; i < _extraLayerMotionStates.Count; i++)
        {
            ExtraLayerMotionState state = _extraLayerMotionStates[i];
            if (state.rect == null || state.source == null) continue;

            Vector3 positionDelta = state.source.localPosition - state.sourceBasePosition;
            Quaternion rotationDelta = Quaternion.Inverse(state.sourceBaseRotation) * state.source.localRotation;

            state.rect.localPosition = state.basePosition + positionDelta * state.multiplier;
            state.rect.localRotation = state.baseRotation
                * Quaternion.SlerpUnclamped(Quaternion.identity, rotationDelta, state.multiplier);
        }
    }
    public void Init(int index, string text, Sprite sprite, string description, Action<int> onSelect, Action<int> onReroll, CardData data = null)
    {
        Index = index;
        IsSelected = false;
        IsRerolled = false;
        onSelectCallback = onSelect;

        if (cardText != null) cardText.text = text;
        if (cardImage != null) { cardImage.sprite = sprite; cardImage.color = ResolveIconColor(sprite); }
        if (descriptionText != null) descriptionText.text = description;
        ApplyLayers(data);
        if (confirmedMark != null) confirmedMark.SetActive(false);
        if (rerolledMark != null) rerolledMark.SetActive(false);

        confirmButton?.onClick.RemoveAllListeners();
        confirmButton?.onClick.AddListener(() =>
        {
            Debug.Log($"[CoreChoiceCard] Select clicked: index={Index}, card='{cardText?.text}', rerolled={IsRerolled}");
            if (IsRerolled) return;
            onSelect?.Invoke(Index);
        });

        rerollButton?.onClick.RemoveAllListeners();
        rerollButton?.onClick.AddListener(() =>
        {
            Debug.Log($"[CoreChoiceCard] Reroll clicked: index={Index}, card='{cardText?.text}', rerolled={IsRerolled}");
            if (IsRerolled) return;
            // 无可刷新候选/次数已满时不进入刷新（不置 IsRerolled，保持卡片可点）
            if (CardManager.Instance != null && !CardManager.Instance.HasRerollCandidates(Index))
            {
                Debug.Log($"[CoreChoiceCard] Reroll skipped: no candidates left or reroll limit reached, index={Index}");
                return;
            }
            // 刷新后是否锁卡由 CoreChoiceUI 统一判定（ApplyRerollLock）——支持每卡多次刷新
            onReroll?.Invoke(Index);
        });

        Debug.Log($"[CoreChoiceCard] Init: index={Index}, object='{name}', confirm={(confirmButton != null ? confirmButton.name : "NULL")}, confirmInteractable={(confirmButton != null && confirmButton.interactable)}, reroll={(rerollButton != null ? rerollButton.name : "NULL")}, rerollInteractable={(rerollButton != null && rerollButton.interactable)}, active={gameObject.activeInHierarchy}");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log($"[CoreChoiceCard] PointerEnter: index={Index}, object='{name}', position={eventData.position}, selected={IsSelected}");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log($"[CoreChoiceCard] PointerExit: index={Index}, object='{name}', position={eventData.position}");
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"[CoreChoiceCard] PointerDown: index={Index}, object='{name}', button={eventData.button}, position={eventData.position}, confirmInteractable={(confirmButton != null && confirmButton.interactable)}, rerollInteractable={(rerollButton != null && rerollButton.interactable)}");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[CoreChoiceCard] PointerClick: index={Index}, object='{name}', button={eventData.button}, position={eventData.position}");
        if (eventData.button != PointerEventData.InputButton.Left || IsRerolled) return;

        GameObject clickedObject = eventData.pointerPress != null
            ? eventData.pointerPress
            : eventData.pointerCurrentRaycast.gameObject;
        if (clickedObject != null && clickedObject.GetComponentInParent<Button>() != null)
        {
            Debug.Log($"[CoreChoiceCard] Card body selection skipped because click belongs to button '{clickedObject.GetComponentInParent<Button>().name}'.");
            return;
        }

        Debug.Log($"[CoreChoiceCard] Card body selected: index={Index}, object='{name}'");
        onSelectCallback?.Invoke(Index);
    }

    void RefreshUI()
    {
        if (IsSelected)
        {
            if (confirmedMark != null) confirmedMark.SetActive(true);
        }
        else if (IsRerolled)
        {
            if (confirmButton != null) confirmButton.interactable = false;
            if (rerollButton != null) rerollButton.interactable = false;
            if (confirmedMark != null) confirmedMark.SetActive(false);
            if (rerolledMark != null) rerolledMark.SetActive(true);
        }
    }

    /// <summary>Toggle selected state. Called externally by CoreChoiceUI.</summary>
    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        RefreshUI();
    }

    /// <summary>Replace this card's content with a new card (used on reroll).</summary>
    public void Replace(string text, Sprite sprite, string description, CardData data = null)
    {
        IsRerolled = false;
        IsSelected = false;
        if (cardText != null) cardText.text = text;
        if (cardImage != null) { cardImage.sprite = sprite; cardImage.color = ResolveIconColor(sprite); }
        if (descriptionText != null) descriptionText.text = description;
        ApplyLayers(data);
        if (confirmedMark != null) confirmedMark.SetActive(false);
        if (rerolledMark != null) rerolledMark.SetActive(false);
        if (confirmButton != null) confirmButton.interactable = true;
        if (rerollButton != null) rerollButton.interactable = true;
    }

    /// <summary>
    /// 由 CoreChoiceUI 在每次刷新后调用：locked=true（该槽位已刷满 maxRerollsPerCard 次）
    /// 锁定卡片（禁用刷新/选择）；locked=false 恢复可交互。
    /// </summary>
    public void ApplyRerollLock(bool locked)
    {
        IsRerolled = locked;
        if (locked)
        {
            RefreshUI();
        }
        else
        {
            if (confirmButton != null) confirmButton.interactable = true;
            if (rerollButton != null) rerollButton.interactable = true;
        }
    }

    // ── 技能图标颜色：卡片主图（cardImage/image(1)）沿用 MonsterSkillIconConfig 对应槽位配置的颜色 ──
    static MonsterSkillIconConfig iconConfigCache;
    static MonsterSkillIconConfig ResolveIconConfig()
    {
        if (iconConfigCache == null) iconConfigCache = Resources.Load<MonsterSkillIconConfig>("UI/MonsterSkillIconConfig");
        return iconConfigCache;
    }
    /// <summary>按卡片主图 sprite 反查 MonsterSkillIconConfig 对应图标颜色；查不到（如玩家卡/非技能图标）返回白色。</summary>
    static Color ResolveIconColor(Sprite sprite)
    {
        if (sprite == null) return Color.white;
        var cfg = ResolveIconConfig();
        if (cfg != null && cfg.TryGetColorByIcon(sprite, out var color)) return color;
        return Color.white;
    }

    // ── 动态生成的额外并列素材层（extraXxxSprites[0..N-1]），随 ApplyLayers 清理 ──
    private readonly System.Collections.Generic.List<Image> _extraForegroundImages = new System.Collections.Generic.List<Image>();
    private readonly System.Collections.Generic.List<Image> _extraMiddlegroundImages = new System.Collections.Generic.List<Image>();
    private readonly System.Collections.Generic.List<Image> _extraBackgroundImages = new System.Collections.Generic.List<Image>();
    private readonly System.Collections.Generic.List<Image> _extraBorderImages = new System.Collections.Generic.List<Image>();

    /// <summary>
    /// 应用 CardData 配置的多层素材（foreground/middleground/background/border，每层可扩展并列多张）。
    /// 每层基础素材赋给 prefab 上已挂的 Image；额外素材生成为同级对象，并按顺序以递减比例跟随基础层视差。
    /// 前景/中景/背景为空时隐藏，边框为空时使用 prefab 默认边框。
    /// 供 CoreChoiceCard（Init/Replace）与 CardFaceBrowser（调试预览）复用。
    /// </summary>
    public void ApplyLayers(CardData data)
    {
        // Init can run while the popup hierarchy is inactive, before Unity invokes Awake.
        // Cache serialized prefab defaults here so generated layers always receive valid baselines.
        EnsureDefaultLayersCached();
        ClearExtraLayers();
        ApplyLayerGroup(
            data != null ? data.foregroundSprite : null,
            data != null ? data.extraForegroundSprites : null,
            data != null && (data.hideForegroundLayer || data.foregroundSprite == null),
            foregroundImage,
            _extraForegroundImages,
            defaultForegroundSprite,
            defaultForegroundEnabled,
            defaultForegroundPosition,
            defaultForegroundRotation,
            "ForegroundExtra");
        ApplyLayerGroup(
            data != null ? data.middlegroundSprite : null,
            data != null ? data.extraMiddlegroundSprites : null,
            data != null && (data.hideMiddlegroundLayer || data.middlegroundSprite == null),
            middlegroundImage,
            _extraMiddlegroundImages,
            defaultMiddlegroundSprite,
            defaultMiddlegroundEnabled,
            defaultMiddlegroundPosition,
            defaultMiddlegroundRotation,
            "MiddlegroundExtra");
        ApplyLayerGroup(
            data != null ? data.backgroundSprite : null,
            data != null ? data.extraBackgroundSprites : null,
            data != null && (data.hideBackgroundLayer || data.backgroundSprite == null),
            backgroundImage,
            _extraBackgroundImages,
            defaultBackgroundSprite,
            defaultBackgroundEnabled,
            defaultBackgroundPosition,
            defaultBackgroundRotation,
            "BackgroundExtra");
        ApplyLayerGroup(
            data != null ? data.borderSprite : null,
            data != null ? data.extraBorderSprites : null,
            data != null && data.hideBorderLayer,
            borderImage,
            _extraBorderImages,
            defaultBorderSprite,
            defaultBorderEnabled,
            defaultBorderPosition,
            defaultBorderRotation,
            "BorderExtra");
    }
    /// <summary>把一层素材（基础 + 额外并列列表）应用到展示位 Image，并动态生成其余并列层。</summary>
    void ApplyLayerGroup(
        Sprite baseSprite,
        System.Collections.Generic.List<Sprite> extraSprites,
        bool hidden,
        Image first,
        System.Collections.Generic.List<Image> extraImages,
        Sprite defaultSprite,
        bool defaultEnabled,
        Vector3 defaultPosition,
        Quaternion defaultRotation,
        string extraName)
    {
        if (first == null) return;

        // 前景、中景、背景为空时隐藏；边框为空时沿用预制体默认边框。Hide 仍可显式隐藏有配置的图层。
        first.sprite = hidden ? null : (baseSprite != null ? baseSprite : defaultSprite);
        first.enabled = hidden ? false : (first.sprite != null && (baseSprite != null || defaultEnabled));

        if (hidden || extraSprites == null || extraSprites.Count == 0) return;

        int insertedCount = 0;
        for (int i = 0; i < extraSprites.Count; i++)
        {
            Sprite sprite = extraSprites[i];
            if (sprite == null) continue;

            var go = new GameObject(extraName + "_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rt = (RectTransform)go.transform;
            // 额外素材保持为基础 Image 的同级对象，并在 LateUpdate 中按递减比例跟随基础层视差。
            rt.SetParent(first.transform.parent, false);
            CopyRectTransform(first.rectTransform, rt, defaultPosition, defaultRotation);

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = first.color;
            img.material = first.material;
            img.raycastTarget = false;   // 扩展层不阻挡交互

            // 排在基础素材之后，列表索引越大越靠上
            rt.SetSiblingIndex(first.transform.GetSiblingIndex() + 1 + insertedCount);

            float multiplier = Mathf.Max(
                minimumExtraParallaxMultiplier,
                firstExtraParallaxMultiplier - extraParallaxStep * insertedCount);
            _extraLayerMotionStates.Add(new ExtraLayerMotionState
            {
                rect = rt,
                source = first.rectTransform,
                basePosition = defaultPosition,
                baseRotation = defaultRotation,
                sourceBasePosition = defaultPosition,
                sourceBaseRotation = defaultRotation,
                multiplier = multiplier
            });

            insertedCount++;
            extraImages.Add(img);
        }
    }

    /// <summary>复制布局，并使用预制体基础位置作为同层视差的零点。</summary>
    static void CopyRectTransform(
        RectTransform from,
        RectTransform to,
        Vector3 defaultPosition,
        Quaternion defaultRotation)
    {
        to.anchorMin = from.anchorMin;
        to.anchorMax = from.anchorMax;
        to.sizeDelta = from.sizeDelta;
        to.pivot = from.pivot;
        to.localPosition = defaultPosition;
        to.localRotation = defaultRotation;
        to.localScale = from.localScale;
    }
    /// <summary>销毁本次生成的动态扩展层（ApplyLayers 前调用，避免累积）。</summary>
    void ClearExtraLayers()
    {
        _extraLayerMotionStates.Clear();
        DestroyExtra(_extraForegroundImages);
        DestroyExtra(_extraMiddlegroundImages);
        DestroyExtra(_extraBackgroundImages);
        DestroyExtra(_extraBorderImages);
    }

    void DestroyExtra(System.Collections.Generic.List<Image> extras)
    {
        if (extras == null) return;
        foreach (var img in extras)
            if (img != null) Destroy(img.gameObject);
        extras.Clear();
    }
}
