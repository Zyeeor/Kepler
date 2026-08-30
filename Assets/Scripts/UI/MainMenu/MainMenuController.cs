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
    public string battleSceneName = "EnemyAiTest";
    [Tooltip("Boss 模式加载的正式战斗场景名。Boss 模式会在该场景中直接进入 Boss 阶段。")]
    public string bossBattleSceneName = "EnemyAiTest";
    [Min(0)]
    [Tooltip("Boss 模式开局给七种罪印的层数。七种罪印统一使用该值，改动后无需修改代码。")]
    public int bossModeInitialImprintStacks = 20;

    [Header("Soul Showcase (Main Menu)")]
    [Tooltip("主菜单原生展示灵魂 prefab（Player.prefab）。游戏启动直进主菜单时实例化，让主角一开始就在主界面（背景后可移动）。")]
    public GameObject soulShowcasePrefab;
    [Tooltip("原生展示灵魂出生位置（世界坐标；仅当场景中还没有灵魂时生效）。")]
    public Vector3 soulSpawnPosition = new Vector3(0f, 1f, 8f);

    [Header("Sub Panels")]
    public SettingsPanel settingsPanel;
    public ConfirmDialog confirmDialog;

    [Header("Hall of Fame (auto-created)")]
    [Tooltip("荣誉殿堂面板（纯代码 UI，Start 时自动创建；留空自动生成，无需场景配置）。")]
    public HallOfFamePanel hallOfFamePanel;
    Button hallOfFameButton;

    [Header("Card Archive (auto-created)")]
    [Tooltip("卡牌图鉴面板（纯代码 UI，Start 时自动创建；留空自动生成）。")]
    public CardArchivePanel cardArchivePanel;
    [Tooltip("图鉴入口按钮。留空则自动查找场景中名为 CardArchiveButton 的按钮，再找不到才克隆设置按钮。")]
    public Button cardArchiveButton;

    [Header("Panels to hide when sub panel opens")]
    public GameObject mainMenuPanel;

    [Header("Play Panel（点 Play 后打开的二级面板）")]
    [Tooltip("主面板上的 Play 入口按钮。点击后隐藏主面板并打开 Play Panel。")]
    public Button playButton;
    [Tooltip("Play Panel 根节点。留空则运行时自动创建（与 mainMenuPanel 同级、同区域）。")]
    public GameObject playPanel;
    [Tooltip("Play Panel 内的按钮容器（自动垂直布局）。留空则使用 playPanel 自身。")]
    public Transform playPanelButtonRoot;
    [Tooltip("Play Panel 的返回按钮（回到主面板）。留空则运行时自动创建。")]
    public Button playPanelBackButton;

    private bool subPanelOpened = false;
    private bool playPanelOpened = false;
    private Button bossModeButton;

    void Start()
    {
        // 主界面灵魂展示：对局带回的灵魂已在（DDOL），否则创建原生展示灵魂——
        // 保证"对局结束前后都在"，游戏启动一开始主界面就有主角。
        SoulMenuShowcase.SpawnNativeShowcase(soulShowcasePrefab, soulSpawnPosition);

        // Initialize sub-panels (they may start inactive, so their own Start won't run)
        if (settingsPanel != null) settingsPanel.Init();
        if (confirmDialog != null) confirmDialog.Init();

        // 新游戏按钮（Play Panel 内）：直接触发新游戏，不再经过"普通模式"选择层
        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveAllListeners();
            startGameButton.onClick.AddListener(OnStartGame);
        }

        // 继续按钮：有存档才可点
        if (continueGameButton != null)
        {
            continueGameButton.onClick.RemoveAllListeners();
            continueGameButton.onClick.AddListener(OnContinueGame);
            continueGameButton.interactable = SaveCoordinator.HasSaveFile;
        }

        if (settingsButton != null)
        {
            // 设置按钮只负责音量/设置面板：先清残留监听，避免与其它入口（荣誉殿堂/图鉴）串台
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(OnSettings);
        }

        if (quitGameButton != null)
        {
            quitGameButton.onClick.RemoveAllListeners();
            quitGameButton.onClick.AddListener(OnQuitGame);
        }

        // 按钮文案统一走 TextCatalog（场景 TMP 英文初值仅作兜底）
        if (startGameButtonText != null) startGameButtonText.text = TextCatalog.Get("ui.mainmenu.new_game");
        if (continueGameButton != null)
        {
            var t = continueGameButton.GetComponentInChildren<TMPro.TMP_Text>();
            if (t != null) t.text = TextCatalog.Get("ui.mainmenu.continue_last_game");
        }
        if (settingsButtonText != null) settingsButtonText.text = TextCatalog.Get("ui.mainmenu.settings");
        if (quitGameButtonText != null) quitGameButtonText.text = TextCatalog.Get("ui.mainmenu.quit");

        EnsureHallOfFameEntry();
        EnsureCardArchiveEntry();
        // Play Panel：把「新游戏/继续上次游戏/荣誉殿堂/卡牌图鉴」四个入口收进去，
        // 主面板只保留 Play 入口（+ Boss模式 / 设置 / 退出）。
        EnsurePlayPanel();
        EnsurePlayButton();
        ShowMainPanel();

        ShowCursor();
    }

    // ───────────────── Play Panel（二级面板） ─────────────────

    /// <summary>显示主面板（首屏）：Play Panel 收起，主面板只留 Play / Boss模式 / 设置 / 退出。</summary>
    void ShowMainPanel()
    {
        playPanelOpened = false;
        if (playPanel != null) playPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (bossModeButton != null) bossModeButton.gameObject.SetActive(true);
    }

    /// <summary>
    /// 确保 Play Panel 就绪：容器缺失时自动创建（与 mainMenuPanel 同级、同区域），
    /// 把四个游戏入口按钮收进面板并做垂直布局，最后默认隐藏。
    /// </summary>
    void EnsurePlayPanel()
    {
        if (playPanel == null)
        {
            Transform parent = mainMenuPanel != null ? mainMenuPanel.transform.parent : transform;
            var go = new GameObject("PlayPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            playPanel = go;
        }

        // 置顶：Play Panel 要盖住首屏（PlayVideoPanel 等），否则下层按钮会透出来与面板按钮重叠。
        playPanel.transform.SetAsLastSibling();

        if (playPanelButtonRoot == null)
            playPanelButtonRoot = playPanel.transform;

        // 四个游戏入口：新游戏 / 继续上次游戏 / 荣誉殿堂 / 卡牌图鉴
        MoveToPlayPanel(startGameButton);
        MoveToPlayPanel(continueGameButton);
        MoveToPlayPanel(hallOfFameButton);
        MoveToPlayPanel(cardArchiveButton);

        playPanel.SetActive(false);
    }

    void MoveToPlayPanel(Button button)
    {
        if (button == null || playPanelButtonRoot == null) return;
        if (button.transform.parent == playPanelButtonRoot) return;
        // 不再自动添加/调整任何 LayoutGroup：按钮沿用自身 RectTransform（anchor/pivot/anchoredPosition），
        // 布局完全交给场景或策划在 playPanel 上配置的布局组件决定。
        button.transform.SetParent(playPanelButtonRoot, false);
        button.gameObject.SetActive(true);
    }

    void EnsurePlayButton()
    {
        if (playButton != null) return;

        // 只复用场景里已摆放的 Play 按钮（如 MainCanvas/PlayVideoPanel/Play）。
        // 不再自动克隆——只有场景里实际配置的 button 才会成为 Play 入口。
        var existing = FindButtonByName("Play", true);
        if (existing != null)
        {
            playButton = existing;
            existing.gameObject.SetActive(true);
            existing.onClick.RemoveAllListeners();
            existing.onClick.AddListener(OpenPlayPanel);
            if (FontRegistry.Instance != null)
                FontRegistry.Instance.ApplyToTree(existing.transform);
            return;
        }

        Debug.LogWarning("[MainMenu] 场景中未找到名为 'Play' 的按钮。Play 入口未配置，请在 MainMenuController.playButton 字段或场景里拖入。");
    }

    /// <summary>点 Play：关掉主面板，打开 Play Panel（四个游戏入口）。</summary>
    public void OpenPlayPanel()
    {
        // 先关主面板再置位 playPanelOpened：HideActivePanel 依赖该标志判断"当前活动面板"，
        // 若先置位会把即将打开的 Play Panel 又关掉、而主面板没收起（两面板同时显示）。
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        playPanelOpened = true;
        // Play 入口本身也收起（它可能挂在首屏 PlayVideoPanel 下，不随 mainMenuPanel 隐藏）
        if (playButton != null) playButton.gameObject.SetActive(false);
        if (playPanel != null) playPanel.SetActive(true);
        if (continueGameButton != null)
            continueGameButton.interactable = SaveCoordinator.HasSaveFile;
        ShowCursor();
    }

    /// <summary>Play Panel 返回：关掉 Play Panel，恢复主面板与 Play 入口。</summary>
    public void ClosePlayPanel()
    {
        playPanelOpened = false;
        if (playPanel != null) playPanel.SetActive(false);
        if (playButton != null) playButton.gameObject.SetActive(true);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        ShowCursor();
    }

    /// <summary>子面板（设置/确认框/荣誉殿堂/图鉴）打开时隐藏当前活动面板（主面板或 Play Panel）。</summary>
    void HideActivePanel()
    {
        if (playPanelOpened && playPanel != null) playPanel.SetActive(false);
        else if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
    }

    /// <summary>
    /// 荣誉殿堂入口：克隆设置按钮生成（与主菜单美术按钮同风格、零场景编辑），
    /// 面板本体由 HallOfFamePanel Prefab 作为独立 Overlay 创建。
    /// </summary>
    void EnsureHallOfFameEntry()
    {
        if (hallOfFamePanel == null) hallOfFamePanel = HallOfFamePanel.EnsureInstance();

        // 优先使用场景中已摆放的 HonorButton（策划自制），避免克隆设置按钮造成监听错配
        var existing = FindButtonByName("HonorButton", true);
        if (existing != null)
        {
            hallOfFameButton = existing;
            existing.gameObject.SetActive(true);
            existing.onClick.RemoveAllListeners();
            existing.onClick.AddListener(OnHallOfFame);
            return;
        }

        if (hallOfFameButton != null || settingsButton == null) return;

        var clone = Instantiate(settingsButton.gameObject, settingsButton.transform.parent);
        clone.name = "HallOfFameButton";
        clone.SetActive(true);
        if (FontRegistry.Instance != null)
            FontRegistry.Instance.ApplyToTree(clone.transform);
        hallOfFameButton = clone.GetComponent<Button>();
        if (hallOfFameButton != null)
        {
            hallOfFameButton.onClick.RemoveAllListeners();
            hallOfFameButton.onClick.AddListener(OnHallOfFame);
        }
        var label = clone.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = TextCatalog.Get("ui.mainmenu.hall_of_fame");
        clone.transform.SetSiblingIndex(settingsButton.transform.GetSiblingIndex() + 1);
    }

    public void OnHallOfFame()
    {
        if (hallOfFamePanel == null) hallOfFamePanel = HallOfFamePanel.EnsureInstance();
        HideActivePanel();
        subPanelOpened = true;
        hallOfFamePanel.Show();
    }

    /// <summary>
    /// 卡牌图鉴入口：面板由 CardArchivePanel Prefab 作为独立 Overlay 创建；
    /// 入口按钮优先使用场景中已摆放的 CardArchiveButton，找不到时克隆设置按钮兜底。
    /// </summary>
    void EnsureCardArchiveEntry()
    {
        EnsureCardArchivePanel();

        // 与 EnsureHallOfFameEntry 完全一致的写法：不依赖缓存的 cardArchiveButton 字段做提前
        // return，每次都重新按名字在场景里查找 CardArchiveButton 并重新挂监听器。Play 模式下脚本
        // 热重载只会保留 public 字段的序列化引用，运行时 onClick 监听器（非持久化）不会跟着存活，
        // 之前"引用还在但点了没反应"就是因为提前 return 跳过了重新挂监听器这一步。
        var existing = FindButtonByName("CardArchiveButton", true);
        if (existing != null)
        {
            cardArchiveButton = existing;
            existing.gameObject.SetActive(true);
            existing.onClick.RemoveAllListeners();
            existing.onClick.AddListener(OnCardArchive);
            var lbl = existing.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null && string.IsNullOrEmpty(lbl.text))
                lbl.text = TextCatalog.Get("ui.mainmenu.card_archive");
            if (FontRegistry.Instance != null)
                FontRegistry.Instance.ApplyToTree(existing.transform);
            return;
        }

        // 2) 兜底：克隆设置按钮
        if (cardArchiveButton != null || settingsButton == null) return;

        var clone = Instantiate(settingsButton.gameObject, settingsButton.transform.parent);
        clone.name = "CardArchiveButton";
        clone.SetActive(true);
        if (FontRegistry.Instance != null)
            FontRegistry.Instance.ApplyToTree(clone.transform);
        cardArchiveButton = clone.GetComponent<Button>();
        if (cardArchiveButton != null)
        {
            cardArchiveButton.onClick.RemoveAllListeners();
            cardArchiveButton.onClick.AddListener(OnCardArchive);
        }
        var label = clone.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = TextCatalog.Get("ui.mainmenu.card_archive");
        clone.transform.SetSiblingIndex(settingsButton.transform.GetSiblingIndex() + 2);
    }

    /// <summary>按名字查找按钮：先查激活对象，再遍历含未激活的场景对象。</summary>
    static Button FindButtonByName(string goName, bool includeInactive)
    {
        var go = GameObject.Find(goName);
        if (go != null) return go.GetComponent<Button>();

        if (!includeInactive) return null;

        // 未激活对象无法被 GameObject.Find 找到，遍历场景中所有 Button
        foreach (var b in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (b.gameObject.name == goName) return b;
        }
        return null;
    }

    /// <summary>从 Resources/SystemUI/CardArchivePanel Prefab 创建独立 Overlay 图鉴。</summary>
    void EnsureCardArchivePanel()
    {
        if (cardArchivePanel != null && cardArchivePanel.enabled) return;
        cardArchivePanel = CardArchivePanel.EnsureInstance();
    }

    public void OnCardArchive()
    {
        EnsureCardArchivePanel();   // 缺失时按需补挂（如场景切换后宿主被销毁）
        if (cardArchivePanel == null) return;
        HideActivePanel();
        subPanelOpened = true;
        cardArchivePanel.onClose = OnCardArchiveClosed;
        cardArchivePanel.Show();
    }

    void OnCardArchiveClosed()
    {
        RestoreActivePanel();
        ShowCursor();
    }

    /// <summary>子面板关闭后恢复来源面板：从 Play Panel 打开的回 Play Panel，否则回主面板。</summary>
    void RestoreActivePanel()
    {
        subPanelOpened = false;
        if (playPanel != null) playPanel.SetActive(playPanelOpened);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(!playPanelOpened);
    }

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!GameManager.IsFormalFlow && Input.GetKeyDown(KeyCode.F8))
        {
            EnsureCardArchivePanel();
            if (cardArchivePanel != null)
            {
                cardArchivePanel.EnableDebugPreviewData();
                OnCardArchive();
            }
        }
        if (!GameManager.IsFormalFlow && Input.GetKeyDown(KeyCode.F9) && cardArchivePanel != null)
            cardArchivePanel.DisableDebugPreviewData();
#endif

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
            else if (cardArchivePanel != null && cardArchivePanel.IsVisible())
            {
                cardArchivePanel.Hide();
            }
            else if (hallOfFamePanel != null && hallOfFamePanel.IsVisible())
            {
                hallOfFamePanel.Hide();
            }
            else if (playPanelOpened)
            {
                // Play Panel 打开时 ESC 返回主面板
                ClosePlayPanel();
            }
        }
    }

    static void SetButtonLabel(Button button, string label)
    {
        if (button == null) return;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;
    }

    public void OnStartGame()
    {
        // 已有存档时先确认：开始新游戏会覆盖原存档（文本走 TextCatalog 统一管理）
        if (SaveCoordinator.HasSaveFile && confirmDialog != null)
        {
            Debug.Log("MainMenu: OnStartGame - save exists, showing confirm");
            HideActivePanel();
            subPanelOpened = true;
            confirmDialog.Show(TextCatalog.Get("ui.mainmenu.newgame_title"), TextCatalog.Get("ui.mainmenu.newgame_message"), OnStartNewGameConfirmed, OnDialogCancel);
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
        SoulMenuShowcase.ExitShowcase(); // 展示灵魂随主菜单卸载销毁（须在 LoadScene 前，防双 Player 实例竞争）
        SceneManager.LoadScene(battleSceneName);
    }

    /// <summary>Boss 模式不经过普通模式确认框，直接创建 Boss 对局并进入正式场景。</summary>
    public void OnStartBossGame()
    {
        Debug.Log("MainMenu: Starting Boss mode - loading: " + bossBattleSceneName);
        RunSession.EnsureInstance().BeginBossRun(bossModeInitialImprintStacks);
        SoulMenuShowcase.ExitShowcase();
        SceneManager.LoadScene(bossBattleSceneName);
    }

    public void OnContinueGame()
    {
        Debug.Log("MainMenu: Continuing game - loading: " + battleSceneName);
        // 继续：读档恢复会话（失败则留在主菜单，按钮已按有无存档置灰）
        if (RunSession.EnsureInstance().LoadFromSave())
        {
            SoulMenuShowcase.ExitShowcase(); // 同上：进入对局场景前销毁展示灵魂
            SceneManager.LoadScene(battleSceneName);
        }
    }

    public void OnSettings()
    {
        Debug.Log("MainMenu: OnSettings CALLED. settingsPanel null? " + (settingsPanel == null));
        if (settingsPanel != null)
        {
            HideActivePanel();
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
            HideActivePanel();
            subPanelOpened = true;
            confirmDialog.Show(TextCatalog.Get("ui.mainmenu.quit_title"), TextCatalog.Get("ui.mainmenu.quit_message"), OnQuitConfirmed, OnDialogCancel);
        }
        else
        {
            OnQuitConfirmed();
        }
    }

    private void OnDialogCancel()
    {
        // callback when user hits Cancel or ESC
        RestoreActivePanel();
    }

    void LateUpdate()
    {
        // 子面板全部关闭后恢复来源面板（主面板 或 Play Panel）
        if (subPanelOpened)
        {
            bool sVisible = settingsPanel != null && settingsPanel.IsVisible();
            bool cVisible = confirmDialog != null && confirmDialog.IsVisible();
            bool hVisible = hallOfFamePanel != null && hallOfFamePanel.IsVisible();
            bool aVisible = cardArchivePanel != null && cardArchivePanel.IsVisible();
            if (!sVisible && !cVisible && !hVisible && !aVisible)
            {
                RestoreActivePanel();
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
