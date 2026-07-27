using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Horizontal 3-option choice UI for Room Core interaction.
/// Uses a single card prefab instantiated 3 times into a HorizontalLayoutGroup parent.
/// Each card has confirm / reroll buttons, text, image, and status marks.
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
    private RoomCore currentCore;
    private CoreChoiceCard[] cards;

    void Awake()
    {
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
        confirmAllButton?.onClick.AddListener(OnConfirmAll);
        if (confirmAllButton != null) confirmAllButton.interactable = true;
    }

    public void Show(RoomCore core)
    {
        currentCore = core;

        // Clear old cards
        if (cardParent != null)
        {
            for (int i = cardParent.childCount - 1; i >= 0; i--)
                Destroy(cardParent.GetChild(i).gameObject);
        }

        // Draw random cards from CardManager
        cards = new CoreChoiceCard[cardCount];
        string[] cardNames = new string[cardCount];
        string[] cardDescriptions = new string[cardCount];
        Sprite[] cardSprites = new Sprite[cardCount];
        for (int i = 0; i < cardCount; i++) cardNames[i] = "Option " + (char)('A' + i);

        if (CardManager.Instance != null)
        {
            CardManager.Instance.DrawCards(cardCount);
            var picks = CardManager.Instance.currentPicks;
            for (int i = 0; i < cardCount; i++)
            {
                if (i < picks.Length && picks[i] != null)
                {
                    cardNames[i] = picks[i].cardName;
                    cardDescriptions[i] = picks[i].description ?? "";
                    cardSprites[i] = picks[i].image;
                }
            }
        }
        else
        {
            Debug.LogWarning("[CoreChoiceUI] CardManager.Instance is null — no cards will be shown. Add CardManager to the scene.");
        }

        // Instantiate cards
        for (int i = 0; i < cardCount; i++)
        {
            var go = cardPrefab != null ? Instantiate(cardPrefab, cardParent) : null;
            if (go == null) continue;

            var card = go.GetComponent<CoreChoiceCard>();
            if (card == null) card = go.AddComponent<CoreChoiceCard>();
            card.Init(i, cardNames[i], cardSprites[i], cardDescriptions[i], OnCardConfirm, OnCardReroll);
            cards[i] = card;
        }

        Debug.Log($"[CoreChoiceUI] Show called: panelRoot={(panelRoot != null ? panelRoot.name : "NULL")}, cardPrefab={(cardPrefab != null ? cardPrefab.name : "NULL")}, cardParent={(cardParent != null ? cardParent.name : "NULL")}");
        if (panelRoot != null) panelRoot.SetActive(true);
        else Debug.LogError("[CoreChoiceUI] panelRoot is NULL — drag the UI Panel into this field!");

        // Re-bind confirm all
        if (confirmAllButton != null)
        {
            confirmAllButton.onClick.RemoveAllListeners();
            confirmAllButton.onClick.AddListener(OnConfirmAll);
            confirmAllButton.interactable = true;
        }

        Time.timeScale = 0f;
    }

    private int selectedIndex = -1; // currently selected card, -1 = none

    void OnCardConfirm(int index)
    {
        if (cards == null || index < 0 || index >= cards.Length) return;
        if (cards[index] == null) return;

        // Deselect previous, select new
        if (selectedIndex >= 0 && selectedIndex < cards.Length && cards[selectedIndex] != null)
            cards[selectedIndex].SetSelected(false);
        cards[index].SetSelected(true);
        selectedIndex = index;
    }

    void OnCardReroll(int index)
    {
        if (CardManager.Instance == null || cards == null || index < 0 || index >= cards.Length) return;
        if (cards[index] == null) return;

        var newCard = CardManager.Instance.DrawOneReroll();
        if (newCard != null)
        {
            Sprite sprite = null;
            if (newCard.image != null) sprite = newCard.image;
            cards[index].Replace(newCard.cardName, sprite, newCard.description ?? "");
            CardManager.Instance.currentPicks[index] = newCard;
            // If rerolled the selected card, deselect
            if (selectedIndex == index) selectedIndex = -1;
        }
    }

    void OnConfirmAll()
    {
        Debug.Log("[CoreChoiceUI] Continue clicked");

        // Unlock the selected card (if any)
        if (CardManager.Instance != null && selectedIndex >= 0 && selectedIndex < (cards?.Length ?? 0))
        {
            CardManager.Instance.SelectCard(selectedIndex);
        }

        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = 1f;

        // Unlock next room
        RoomManager.Instance?.OnCoreConfirmed();

        if (currentCore != null)
        {
            currentCore.OnChoicesConfirmed();
            Destroy(currentCore.gameObject);
        }
    }
}
