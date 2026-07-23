using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("GameOver UI")]
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;
    public Button restartButton;
    public TMP_Text restartButtonText;
    public Button homeButton;
    public TMP_Text homeButtonText;
    [Tooltip("Scene name to load when HOME is clicked.")]
    public string homeSceneName = "MainMenu";

    [Header("Pause")]
    public Button pauseButton;
    public TMP_Text pauseButtonText;
    private bool isPaused = false;

    [Header("Pause Menu Panel")]
    public GameObject pauseMenuPanel;
    public Button resumeButton;
    public TMP_Text resumeButtonText;
    public Button settingsButtonOnPause;
    public TMP_Text settingsButtonOnPauseText;
    public Button returnToMenuButton;
    public TMP_Text returnToMenuButtonText;

    [Header("GameOver Extended")]
    public Button settingsButtonOnGameOver;
    public TMP_Text settingsButtonOnGameOverText;
    public Button quitButtonOnGameOver;
    public TMP_Text quitButtonOnGameOverText;

    [Header("Health Bars")]
    public Button healthBarToggleButton;
    public TMP_Text healthBarToggleText;

    [Header("Settings & Confirm")]
    public SettingsPanel settingsPanel;
    public ConfirmDialog confirmDialog;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // Initialize sub-panels
        if (settingsPanel != null) settingsPanel.Init();
        if (confirmDialog != null) confirmDialog.Init();

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);

        if (homeButton != null)
            homeButton.onClick.AddListener(OnHomeClicked);

        if (pauseButton != null)
            pauseButton.onClick.AddListener(OnPauseClicked);

        if (healthBarToggleButton != null)
            healthBarToggleButton.onClick.AddListener(OnHealthBarToggleClicked);

        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(OnResumeClicked);

        if (settingsButtonOnPause != null)
            settingsButtonOnPause.onClick.AddListener(OnSettingsFromPause);

        if (returnToMenuButton != null)
            returnToMenuButton.onClick.AddListener(OnReturnToMenuClicked);

        if (settingsButtonOnGameOver != null)
            settingsButtonOnGameOver.onClick.AddListener(OnSettingsFromGameOver);

        if (quitButtonOnGameOver != null)
            quitButtonOnGameOver.onClick.AddListener(OnQuitFromGameOver);

        UpdatePauseButtonText();
        UpdateHealthBarToggleText();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 如果确认弹窗开着，关掉它
            if (confirmDialog != null && confirmDialog.IsVisible())
            {
                confirmDialog.Hide();
                return;
            }

            // 如果设置面板开着，关掉它
            if (settingsPanel != null && settingsPanel.IsVisible())
            {
                settingsPanel.Hide();
                return;
            }

            // GameOver 状态下不响应 ESC（由 GameOver 面板上的按钮处理）
            if (gameOverPanel != null && gameOverPanel.activeSelf)
                return;

            // 切换暂停状态
            TogglePause();
        }
    }

    public void ShowGameOver()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            Debug.Log("UIManager: GameOver panel shown");
        }

        if (gameOverText != null)
            gameOverText.text = "GAME OVER";

        // Show and unlock cursor so the player can click UI buttons
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Time.timeScale = 0f;
    }

    public void HideGameOver()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void OnRestartClicked()
    {
        Debug.Log("UIManager: Restart clicked - reloading scene");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnHomeClicked()
    {
        Debug.Log("UIManager: Home clicked - loading scene: " + homeSceneName);
        Time.timeScale = 1f;
        SceneManager.LoadScene(homeSceneName);
    }

    public void OnPauseClicked()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            Time.timeScale = 0f;
            Debug.Log("UIManager: Game paused");
        }
        else
        {
            Time.timeScale = 1f;
            Debug.Log("UIManager: Game resumed");
        }

        UpdatePauseButtonText();
    }

    void UpdatePauseButtonText()
    {
        if (pauseButtonText != null)
            pauseButtonText.text = isPaused ? "\u25B6" : "| |";
    }

    public void OnHealthBarToggleClicked()
    {
        Enemy.ShowHealthBars = !Enemy.ShowHealthBars;
        Debug.Log("Health bars: " + (Enemy.ShowHealthBars ? "ON" : "OFF"));
        UpdateHealthBarToggleText();
    }

    void UpdateHealthBarToggleText()
    {
        if (healthBarToggleText != null)
            healthBarToggleText.text = Enemy.ShowHealthBars ? "HP: ON" : "HP: OFF";
    }

    void TogglePause()
    {
        isPaused = !isPaused;

        if (isPaused)
            ShowPauseMenu();
        else
            HidePauseMenu();

        UpdatePauseButtonText();
    }

    void ShowPauseMenu()
    {
        Time.timeScale = 0f;
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Debug.Log("UIManager: Pause menu shown");
    }

    void HidePauseMenu()
    {
        Time.timeScale = 1f;
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        Debug.Log("UIManager: Pause menu hidden");
    }

    public void OnResumeClicked()
    {
        isPaused = false;
        HidePauseMenu();
        UpdatePauseButtonText();
        Debug.Log("UIManager: Resume clicked");
    }

    public void OnSettingsFromPause()
    {
        Debug.Log("UIManager: OnSettingsFromPause CALLED. settingsPanel null? " + (settingsPanel == null));
        if (settingsPanel != null)
        {
            // Hide pause menu underneath so settings sits clean on top
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            settingsPanel.Show();
            openedFromPause = true;
            Debug.Log("UIManager: Settings.Show() called. IsVisible=" + settingsPanel.IsVisible());
        }
    }

    private bool openedFromPause = false;
    private bool confirmOpenedFromPause = false;

    void LateUpdate()
    {
        // Reopen pause menu when settings/confirm closes and we came from pause
        if (openedFromPause && settingsPanel != null && !settingsPanel.IsVisible())
        {
            openedFromPause = false;
            if (isPaused && pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        }
        if (confirmOpenedFromPause && confirmDialog != null && !confirmDialog.IsVisible())
        {
            confirmOpenedFromPause = false;
            if (isPaused && pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        }
    }

    public void OnReturnToMenuClicked()
    {
        Debug.Log("UIManager: OnReturnToMenuClicked CALLED. confirmDialog null? " + (confirmDialog == null));
        if (confirmDialog != null)
        {
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            confirmOpenedFromPause = true;
            confirmDialog.Show("Return to Menu", "Are you sure? Current progress will be lost.", OnReturnToMenuConfirmed, null);
            Debug.Log("UIManager: ConfirmDialog.Show called. IsVisible=" + confirmDialog.IsVisible());
        }
        else
        {
            OnReturnToMenuConfirmed();
        }
    }

    private void OnReturnToMenuConfirmed()
    {
        Debug.Log("UIManager: Return to menu CONFIRMED - loading: " + homeSceneName);
        Time.timeScale = 1f;
        SceneManager.LoadScene(homeSceneName);
    }

    public void OnSettingsFromGameOver()
    {
        if (settingsPanel != null)
            settingsPanel.Show();
        Debug.Log("UIManager: Settings (from game over) opened");
    }

    public void OnQuitFromGameOver()
    {
        Debug.Log("UIManager: Quit from game over - loading: " + homeSceneName);
        Time.timeScale = 1f;
        SceneManager.LoadScene(homeSceneName);
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
