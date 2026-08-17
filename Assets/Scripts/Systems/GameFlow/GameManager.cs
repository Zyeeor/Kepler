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

    [Header("World Seed（地图种子）")]
    [Tooltip("是否使用固定种子：开启后新对局使用固定Seed（便于复现特定地图/调试）；关闭则每局随机。")]
    public bool useFixedSeed = false;
    [Tooltip("固定种子值（仅 useFixedSeed=true 时生效）。")]
    public uint fixedSeed = 12345;

    [Header("Flow（流程）")]
    [Tooltip("正式流程开关：开启后游戏启动先进主菜单（MainMenu），由主菜单进入对局；同时屏蔽调试显示（F2 面板/作弊提示/刷怪面板）。关闭则直接进入当前场景（调试模式）。")]
    public bool useFormalFlow = false;

    [Header("Test")]
    [Tooltip("测试开关：开局自动触发一次双选选卡弹窗（验证双选/暂停/ESC 交互）。")]
    public bool testDoublePickOnStart = true;
    
    public enum GameState
    {
        Soul,        // 灵魂态
        Possessed,   // 附体态
        BulletTime,  // 子弹时间
        GameOver
    }
    
    /// <summary>正式流程（屏蔽调试显示/先进主菜单）。供调试组件查询：调试组件在 Update/OnGUI 开头检查并跳过。</summary>
    public static bool IsFormalFlow => Instance != null && Instance.useFormalFlow;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;

            // 正式流程：游戏启动先进主菜单（而非直接进入对局场景）。
            // 注意仅在首次创建时跳转——主菜单点"开始/继续"二次进入对局场景时
            // Instance 已存在（DDOL 保留），不会重新跳转。
            if (useFormalFlow && SceneManager.GetActiveScene().name != "MainMenu")
            {
                Debug.Log("[GameManager] 正式流程：启动先进主菜单。");
                SceneManager.LoadScene("MainMenu");
            }
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 对局状态归 RunSession 管（新局/继续/重开由会话决定初始值），
        // 本层只负责系统级常驻，不再无条件重置（否则"返回主菜单再进入"会清空进度）。
        Debug.Log($"GameManager: Scene loaded - {scene.name}");
    }
    
    void Start()
    {
        soulTime = 15f;
        currentDrainRate = soulDrainRate;
        currentState = GameState.Soul;

        // Ensure only one AudioListener exists
        var listeners = FindObjectsOfType<AudioListener>();
        for (int i = 1; i < listeners.Length; i++)
            listeners[i].enabled = false;

        // 测试开关：开局自动触发一次双选选卡弹窗（验证双选/暂停/ESC 交互）
        if (testDoublePickOnStart)
            StartCoroutine(TriggerTestDoublePick());
    }

    // 测试用：延迟两帧确保 CoreChoiceUI.Instance 已就绪后，触发双选弹窗
    System.Collections.IEnumerator TriggerTestDoublePick()
    {
        yield return null;          // 等一帧，确保 CoreChoiceUI.Instance 已 Awake
        yield return null;          // 再等一帧，确保场景完全就绪
        var ui = CoreChoiceUI.Instance;
        Debug.Log($"[Test] TriggerTestDoublePick: CoreChoiceUI.Instance={(ui != null ? "found" : "NULL")}");
        if (ui != null)
        {
            ui.Show(onClosed: () => Debug.Log("[Test] DoublePick closed"), doublePick: true);
        }
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
                // 附身编排防御：GameOver 时显式终止飞行协程/附身态，防止协程用 unscaledDeltaTime 继续推进覆盖 GameOver
                if (PossessionManager.Instance != null) PossessionManager.Instance.OnGameOver();
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

            // Show and unlock cursor so the player can click UI buttons
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
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