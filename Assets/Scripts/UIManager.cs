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

    [Header("Health Bars")]
    public Button healthBarToggleButton;
    public TMP_Text healthBarToggleText;

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

        UpdatePauseButtonText();
        UpdateHealthBarToggleText();
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

    void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
