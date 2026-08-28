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
    private bool subPanelOpened = false;
    private Button bossModeButton;
    private bool modeSelectionVisible = true;

    void Start()
    {
        // 主界面灵魂展示：对局带回的灵魂已在（DDOL），否则创建原生展示灵魂——
        // 保证"对局结束前后都在"，游戏启动一开始主界面就有主角。
        SoulMenuShowcase.SpawnNativeShowcase(soulShowcasePrefab, soulSpawnPosition);

        // Initialize sub-panels (they may start inactive, so their own Start won't run)
        if (settingsPanel != null) settingsPanel.Init();
        if (confirmDialog != null) confirmDialog.Init();

        // 继续按钮：有存档才可点（每帧监听存档文件状态）
        if (continueGameButton != null)
        {
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
            quitGameButton.onClick.AddListener(OnQuitGame);

        // 按钮文案统一走 TextCatalog（场景 TMP 英文初值仅作兜底）
        if (startGameButtonText != null) startGameButtonText.text = TextCatalog.Get("ui.mainmenu.start");
        if (continueGameButton != null)
        {
            var t = continueGameButton.GetComponentInChildren<TMPro.TMP_Text>();
            if (t != null) t.text = TextCatalog.Get("ui.mainmenu.continue");
        }
        if (settingsButtonText != null) settingsButtonText.text = TextCatalog.Get("ui.mainmenu.settings");
        if (quitGameButtonText != null) quitGameButtonText.text = TextCatalog.Get("ui.mainmenu.quit");

        EnsureBossModeButton();
        ShowModeSelection();
        EnsureHallOfFameEntry();
        EnsureCardArchiveEntry();

        ShowCursor();
    }

    /// <summary>
    /// 荣誉殿堂入口：克隆设置按钮生成（与主菜单美术按钮同风格、零场景编辑），
    /// 面板本体为纯代码 UI（HallOfFamePanel.EnsureInstance 自建 Overlay Canvas）。
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
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        subPanelOpened = true;
        hallOfFamePanel.Show();
    }

    /// <summary>
    /// 卡牌图鉴入口：
    /// 1) 面板组件挂载到主菜单 Canvas 下（宿主铺满 Canvas），策划可在 Inspector 直接调参数；
    /// 2) 入口按钮优先使用场景中已摆放的 CardArchiveButton（策划自制、可自由调样式/位置），
    ///    找不到时才克隆设置按钮兜底（零场景编辑也能用）。
    /// </summary>
    void EnsureCardArchiveEntry()
    {
        EnsureCardArchivePanel();
        if (cardArchiveButton != null) return;

        // 1) 优先：场景中已摆放好的按钮（含未激活的，便于策划先摆好再启用）
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
        if (settingsButton == null) return;

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

    /// <summary>
    /// 把图鉴组件挂到主菜单 Canvas 下：已挂载则复用，否则创建宿主（铺满 Canvas）并 AddComponent。
    /// 宿主挂在 Canvas 下而非 DDOL 游离对象，便于策划在编辑器里选中调参。
    /// </summary>
    void EnsureCardArchivePanel()
    {
        if (cardArchivePanel != null) return;

        // 优先复用场景中/主菜单下已有的组件实例
        cardArchivePanel = GetComponentInChildren<CardArchivePanel>(true);
        if (cardArchivePanel == null)
            cardArchivePanel = FindObjectOfType<CardArchivePanel>();

        if (cardArchivePanel != null) return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogWarning("[MainMenu] 未找到 Canvas，无法挂载卡牌图鉴。"); return; }

        var host = new GameObject(nameof(CardArchivePanel), typeof(RectTransform));
        host.transform.SetParent(canvas.transform, false);
        var hostRT = host.GetComponent<RectTransform>();
        hostRT.anchorMin = Vector2.zero;
        hostRT.anchorMax = Vector2.one;
        hostRT.offsetMin = Vector2.zero;
        hostRT.offsetMax = Vector2.zero;

        cardArchivePanel = host.AddComponent<CardArchivePanel>();
        // 显式构建：AddComponent 后 Start 要等下一帧，这里立即建好，避免首次点击时还没构建
        cardArchivePanel.Build();
    }

    public void OnCardArchive()
    {
        EnsureCardArchivePanel();   // 缺失时按需补挂（如场景切换后宿主被销毁）
        if (cardArchivePanel == null) return;
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        subPanelOpened = true;
        cardArchivePanel.onClose = OnCardArchiveClosed;
        cardArchivePanel.Show();
    }

    void OnCardArchiveClosed()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        subPanelOpened = false;
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
            else if (cardArchivePanel != null && cardArchivePanel.IsVisible())
            {
                cardArchivePanel.Hide();
            }
            else if (!modeSelectionVisible && !subPanelOpened)
            {
                ShowModeSelection();
            }
        }
    }

    /// <summary>
    /// 首屏只显示模式选择：复用现有开始按钮作为普通模式，并运行时复制一个 Boss 模式按钮，
    /// 避免修改现有 MainMenu 场景布局与美术资源。
    /// </summary>
    void EnsureBossModeButton()
    {
        if (bossModeButton != null || startGameButton == null) return;

        Transform parent = startGameButton.transform.parent;
        if (parent == null) return;
        GameObject clone = Instantiate(startGameButton.gameObject, parent);
        clone.name = "BossModeButton";
        clone.SetActive(true);

        RectTransform cloneRect = clone.GetComponent<RectTransform>();
        RectTransform continueRect = continueGameButton != null
            ? continueGameButton.GetComponent<RectTransform>() : null;
        if (cloneRect != null && continueRect != null)
            cloneRect.anchoredPosition = continueRect.anchoredPosition;
        else if (cloneRect != null)
            cloneRect.anchoredPosition += Vector2.down * 100f;

        bossModeButton = clone.GetComponent<Button>();
        if (bossModeButton == null) return;
        // 克隆按钮只保留场景持久化外观，不复用开始/继续的运行时监听。
        bossModeButton.onClick.RemoveAllListeners();
        bossModeButton.onClick.AddListener(OnStartBossGame);
        SetButtonLabel(bossModeButton, "Boss模式");
        if (FontRegistry.Instance != null)
            FontRegistry.Instance.ApplyToTree(clone.transform);
    }

    /// <summary>显示首屏的普通模式 / Boss 模式选择。</summary>
    void ShowModeSelection()
    {
        modeSelectionVisible = true;
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(true);
            startGameButton.onClick.RemoveListener(OnStartGame);
            startGameButton.onClick.RemoveListener(OnSelectNormalMode);
            startGameButton.onClick.AddListener(OnSelectNormalMode);
            SetButtonLabel(startGameButton, "普通模式");
        }
        if (continueGameButton != null)
            continueGameButton.gameObject.SetActive(false);
        if (bossModeButton != null)
            bossModeButton.gameObject.SetActive(true);
    }

    /// <summary>进入普通模式菜单，恢复原有开始 / 继续入口。</summary>
    public void OnSelectNormalMode()
    {
        modeSelectionVisible = false;
        if (bossModeButton != null)
            bossModeButton.gameObject.SetActive(false);
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(true);
            startGameButton.onClick.RemoveListener(OnSelectNormalMode);
            startGameButton.onClick.RemoveListener(OnStartGame);
            startGameButton.onClick.AddListener(OnStartGame);
            SetButtonLabel(startGameButton, TextCatalog.Get("ui.mainmenu.start"));
        }
        if (continueGameButton != null)
        {
            continueGameButton.gameObject.SetActive(true);
            continueGameButton.interactable = SaveCoordinator.HasSaveFile;
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
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
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
            bool hVisible = hallOfFamePanel != null && hallOfFamePanel.IsVisible();
            bool aVisible = cardArchivePanel != null && cardArchivePanel.IsVisible();
            if (!sVisible && !cVisible && !hVisible && !aVisible)
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
