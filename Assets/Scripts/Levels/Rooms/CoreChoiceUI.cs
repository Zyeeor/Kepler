using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

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
    private int selectedIndex = -1;
    private float previousTimeScale = 1f;
    private bool gameplayInputWasBlocked;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockState;
    private float nextPointerDiagnosticTime;

    void Awake()
    {
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
        confirmAllButton?.onClick.AddListener(OnConfirmAll);
        if (confirmAllButton != null) confirmAllButton.interactable = true;
        Debug.Log($"[CoreChoiceUI] Awake: object='{name}', panelRoot={(panelRoot != null ? panelRoot.name : "NULL")}, cardPrefab={(cardPrefab != null ? cardPrefab.name : "NULL")}, cardParent={(cardParent != null ? cardParent.name : "NULL")}, confirmAll={(confirmAllButton != null ? confirmAllButton.name : "NULL")}, eventSystem={(EventSystem.current != null ? EventSystem.current.name : "NULL")}");
    }

    void Update()
    {
        if (panelRoot == null || !panelRoot.activeInHierarchy) return;
        if (Time.unscaledTime < nextPointerDiagnosticTime) return;

        nextPointerDiagnosticTime = Time.unscaledTime + 0.5f;
        LogPointerDiagnostics();
    }

    public void Show(RoomCore core)
    {
        currentCore = core;
        selectedIndex = -1;
        previousTimeScale = Time.timeScale;
        gameplayInputWasBlocked = PlayerController.IsGameplayInputBlocked;
        previousCursorVisible = Cursor.visible;
        previousCursorLockState = Cursor.lockState;

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
        Debug.Log($"[CoreChoiceUI] EventSystem={(EventSystem.current != null ? EventSystem.current.name : "NULL")}, generatedCards={CountGeneratedCards()}");
        if (panelRoot != null) panelRoot.SetActive(true);
        else Debug.LogError("[CoreChoiceUI] panelRoot is NULL — drag the UI Panel into this field!");

        // Re-bind confirm all
        if (confirmAllButton != null)
        {
            confirmAllButton.onClick.RemoveAllListeners();
            confirmAllButton.onClick.AddListener(OnConfirmAll);
            confirmAllButton.interactable = true;
        }

        PlayerController.SetGameplayInputBlocked(true, "CoreChoiceUI");
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 0f;
        Debug.Log($"[CoreChoiceUI] Gameplay paused for card selection. previousTimeScale={previousTimeScale:F2}, cursorUnlocked=true");
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

        // Deselect previous, select new
        if (selectedIndex >= 0 && selectedIndex < cards.Length && cards[selectedIndex] != null)
            cards[selectedIndex].SetSelected(false);
        cards[index].SetSelected(true);
        selectedIndex = index;
        Debug.Log($"[CoreChoiceUI] Card selected: index={index}, name={cards[index].name}");
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

        var newCard = CardManager.Instance.DrawOneReroll();
        if (newCard != null)
        {
            Sprite sprite = null;
            if (newCard.image != null) sprite = newCard.image;
            cards[index].Replace(newCard.cardName, sprite, newCard.description ?? "");
            CardManager.Instance.currentPicks[index] = newCard;
            // If rerolled the selected card, deselect
            if (selectedIndex == index) selectedIndex = -1;
            Debug.Log($"[CoreChoiceUI] Card rerolled: index={index}, name={newCard.cardName}");
        }
        else Debug.LogWarning($"[CoreChoiceUI] Reroll produced no card: index={index}");
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
        if (!gameplayInputWasBlocked)
            PlayerController.SetGameplayInputBlocked(false, "CoreChoiceUI");
        Time.timeScale = previousTimeScale;
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockState;
        Debug.Log($"[CoreChoiceUI] Gameplay resumed after card selection. restoredTimeScale={previousTimeScale:F2}, cursorRestored={previousCursorLockState}");

        // Unlock next room
        RoomManager.Instance?.OnCoreConfirmed();

        if (currentCore != null)
        {
            currentCore.OnChoicesConfirmed();
            Destroy(currentCore.gameObject);
        }
    }

    private int CountGeneratedCards()
    {
        if (cards == null) return 0;
        int count = 0;
        foreach (CoreChoiceCard card in cards)
            if (card != null) count++;
        return count;
    }

    private void LogPointerDiagnostics()
    {
        EventSystem eventSystem = EventSystem.current;
        Canvas canvas = GetComponent<Canvas>();
        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        string selected = eventSystem != null && eventSystem.currentSelectedGameObject != null
            ? eventSystem.currentSelectedGameObject.name
            : "NULL";

        Debug.Log($"[CoreChoiceUI] Pointer diagnostics: mouse={Input.mousePosition}, cursorVisible={Cursor.visible}, cursorLock={Cursor.lockState}, timeScale={Time.timeScale:F2}, panelActive={panelRoot.activeInHierarchy}, canvasActive={gameObject.activeInHierarchy}, canvasScale={transform.lossyScale}, raycaster={(raycaster != null ? raycaster.enabled.ToString() : "NULL")}, eventSystem={(eventSystem != null ? eventSystem.name : "NULL")}, pointerOverUI={(eventSystem != null && eventSystem.IsPointerOverGameObject())}, selected={selected}");

        if (eventSystem == null) return;

        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = Input.mousePosition
        };
        List<RaycastResult> results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerData, results);

        if (results.Count == 0)
        {
            Debug.LogWarning("[CoreChoiceUI] Pointer diagnostics: RaycastAll returned 0 UI hits.");
            return;
        }

        for (int i = 0; i < Mathf.Min(results.Count, 8); i++)
        {
            GameObject hit = results[i].gameObject;
            Button button = hit != null ? hit.GetComponentInParent<Button>() : null;
            Debug.Log($"[CoreChoiceUI] RaycastHit[{i}]: object='{(hit != null ? hit.name : "NULL")}', layer={(hit != null ? hit.layer.ToString() : "n/a")}, active={(hit != null && hit.activeInHierarchy)}, button={(button != null ? button.name : "NULL")}, buttonInteractable={(button != null && button.interactable)}");
        }
    }
}
