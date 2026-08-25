using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class UIManager : SceneSingleton<UIManager>
{
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

    [Header("Soul Showcase (Main Menu)")]
    [Tooltip("回主菜单时是否把玩家灵魂带过去展示（主菜单背景后可移动，禁自然衰减）。开启即触发主界面主角展示效果。")]
    public bool bringSoulToMainMenu = true;

    [Header("Settings & Confirm")]
    public SettingsPanel settingsPanel;
    public ConfirmDialog confirmDialog;

    [Header("Result")]
    [Tooltip("结算面板（胜利/失败）延迟弹出秒数：留出时间看最终战况/死亡动画。")]
    [Min(0f)] public float resultDelaySeconds = 2f;

    /// <summary>结算挂起计数（>0 时延迟弹结算面板；First Clear 八步序列等"结算前演出"用）。</summary>
    public static int ResultSuspendCount;

    protected override void Awake()
    {
        base.Awake();   // 防重复注册（已有实例则销毁本对象）
        if (Instance != this) return;
        // 同步主菜单灵魂展示全局开关：主菜单场景没有 UIManager 实例（场景级单例），
        // 原生创建路径读不到其实例字段，需经静态桥接跨场景传递。
        SoulMenuShowcase.GlobalEnabled = bringSoulToMainMenu;
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

        // Run 级流程：订阅阶段事件驱动结算面板（不再由子系统直接调用 ShowResult）。
        // Result → VICTORY；Failed → GAME OVER。终态阶段不可再转移，无重复触发。
        RunSession.EnsureInstance().OnPhaseChanged += OnRunPhaseChanged;

        // UICanvas 下的全部场景文本由 FontRegistry 统一收敛；含 inactive 的暂停面板也会被处理。
        if (FontRegistry.Instance != null)
            FontRegistry.Instance.ApplyToTree(transform);
        ResolveButtonTextReferences();
        UpdatePauseButtonText();
        UpdateHealthBarToggleText();
        ApplyCatalogButtonTexts();
    }

    /// <summary>
    /// 兼容旧场景中未序列化 TMP_Text 字段的按钮：从按钮子树自动解析文案组件。
    /// 这样 TextCatalog 不依赖手工绑定，暂停面板也能统一接管。
    /// </summary>
    void ResolveButtonTextReferences()
    {
        if (restartButtonText == null) restartButtonText = FindButtonText(restartButton);
        if (homeButtonText == null) homeButtonText = FindButtonText(homeButton);
        if (pauseButtonText == null) pauseButtonText = FindButtonText(pauseButton);
        if (resumeButtonText == null) resumeButtonText = FindButtonText(resumeButton);
        if (settingsButtonOnPauseText == null) settingsButtonOnPauseText = FindButtonText(settingsButtonOnPause);
        if (returnToMenuButtonText == null) returnToMenuButtonText = FindButtonText(returnToMenuButton);
        if (settingsButtonOnGameOverText == null) settingsButtonOnGameOverText = FindButtonText(settingsButtonOnGameOver);
        if (quitButtonOnGameOverText == null) quitButtonOnGameOverText = FindButtonText(quitButtonOnGameOver);
        if (healthBarToggleText == null) healthBarToggleText = FindButtonText(healthBarToggleButton);
    }

    static TMP_Text FindButtonText(Button button)
    {
        return button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
    }

    /// <summary>按钮文案和字体统一走 TextCatalog + FontRegistry。</summary>
    void ApplyCatalogButtonTexts()
    {
        ApplyCatalogText(restartButtonText, "ui.gameover.restart");
        ApplyCatalogText(homeButtonText, "ui.gameover.home");
        ApplyCatalogText(resumeButtonText, "ui.pause.resume");
        ApplyCatalogText(settingsButtonOnPauseText, "ui.pause.settings");
        ApplyCatalogText(returnToMenuButtonText, "ui.pause.return_menu");
        ApplyCatalogText(settingsButtonOnGameOverText, "ui.gameover.settings");
        ApplyCatalogText(quitButtonOnGameOverText, "ui.gameover.quit");
    }

    static void ApplyCatalogText(TMP_Text text, string key)
    {
        if (text == null) return;
        text.text = TextCatalog.Get(key);
        var registry = FontRegistry.Instance;
        if (registry != null)
            registry.ApplyFontToText(text, FontSlots.Default);
    }

    /// <summary>RunFlow 阶段事件响应：终态弹结算面板（复用 GameOver 面板与 Restart/Home 按钮）。</summary>
    void OnRunPhaseChanged(RunPhase phase)
    {
        switch (phase)
        {
            case RunPhase.Result:
                StartResultDelay(true);
                break;
            case RunPhase.Failed:
                StartResultDelay(false);
                break;
            default:
                break; // 其他阶段不弹结算（Opening/Tutorial/Waves/Choice/Final 由各自系统驱动）
        }
    }

    /// <summary>
    /// 结算面板延迟弹出（胜利/失败共用，延迟秒数 resultDelaySeconds 可配置）：
    /// 让玩家看清最终战况/死亡动画后再弹窗。用 Realtime 等待，不受暂停/冻结影响。
    /// </summary>
    Coroutine resultDelayCoroutine;
    void StartResultDelay(bool won)
    {
        if (resultDelayCoroutine != null) StopCoroutine(resultDelayCoroutine); // 防重复触发
        resultDelayCoroutine = StartCoroutine(ShowResultDelayed(won));
    }

    System.Collections.IEnumerator ShowResultDelayed(bool won)
    {
        if (resultDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(resultDelaySeconds);
        // 结算挂起：等待 First Clear 等"结算前演出"结束（ResultSuspendCount 归零）
        while (ResultSuspendCount > 0)
            yield return null;
        resultDelayCoroutine = null;
        ShowResult(won);
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

            // 选卡会话进行中（含隐藏界面）：ESC 进暂停菜单，选卡保持，暂停后可继续选卡
            if (CoreChoiceUI.Instance != null && CoreChoiceUI.Instance.IsDrafting)
            {
                TogglePause();
                return;
            }

            // GameOver 状态下不响应 ESC（由 GameOver 面板上的按钮处理）
            if (gameOverPanel != null && gameOverPanel.activeSelf)
                return;

            // 切换暂停状态
            TogglePause();
        }
    }

    /// <summary>
    /// 终局结算面板（Result）：胜利/失败统一入口（复用 GameOver 面板与 Restart/Home 按钮）。
    /// won=true → 文本 VICTORY；false → GAME OVER。结束本局语义由按钮完成：
    /// Restart = 清档重开（EndRun + 重载场景），Home = 清档回主菜单（EndRun）。
    /// </summary>
    public void ShowResult(bool won)
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            Debug.Log($"UIManager: Result panel shown (won={won})");
        }

        if (gameOverText != null)
            gameOverText.text = won ? TextCatalog.Get("ui.result.victory") : TextCatalog.Get("ui.result.gameover");

        // Show and unlock cursor so the player can click UI buttons
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // 结算冻结（GameOver 域已由 GameManager Push；此处保险补 Pause 域，缺失 UI 场景下仍冻结）
        TimeScaleManager.Push(TimeDomain.Pause, 0f);
    }

    /// <summary>失败结算（GameManager 失败路径调用，委托 ShowResult(false)）。</summary>
    public void ShowGameOver()
    {
        ShowResult(false);
    }

    public void OnRestartClicked()
    {
        Debug.Log("UIManager: Restart clicked - ending run, reloading scene");
        // 重开 = 结束当前对局（清内存态+清存档），再重载场景开始新局
        RunSession.EnsureInstance().EndRun();
        // 重置常驻 GameManager 战斗状态：GameManager 为 DontDestroyOnLoad，
        // 场景重载不重建——不 Reset 则 currentState 停在 GameOver，新局附身会被 CanStartPossession 拒绝。
        // （ResetGame 内部已 TimeScaleManager.ResetAll 清空全部时间请求）
        if (GameManager.Instance != null) GameManager.Instance.ResetGame();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnHomeClicked()
    {
        Debug.Log("UIManager: Home clicked - ending run, loading scene: " + homeSceneName);
        // 终局返回：结束本局（清内存态 + 清存档）——失败/胜利后主菜单"继续"不再可用（正式语义：失败不续关）
        RunSession.EnsureInstance().EndRun();
        // 复位常驻 GameManager：不 Reset 则 currentState 停在 GameOver，主菜单"开始新游戏"后新局附身被拒
        if (GameManager.Instance != null) GameManager.Instance.ResetGame();
        TryEnterSoulShowcase();
        SceneManager.LoadScene(homeSceneName);
    }

    public void OnPauseClicked()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            TimeScaleManager.Push(TimeDomain.Pause, 0f);
            Debug.Log("UIManager: Game paused");
        }
        else
        {
            // 退出暂停：Pop 本 Pause 请求。若选卡会话仍在进行（CoreChoiceUI 自己也 Push 了 Pause 域），
            // 栈中仍有 Pause 请求 → 保持暂停；否则恢复 1。原 IsDraftingActive 特判由优先级栈天然消解。
            TimeScaleManager.Pop(TimeDomain.Pause);
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
            healthBarToggleText.text = Enemy.ShowHealthBars ? TextCatalog.Get("ui.toggle.hp_on") : TextCatalog.Get("ui.toggle.hp_off");
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
        // 暂停面板可能在首场景初始化时尚未经过全局扫描；打开前再次收敛引用、文案和字体。
        ResolveButtonTextReferences();
        ApplyCatalogButtonTexts();
        if (pauseMenuPanel != null && FontRegistry.Instance != null)
            FontRegistry.Instance.ApplyToTree(pauseMenuPanel.transform);

        TimeScaleManager.Push(TimeDomain.Pause, 0f);
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // 暂停期间把选卡界面 Canvas 降到暂停菜单之下：
        // 暂停菜单的全屏半透明遮罩（raycastTarget=true）会盖住并拦截选卡界面点击，
        // 因此不需要专门禁用 continue 按钮
        if (CoreChoiceUI.Instance != null)
            CoreChoiceUI.Instance.SetCanvasSortingOrder(-10);

        Debug.Log("UIManager: Pause menu shown");
    }

    void HidePauseMenu()
    {
        // 退出暂停菜单：Pop 本 Pause 请求（选卡会话的 Pause 由 CoreChoiceUI 独立 Push，互不干扰）
        TimeScaleManager.Pop(TimeDomain.Pause);
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        // 恢复选卡界面 Canvas 层级（暂停时被降到 -10）
        if (CoreChoiceUI.Instance != null)
            CoreChoiceUI.Instance.SetCanvasSortingOrder(100);

        // 游戏正常态光标可见（项目无锁定光标的游玩方式），恢复暂停时保持光标可见
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

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
            confirmDialog.Show("Return to Menu", "Return to menu? Progress is saved at each wave clear and can be continued from the main menu.", OnReturnToMenuConfirmed, null);
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
        TimeScaleManager.ResetAll();   // 回主菜单：清空全部时间请求
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
        Debug.Log("UIManager: Return to menu from game over - ending run, loading: " + homeSceneName);
        // 终局返回主界面：结束本局（清内存态 + 清存档）——失败/胜利后主菜单"继续"不再可用
        RunSession.EnsureInstance().EndRun();
        // 复位常驻 GameManager：不 Reset 则 currentState 停在 GameOver，主菜单"开始新游戏"后新局附身被拒
        if (GameManager.Instance != null) GameManager.Instance.ResetGame();
        TryEnterSoulShowcase();
        SceneManager.LoadScene(homeSceneName);
    }

    /// <summary>按开关把玩家灵魂带入主菜单展示模式（附身/飞行中自动跳过）。</summary>
    void TryEnterSoulShowcase()
    {
        if (bringSoulToMainMenu) SoulMenuShowcase.EnterShowcase();
    }

    protected override void OnDestroy()
    {
        TimeScaleManager.ResetAll();   // 场景卸载：清空时间请求（防 GameOver 冻结跨场景残留）
        // 退订 Run 级流程事件：RunSession 为常驻（DontDestroyOnLoad）对象，
        // 场景重载后旧 UIManager 若不退订，悬空委托会在事件触发时抛"对象已销毁"异常并中断委托链，
        // 导致后续订阅者（新 UIManager）收不到事件（结算面板不弹）。
        if (RunSession.Instance != null)
            RunSession.Instance.OnPhaseChanged -= OnRunPhaseChanged;
        base.OnDestroy();   // 清 Instance
    }
}
