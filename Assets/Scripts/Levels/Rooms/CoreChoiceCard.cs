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

    /// <summary>
    /// 应用 CardData 配置的四层素材（foreground/middleground/background/broader）。
    /// 字段为 null 的层保持 prefab 默认素材不动（便于内容侧逐层配置）。
    /// </summary>
    void ApplyLayers(CardData data)
    {
        if (data == null) return;
        if (foregroundImage != null && data.foregroundSprite != null) foregroundImage.sprite = data.foregroundSprite;
        if (middlegroundImage != null && data.middlegroundSprite != null) middlegroundImage.sprite = data.middlegroundSprite;
        if (backgroundImage != null && data.backgroundSprite != null) backgroundImage.sprite = data.backgroundSprite;
        if (borderImage != null && data.borderSprite != null) borderImage.sprite = data.borderSprite;
    }
}
