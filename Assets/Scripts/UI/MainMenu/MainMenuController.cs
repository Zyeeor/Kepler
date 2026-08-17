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
    [Tooltip("继续按钮：存在存档时可点（无存档自动置灰）。点击后加载 battleSceneName 并恢复波次间存档。")]
    public Button continueGameButton;
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

        // 继续按钮：有存档才可点（每帧监听存档文件状态）
        if (continueGameButton != null)
        {
            continueGameButton.onClick.AddListener(OnContinueGame);
            continueGameButton.interactable = SaveCoordinator.HasSaveFile;
        }

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
        // 已有存档时先确认：开始新游戏会覆盖原存档
        if (SaveCoordinator.HasSaveFile && confirmDialog != null)
        {
            Debug.Log("MainMenu: OnStartGame - save exists, showing confirm");
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            subPanelOpened = true;
            confirmDialog.Show("Start New Game", "Starting a new game will overwrite your saved progress. Continue?", OnStartNewGameConfirmed, OnDialogCancel);
        }
        else
        {
            OnStartNewGameConfirmed();
        }
    }

    private void OnStartNewGameConfirmed()
    {
        Debug.Log("MainMenu: Starting game - loading: " + battleSceneName);
        // 新游戏：开启新对局会话（随机种子 + 清旧存档），场景对象据此初始化
        RunSession.EnsureInstance().BeginNewRun();
        SceneManager.LoadScene(battleSceneName);
    }

    public void OnContinueGame()
    {
        Debug.Log("MainMenu: Continuing game - loading: " + battleSceneName);
        // 继续：读档恢复会话（失败则留在主菜单，按钮已按有无存档置灰）
        if (RunSession.EnsureInstance().LoadFromSave())
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
