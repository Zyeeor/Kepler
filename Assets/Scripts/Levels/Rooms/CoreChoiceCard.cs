using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

/// <summary>
/// A single choice card. Instantiated by CoreChoiceUI.
/// Has text, image, confirm/reroll buttons, and status marks.
/// </summary>
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

    public void Init(int index, string text, Sprite sprite, string description, Action<int> onSelect, Action<int> onReroll, CardData data = null)
    {
        Index = index;
        IsSelected = false;
        IsRerolled = false;
        onSelectCallback = onSelect;

        if (cardText != null) cardText.text = text;
        if (cardImage != null && sprite != null) cardImage.sprite = sprite;
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
        if (cardImage != null && sprite != null) cardImage.sprite = sprite;
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

    // ── 动态生成的额外并列素材层（extraXxxSprites[0..N-1]），随 ApplyLayers 清理 ──
    private readonly System.Collections.Generic.List<Image> _extraForegroundImages = new System.Collections.Generic.List<Image>();
    private readonly System.Collections.Generic.List<Image> _extraMiddlegroundImages = new System.Collections.Generic.List<Image>();
    private readonly System.Collections.Generic.List<Image> _extraBackgroundImages = new System.Collections.Generic.List<Image>();
    private readonly System.Collections.Generic.List<Image> _extraBorderImages = new System.Collections.Generic.List<Image>();

    /// <summary>
    /// 应用 CardData 配置的多层素材（foreground/middleground/background/border，每层可扩展并列多张）。
    /// 每层基础素材赋给 prefab 上已挂的 Image；extraXxxSprites 列表运行时动态生成 Image 作为其 sibling
    /// （sibling index 越大越靠前，即列表索引越大越靠上）。字段为 null / 空列表的层保持 prefab 默认素材不动。
    /// 供 CoreChoiceCard（Init/Replace）与 CardFaceBrowser（调试预览）复用。
    /// </summary>
    public void ApplyLayers(CardData data)
    {
        ClearExtraLayers();
        if (data == null) return;
        ApplyLayerGroup(data.foregroundSprite, data.extraForegroundSprites, foregroundImage, _extraForegroundImages, "ForegroundExtra");
        ApplyLayerGroup(data.middlegroundSprite, data.extraMiddlegroundSprites, middlegroundImage, _extraMiddlegroundImages, "MiddlegroundExtra");
        ApplyLayerGroup(data.backgroundSprite, data.extraBackgroundSprites, backgroundImage, _extraBackgroundImages, "BackgroundExtra");
        ApplyLayerGroup(data.borderSprite, data.extraBorderSprites, borderImage, _extraBorderImages, "BorderExtra");
    }

    /// <summary>把一层素材（基础 + 额外并列列表）应用到展示位 Image，并动态生成其余并列层。</summary>
    void ApplyLayerGroup(Sprite baseSprite, System.Collections.Generic.List<Sprite> extraSprites, Image first, System.Collections.Generic.List<Image> extraImages, string extraName)
    {
        if (first != null && baseSprite != null)
            first.sprite = baseSprite;

        if (extraSprites == null || extraSprites.Count == 0) return;

        for (int i = 0; i < extraSprites.Count; i++)
        {
            Sprite sprite = extraSprites[i];
            if (sprite == null || first == null) continue;

            var go = new GameObject($"{extraName}_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(first.transform.parent, false);
            CopyRectTransform(first.rectTransform, rt);

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = first.color;
            img.material = first.material;
            img.raycastTarget = false;   // 扩展层不阻挡交互

            // 排在基础素材之后（sibling index 越大越靠前），列表索引越大越靠上
            rt.SetSiblingIndex(first.transform.GetSiblingIndex() + 1 + i);
            extraImages.Add(img);
        }
    }

    /// <summary>复制 RectTransform 布局参数，让动态扩展层与基础素材同位。</summary>
    static void CopyRectTransform(RectTransform from, RectTransform to)
    {
        to.anchorMin = from.anchorMin;
        to.anchorMax = from.anchorMax;
        to.anchoredPosition = from.anchoredPosition;
        to.sizeDelta = from.sizeDelta;
        to.pivot = from.pivot;
        to.localRotation = from.localRotation;
        to.localScale = from.localScale;
    }

    /// <summary>销毁本次生成的动态扩展层（ApplyLayers 前调用，避免累积）。</summary>
    void ClearExtraLayers()
    {
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
