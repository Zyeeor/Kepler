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

    [Header("Bullet Time（子弹时间）")]
    [Tooltip("子弹时间的时间缩放倍率（全局单源：PossessionManager 触发的子弹时间亦读此值）。")]
    [Range(0.05f, 1f)] public float bulletTimeScale = 0.2f;

    public enum GameState
    {
        Soul,        // 灵魂态
        Possessed,   // 附体态
        BulletTime,  // 子弹时间
        GameOver
    }
    
    /// <summary>正式流程（屏蔽调试显示/先进主菜单）。供调试组件查询：调试组件在 Update/OnGUI 开头检查并跳过。</summary>
    public static bool IsFormalFlow => Instance != null && Instance.useFormalFlow;

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
        // 敌方注册表兜底清理：正常路径旧场景怪随卸载触发 OnDisable 逐个注销；
        // 极端情况（卸载异常/DDOL 提升）残留 fake-null，此处清空（幂等，新场景怪 OnEnable 重新注册）。
        EnemyRegistry.Clear();
        // 音频管理器丢失自愈（幂等）：常驻实例正常时无操作；若因极端时序被销毁，
        // 在场景加载完成后重建（此时 Start 的 DontDestroyOnLoad 可靠）。
        AudioManager.EnsureInstance();
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
        AudioManager.EnsureInstance();
        AudioSettingsManager.EnsureInstance();
        CombatHitboxDebugSettings.EnsureOnGameManager();
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
                TimeScaleManager.Push(TimeDomain.BulletTime, bulletTimeScale);   // 子弹时间（单源：bulletTimeScale 字段）
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