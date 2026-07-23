using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Horizontal 3-option choice UI for Room Core interaction.
/// Each option has a "Confirm" button and a "Reroll" (refresh) button.
/// Player can reroll each option once. Confirm all to close and resume.
/// </summary>
public class CoreChoiceUI : MonoBehaviour
{
    public static CoreChoiceUI Instance { get; private set; }

    [Header("UI Root")]
    public GameObject panelRoot;

    [Header("Choice Cards (root GameObjects with ChoiceCard component)")]
    public ChoiceCard card1;
    public ChoiceCard card2;
    public ChoiceCard card3;

    [Header("Option 1")]
    public TextMeshProUGUI option1Text;
    public Button option1Confirm;
    public Button option1Reroll;
    public GameObject option1ConfirmedMark;
    public GameObject option1RerolledMark;

    [Header("Option 2")]
    public TextMeshProUGUI option2Text;
    public Button option2Confirm;
    public Button option2Reroll;
    public GameObject option2ConfirmedMark;
    public GameObject option2RerolledMark;

    [Header("Option 3")]
    public TextMeshProUGUI option3Text;
    public Button option3Confirm;
    public Button option3Reroll;
    public GameObject option3ConfirmedMark;
    public GameObject option3RerolledMark;

    [Header("Global")]
    public Button confirmAllButton;
    public TextMeshProUGUI titleText;

    // State
    private RoomCore currentCore;
    private bool[] confirmed = new bool[3];
    private bool[] rerolled = new bool[3];
    private string[] currentOptions = new string[3] { "Option A", "Option B", "Option C" };

    void Awake()
    {
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);

        // Option 1
        option1Confirm?.onClick.AddListener(() => ConfirmOption(0));
        option1Reroll?.onClick.AddListener(() => RerollOption(0));

        // Option 2
        option2Confirm?.onClick.AddListener(() => ConfirmOption(1));
        option2Reroll?.onClick.AddListener(() => RerollOption(1));

        // Option 3
        option3Confirm?.onClick.AddListener(() => ConfirmOption(2));
        option3Reroll?.onClick.AddListener(() => RerollOption(2));

        // Confirm all
        confirmAllButton?.onClick.AddListener(OnConfirmAll);
        if (confirmAllButton != null) confirmAllButton.interactable = false;
    }

    public void Show(RoomCore core)
    {
        currentCore = core;
        for (int i = 0; i < 3; i++) { confirmed[i] = false; rerolled[i] = false; }

        // TODO: Replace with real option generation logic
        currentOptions[0] = "Option A";
        currentOptions[1] = "Option B";
        currentOptions[2] = "Option C";

        RefreshUI();
        if (panelRoot != null) panelRoot.SetActive(true);
        // Re-bind in case Inspector OnClick overrides code binding
        if (confirmAllButton != null)
        {
            confirmAllButton.onClick.RemoveAllListeners();
            confirmAllButton.onClick.AddListener(OnConfirmAll);
        }

        // Pause game while choosing
        Time.timeScale = 0f;
    }

    void RefreshUI()
    {
        if (option1Text != null) option1Text.text = currentOptions[0];
        if (option2Text != null) option2Text.text = currentOptions[1];
        if (option3Text != null) option3Text.text = currentOptions[2];

        UpdateOptionButtons(0, option1Confirm, option1Reroll, option1ConfirmedMark, option1RerolledMark);
        UpdateOptionButtons(1, option2Confirm, option2Reroll, option2ConfirmedMark, option2RerolledMark);
        UpdateOptionButtons(2, option3Confirm, option3Reroll, option3ConfirmedMark, option3RerolledMark);

        // Confirm all is always interactable (skip confirm/reroll check)
        if (confirmAllButton != null) confirmAllButton.interactable = true;
    }

    void UpdateOptionButtons(int idx, Button confirmBtn, Button rerollBtn, GameObject confirmMark, GameObject rerollMark)
    {
        if (confirmed[idx])
        {
            if (confirmBtn != null) confirmBtn.interactable = false;
            if (rerollBtn != null) rerollBtn.interactable = false;
            if (confirmMark != null) confirmMark.SetActive(true);
            if (rerollMark != null) rerollMark.SetActive(false);
        }
        else if (rerolled[idx])
        {
            if (confirmBtn != null) confirmBtn.interactable = false;
            if (rerollBtn != null) rerollBtn.interactable = false;
            if (confirmMark != null) confirmMark.SetActive(false);
            if (rerollMark != null) rerollMark.SetActive(true);
        }
        else
        {
            if (confirmBtn != null) confirmBtn.interactable = true;
            if (rerollBtn != null) rerollBtn.interactable = !rerolled[idx];
            if (confirmMark != null) confirmMark.SetActive(false);
            if (rerollMark != null) rerollMark.SetActive(false);
        }
    }

    void ConfirmOption(int idx)
    {
        if (confirmed[idx] || rerolled[idx]) return;
        confirmed[idx] = true;
        RefreshUI();
    }

    void RerollOption(int idx)
    {
        if (confirmed[idx] || rerolled[idx]) return;
        rerolled[idx] = true;
        // TODO: Generate a new random option for this slot
        currentOptions[idx] = "Rerolled " + (char)('A' + idx);
        RefreshUI();
    }

    void OnConfirmAll()
    {
        Debug.Log("[CoreChoiceUI] Confirm All clicked");
        // TODO: Apply chosen effects based on confirmed options

        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = 1f;

        if (currentCore != null)
        {
            currentCore.OnChoicesConfirmed();
            Destroy(currentCore.gameObject);
        }
    }
}
