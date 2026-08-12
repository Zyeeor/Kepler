using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// 主菜单场景控制器。
/// 开始游戏 / 设置 / 退出游戏。
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Main Menu Buttons")]
    public Button startGameButton;
    public TMP_Text startGameButtonText;
    public Button settingsButton;
    public TMP_Text settingsButtonText;
    public Button quitGameButton;
    public TMP_Text quitGameButtonText;

    [Header("Scene Settings")]
    [Tooltip("开始游戏时加载的战斗场景名")]
    public string battleSceneName = "CombatTest";

    [Header("Sub Panels")]
    public SettingsPanel settingsPanel;
    public ConfirmDialog confirmDialog;

    [Header("Panels to hide when sub panel opens")]
    public GameObject mainMenuPanel;
    private bool subPanelOpened = false;

    void Start()
    {
        // Initialize sub-panels (they may start inactive, so their own Start won't run)
        if (settingsPanel != null) settingsPanel.Init();
        if (confirmDialog != null) confirmDialog.Init();

        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGame);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettings);

        if (quitGameButton != null)
            quitGameButton.onClick.AddListener(OnQuitGame);

        ShowCursor();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel != null && settingsPanel.IsVisible())
            {
                settingsPanel.Hide();
            }
            else if (confirmDialog != null && confirmDialog.IsVisible())
            {
                confirmDialog.Hide();
            }
        }
    }

    public void OnStartGame()
    {
        Debug.Log("MainMenu: Starting game - loading: " + battleSceneName);
        SceneManager.LoadScene(battleSceneName);
    }

    public void OnSettings()
    {
        Debug.Log("MainMenu: OnSettings CALLED. settingsPanel null? " + (settingsPanel == null));
        if (settingsPanel != null)
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            subPanelOpened = true;
            settingsPanel.Show();
            Debug.Log("MainMenu: Settings.Show() called. IsVisible=" + settingsPanel.IsVisible());
        }
    }

    public void OnQuitGame()
    {
        Debug.Log("MainMenu: OnQuitGame CALLED");
        if (confirmDialog != null)
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            subPanelOpened = true;
            confirmDialog.Show("Quit Game", "Are you sure you want to quit?", OnQuitConfirmed, OnDialogCancel);
        }
        else
        {
            OnQuitConfirmed();
        }
    }

    private void OnDialogCancel()
    {
        // callback when user hits Cancel or ESC
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        subPanelOpened = false;
    }

    void LateUpdate()
    {
        // Reopen main menu when sub panel closes
        if (subPanelOpened)
        {
            bool sVisible = settingsPanel != null && settingsPanel.IsVisible();
            bool cVisible = confirmDialog != null && confirmDialog.IsVisible();
            if (!sVisible && !cVisible)
            {
                if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
                subPanelOpened = false;
            }
        }
    }

    private void OnQuitConfirmed()
    {
        Debug.Log("MainMenu: Quitting game");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
