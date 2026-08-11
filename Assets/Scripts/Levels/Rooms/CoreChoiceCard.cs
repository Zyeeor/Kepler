using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// A single choice card. Instantiated by CoreChoiceUI.
/// Has text, image, confirm/reroll buttons, and status marks.
/// </summary>
public class CoreChoiceCard : MonoBehaviour
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

    public void Init(int index, string text, Sprite sprite, string description, Action<int> onSelect, Action<int> onReroll)
    {
        Index = index;
        IsSelected = false;
        IsRerolled = false;

        if (cardText != null) cardText.text = text;
        if (cardImage != null && sprite != null) cardImage.sprite = sprite;
        if (descriptionText != null) descriptionText.text = description;
        if (confirmedMark != null) confirmedMark.SetActive(false);
        if (rerolledMark != null) rerolledMark.SetActive(false);

        confirmButton?.onClick.RemoveAllListeners();
        confirmButton?.onClick.AddListener(() =>
        {
            if (IsRerolled) return;
            onSelect?.Invoke(Index);
        });

        rerollButton?.onClick.RemoveAllListeners();
        rerollButton?.onClick.AddListener(() =>
        {
            if (IsRerolled) return;
            IsRerolled = true;
            RefreshUI();
            onReroll?.Invoke(Index);
        });
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
    public void Replace(string text, Sprite sprite, string description)
    {
        IsRerolled = false;
        IsSelected = false;
        if (cardText != null) cardText.text = text;
        if (cardImage != null && sprite != null) cardImage.sprite = sprite;
        if (descriptionText != null) descriptionText.text = description;
        if (confirmedMark != null) confirmedMark.SetActive(false);
        if (rerolledMark != null) rerolledMark.SetActive(false);
        if (confirmButton != null) confirmButton.interactable = true;
        if (rerollButton != null) rerollButton.interactable = true;
    }
}
