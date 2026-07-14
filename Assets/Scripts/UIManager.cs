using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("GameOver UI")]
    public GameObject gameOverPanel;
    public Text gameOverText;
    public Button restartButton;
    public Text restartButtonText;

    [Header("Pause")]
    public Button pauseButton;
    public Text pauseButtonText;
    private bool isPaused = false;

    [Header("Health Bars")]
    public Button healthBarToggleButton;
    public Text healthBarToggleText;

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
