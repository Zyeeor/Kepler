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

    [Header("Tutorial（新人引导）")]
    [Tooltip("强制开启新人引导：开启后无论进入对局路径（直接 Play/读档/重开）与教学总开关如何，本局都触发新人引导（调试用）。")]
    public bool forceTutorial = false;

    [Header("Flow（流程）")]
    [Tooltip("启动进主菜单（与 useFormalFlow 相互独立）：开启后游戏启动先进主菜单（MainMenu），由主菜单进入对局；关闭则直接进入当前场景（调试模式）。配合 useFormalFlow=false 即为\"主界面开始 + 保留调试\"模式。")]
    public bool bootToMainMenu = false;
    [Tooltip("正式流程（屏蔽调试显示）：开启后屏蔽全部调试组件（F2/F4/F5/F6 面板、作弊提示、刷怪面板、调试相机等）。不再控制\"是否进主菜单\"——由 bootToMainMenu 单独决定。")]
    public bool useFormalFlow = false;

    public enum GameState
    {
        Soul,        // 灵魂态
        Possessed,   // 附体态
        BulletTime,  // 子弹时间
        GameOver
    }
    
    /// <summary>正式流程（屏蔽调试显示）。供调试组件查询：调试组件在 Update/OnGUI 开头检查并跳过。仅由 useFormalFlow 控制，与是否进主菜单（bootToMainMenu）无关。</summary>
    public static bool IsFormalFlow => Instance != null && Instance.useFormalFlow;

    /// <summary>强制开启新人引导（GameManager 调试开关，供 TutorialController 准入判定查询）。</summary>
    public static bool ForceTutorial => Instance != null && Instance.forceTutorial;

    /// <summary>
    /// 游戏状态变更事件（Kimi 评审断环：GameManager 不再直接调用各系统，改为广播；
    /// 订阅方如 PossessionManager 自取所需状态处理，并在自身销毁时退订）。
    /// </summary>
    public static event System.Action<GameState> OnStateChanged;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BulletTimeController.EnsureInstance();
            SceneManager.sceneLoaded += OnSceneLoaded;
            CombatHitboxDebugSettings.EnsureOnGameManager();

            // 全局音频管理器由常驻 GameManager 统一创建（各场景不挂 AudioManager，
            // 避免场景内实例与常驻单例多实例竞态）。SceneBgm 组件仍留在各场景中，
            // AudioManager 监听场景加载自动切曲。创建放在 Start：Awake 时场景加载未完成，
            // DontDestroyOnLoad 可能失效，导致常驻音频管理器随场景卸载被销毁。
            // 注：若 AudioManager 已被销毁而 Instance 残留（fake null），Unity 的 == 比较
            // 会返回 null，EnsureInstance 能正确重建新实例（Start 时序下 DDOL 可靠）。

            // 正式流程：游戏启动先进主菜单（而非直接进入对局场景）。
            // 注意仅在首次创建时跳转——主菜单点"开始/继续"二次进入对局场景时
            // Instance 已存在（DDOL 保留），不会重新跳转。
            // 进主菜单由 bootToMainMenu 或 useFormalFlow 任一为真触发（与屏蔽调试解耦）：
            // - bootToMainMenu=true & useFormalFlow=false → 主界面开始 + 保留调试（新需求）
            // - useFormalFlow=true                    → 主界面开始 + 屏蔽调试（原正式流程）
            if ((bootToMainMenu || useFormalFlow) && SceneManager.GetActiveScene().name != "MainMenu")
            {
                Debug.Log("[GameManager] 启动进主菜单。");
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
        // 字体由 FontApplier 独占处理 SceneManager.sceneLoaded，避免 GameManager 维护第二套扫描链路。
        // 敌方注册表兜底清理：正常路径旧场景怪随卸载触发 OnDisable 逐个注销；
        // 极端情况（卸载异常/DDOL 提升）残留 fake-null，此处清空（幂等，新场景怪 OnEnable 重新注册）。
        EnemyRegistry.Clear();
        // 音频管理器丢失自愈（幂等）：常驻实例正常时无操作；若因极端时序被销毁，
        // 在场景加载完成后重建（此时 Start 的 DontDestroyOnLoad 可靠）。
        AudioManager.EnsureInstance();
        AudioEventBinder.EnsureInstance(); // 音频事件订阅器自愈（同 AudioManager 生命周期）
        NarrativeScheduler.EnsureInstance();
        NarrativeEventBus.EnsureInstance();
        // 每次场景加载收敛 AudioListener（新场景相机可能带启用的监听器，且不止一个时需收敛）
        EnsureSingleAudioListener();
        // DDOL 玩家对象兜底清理（主界面幽灵 bug 防御面）：
        // - 进主菜单：只销毁无 Showcase 标记的残留 Player（bug 残留），保留正规展示灵魂
        // - 进对局场景：销毁所有 DDOL Player（含展示灵魂，防双 Player 静态 Instance 竞争）
        PurgeDdolSouls(keepShowcase: scene.name == "MainMenu");
    }

    /// <summary>
    /// 清理 DontDestroyOnLoad 场景中的玩家灵魂对象。正常路径玩家随对局场景卸载销毁，
    /// 出现在 DDOL 中的 Player 只可能来自：历史 bug 残留（被附身怪回池连带）或主菜单展示模式。
    /// </summary>
    static void PurgeDdolSouls(bool keepShowcase)
    {
        var souls = FindObjectsOfType<SoulActor>(true);
        foreach (var s in souls)
        {
            if (s == null || s.gameObject.scene.name != "DontDestroyOnLoad") continue;
            if (keepShowcase && s.GetComponent<SoulMenuShowcase>() != null) continue;
            Debug.LogWarning($"[GameManager] 清理 DDOL 残留玩家对象 '{s.gameObject.name}'（场景加载兜底）。");
            Destroy(s.gameObject);
        }
    }

    /// <summary>
    /// 确保仅保留一个启用的 AudioListener（FindObjectsOfType 顺序不稳定，不能按索引"保留第一个"：
    /// 那可能保留的是本就禁用的，把唯一启用的（如主相机）误禁掉，导致无声）。
    /// Start 与每次场景加载后调用。
    /// </summary>
    void EnsureSingleAudioListener()
    {
        var listeners = FindObjectsOfType<AudioListener>();
        bool kept = false;
        foreach (var l in listeners)
        {
            if (!kept && l.enabled) { kept = true; continue; }
            l.enabled = false;
        }
    }
    
    void Start()
    {
        // 全局字体统一应用：FontApplier 常驻并订阅场景加载，每场景加载后自动套用 FontRegistry 字体
        FontApplier.EnsureInstance();
        AudioManager.EnsureInstance();
        AudioEventBinder.EnsureInstance();
        AudioSettingsManager.EnsureInstance();
        CombatHitboxDebugSettings.EnsureOnGameManager();
        AudioDebugPanel.EnsureOnGameManager();
        NarrativeDebugPanel.EnsureOnGameManager();
        CardArchiveTracker.EnsureOnGameManager();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CardFaceBrowser.EnsureOnGameManager();
#endif
        soulTime = 15f;
        currentDrainRate = soulDrainRate;
        currentState = GameState.Soul;

        EnsureSingleAudioListener();
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
                TimeScaleManager.Pop(TimeDomain.BulletTime);   // 退出子弹时间（未开启时幂等）
                break;
            case GameState.Possessed:
                currentDrainRate = possessedDrainRate;
                TimeScaleManager.Pop(TimeDomain.BulletTime);
                break;
            case GameState.BulletTime:
                TimeScaleManager.Push(TimeDomain.BulletTime, BulletTimeController.ConfiguredTimeScale);
                break;
            case GameState.GameOver:
                ShowGameOverUI();
                // Run 级流程：失败打断边 → Failed（终态），UIManager 订阅后弹 GAME OVER 结算
                RunSession.EnsureInstance().TransitionTo(RunPhase.Failed);
                break;
        }
        OnStateChanged?.Invoke(newState);   // 广播状态变更（订阅方如 PossessionManager 自取所需状态处理）
        Debug.Log($"State: {newState}");
    }
    
    void ShowGameOverUI()
    {
        // 结算面板由 UIManager 订阅 RunFlow 阶段事件（Failed → 延迟 resultDelaySeconds 后 ShowResult(false)）驱动。
        // UIManager 存在时不本地立即显示（避免双重弹窗），仅停时间冻结战斗（死亡动画暂停展示）。
        if (UIManager.Instance != null)
        {
            TimeScaleManager.Push(TimeDomain.GameOver, 0f);
            return;
        }
        // UIManager 缺失时的本地回退显示
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            TimeScaleManager.Push(TimeDomain.GameOver, 0f);
            Debug.Log("GameOver panel displayed");

            // Show and unlock cursor so the player can click UI buttons
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            // 无任何面板可显示：放行时间（原意图保留；正常情况下此路径不可达）
            TimeScaleManager.Pop(TimeDomain.GameOver);
            Debug.LogWarning("GameOver panel reference is null!");
        }
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
        TimeScaleManager.ResetAll();   // 场景重开：清空全部时间请求，恢复 1
    }
}
