using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [Header("Time System")]
    public float soulTime = 15f;              // 灵魂初始时间
    public float maxSoulTime = 30f;
    public float soulDrainRate = 1f;          // 灵魂态流速
    public float possessedDrainRate = 0.7f;   // 附体态流速
    public float currentDrainRate;
    
    [Header("Game State")]
    public GameState currentState = GameState.Soul;
    public float gameTimer;

    [Header("GameOver UI")]
    public GameObject gameOverPanel;
    
    public enum GameState
    {
        Soul,        // 灵魂态
        Possessed,   // 附体态
        BulletTime,  // 子弹时间
        GameOver
    }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"GameManager: Scene loaded - {scene.name}, resetting state");
        ResetGame();
    }
    
    void Start()
    {
        soulTime = 15f;
        currentDrainRate = soulDrainRate;
        currentState = GameState.Soul;
    }
    
    void Update()
    {
        if (currentState == GameState.GameOver) return;
        
        // Soul time drains over time (but does NOT trigger GameOver)
        // GameOver is ONLY triggered by PlayerHealth reaching zero
        if (currentState == GameState.Soul)
        {
            soulTime -= currentDrainRate * Time.deltaTime;
            if (soulTime < 0) soulTime = 0;
        }
        gameTimer += Time.deltaTime;
    }
    
    public void AddTime(float seconds)
    {
        soulTime = Mathf.Min(soulTime + seconds, maxSoulTime);
        Debug.Log($"+{seconds}s, Current Time: {soulTime:F1}s");
    }
    
    public void SpendTime(float seconds)
    {
        soulTime -= seconds;
        Debug.Log($"-{seconds}s, Current Time: {soulTime:F1}s");
    }
    
    public void SwitchState(GameState newState)
    {
        currentState = newState;
        switch (newState)
        {
            case GameState.Soul:
                currentDrainRate = soulDrainRate;
                Time.timeScale = 1f;
                break;
            case GameState.Possessed:
                currentDrainRate = possessedDrainRate;
                Time.timeScale = 1f;
                break;
            case GameState.BulletTime:
                Time.timeScale = 0.2f;   // 子弹时间
                break;
            case GameState.GameOver:
                ShowGameOverUI();
                break;
        }
        Debug.Log($"State: {newState}");
    }
    
    void ShowGameOverUI()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            Time.timeScale = 0f;
            Debug.Log("GameOver panel displayed");
        }
        else
        {
            Time.timeScale = 1f;
            Debug.LogWarning("GameOver panel reference is null!");
        }
    }

    void GameOver()
    {
        currentState = GameState.GameOver;
        Debug.Log("GAME OVER - Soul time depleted!");
        ShowGameOverUI();
    }

    /// <summary>
    /// Reset game state for scene restart.
    /// Called by UIManager.OnRestartClicked() before reloading the scene.
    /// </summary>
    public void ResetGame()
    {
        Debug.Log("GameManager: Resetting game state for restart");
        soulTime = 15f;
        gameTimer = 0f;
        currentDrainRate = soulDrainRate;
        currentState = GameState.Soul;
        Time.timeScale = 1f;
    }
}