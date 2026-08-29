using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public enum VictoryEpilogueStage
{
    Inactive,
    EnteringBlack,
    FirstReveal,
    WaitingForName,
    FirstFadeOut,
    SecondBlack,
    FinalReveal,
    FinalHold,
    ExitBlack,
    Leaving,
}

/// <summary>
/// Victory Epilogue 的统一表现控制器。
/// Formal Victory 与 F10/F11 Preview 共用同一套 UI、时序、输入和音频触发代码；
/// Preview 只跳过正式 Result 副作用，不会伪造 Boss 击败、Run Result 或存档。
/// </summary>
public sealed class VictoryEpilogueController : MonoBehaviour
{
    public static VictoryEpilogueController Instance { get; private set; }
    public static bool IsEnabled => UIManager.Instance != null && UIManager.Instance.useVictoryEpilogueForWin;
    public static bool IsPlaying => Instance != null && Instance._isPlaying;

    [SerializeField] VictoryEpilogueConfig config;

    VictoryEpilogueView _viewInstance;
    bool _usingManualPrefab;
    Canvas _canvas;
    CanvasGroup _rootGroup;
    Image _blackBackground;
    CanvasGroup _firstStageGroup;
    CanvasGroup _inputGroup;
    CanvasGroup _finalStageGroup;
    CanvasGroup _finalTitleGroup;
    CanvasGroup _finalNameGroup;
    CanvasGroup _finalCoronationGroup;
    TextMeshProUGUI _firstMessageText;
    TextMeshProUGUI _namePromptText;
    TextMeshProUGUI _finalTitleText;
    TextMeshProUGUI _finalNameText;
    TextMeshProUGUI _finalCoronationText;
    TMP_InputField _nameInput;
    Button _confirmButton;

    Coroutine _playRoutine;
    bool _isPlaying;
    bool _formalActive;
    bool _formalStartedForRun;
    bool _waitingForName;
    bool _timeLockHeld;
    string _playerName;
    VictoryEpilogueStage _stage = VictoryEpilogueStage.Inactive;

    public VictoryEpilogueStage Stage => _stage;
    public VictoryEpilogueConfig Config => config;
    public bool IsFormalActive => _formalActive;
    public bool UsingManualPrefab => _usingManualPrefab;

    public static VictoryEpilogueController EnsureInstance(VictoryEpilogueConfig requestedConfig = null)
    {
        if (Instance != null)
        {
            if (requestedConfig != null) Instance.config = requestedConfig;
            return Instance;
        }

        var go = new GameObject("VictoryEpilogueController");
        DontDestroyOnLoad(go);
        var controller = go.AddComponent<VictoryEpilogueController>();
        if (requestedConfig != null) controller.config = requestedConfig;
        return controller;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadConfigIfNeeded();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!_formalActive && !_waitingForName && config != null && config.enableVictoryEpilogueDebugPreview)
        {
            bool modifierOk = (!config.debugRequireControl || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                && (!config.debugRequireShift || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            if (modifierOk && Input.GetKeyDown(config.debugFullPreviewInput))
                PlayFullPreview();
            else if (modifierOk && Input.GetKeyDown(config.debugFinalPreviewInput))
                PlayFinalPreview();
        }
#endif

        if (_waitingForName && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            ConfirmName();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (_playRoutine != null) StopCoroutine(_playRoutine);
        ReleaseLocks();
        if (Instance == this) Instance = null;
    }

    void LoadConfigIfNeeded()
    {
        if (config != null) return;
        config = Resources.Load<VictoryEpilogueConfig>("Victory/VictoryEpilogueConfig");
        if (config == null) config = VictoryEpilogueConfig.CreateRuntimeDefaults();
        if (config.presentationPrefab == null)
            config.presentationPrefab = Resources.Load<VictoryEpilogueView>("Victory/VictoryEpilogueView");
    }

    public void PlayFormalVictory()
    {
        PlayFormalVictory(null);
    }

    public void PlayFormalVictory(VictoryEpilogueConfig requestedConfig)
    {
        if (_formalStartedForRun || _isPlaying) return;
        if (requestedConfig != null) config = requestedConfig;
        LoadConfigIfNeeded();
        _formalStartedForRun = true;
        _formalActive = true;
        StartPlayback(false);
    }

    public void PlayFullPreview()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_formalActive) return;
        LoadConfigIfNeeded();
        ResetPreviewState();
        _formalActive = false;
        StartPlayback(false);
#endif
    }

    public void PlayFinalPreview()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_formalActive) return;
        LoadConfigIfNeeded();
        ResetPreviewState();
        _formalActive = false;
        StartPlayback(true);
#endif
    }

    public void StopResetPreview()
    {
        if (_formalActive) return;
        ResetPreviewState();
    }

    void StartPlayback(bool finalOnly)
    {
        BuildLayout();
        StopPlaybackRoutineOnly();
        _isPlaying = true;
        _waitingForName = false;
        _playerName = finalOnly ? SanitizeName(config.debugEpiloguePlayerName) : string.Empty;
        if (string.IsNullOrEmpty(_playerName)) _playerName = "SONG";
        _stage = finalOnly ? VictoryEpilogueStage.FinalReveal : VictoryEpilogueStage.EnteringBlack;

        _canvas.gameObject.SetActive(true);
        _rootGroup.alpha = finalOnly ? 1f : 0f;
        _rootGroup.blocksRaycasts = true;
        _rootGroup.interactable = true;
        SetAllPresentationGroups(false);
        AcquireLocks();
        _playRoutine = StartCoroutine(finalOnly ? RunFinalPreview() : RunFullPlayback());
    }

    IEnumerator RunFullPlayback()
    {
        PlayCue(config.enterBlackAudio);
        AudioManager.Instance?.BeginVictoryEpilogue();
        yield return Fade(_rootGroup, 0f, 1f, config.fadeToBlackDuration);

        _stage = VictoryEpilogueStage.SecondBlack;
        yield return Hold(config.firstBlackHoldDuration);

        _stage = VictoryEpilogueStage.FirstReveal;
        _firstStageGroup.gameObject.SetActive(true);
        _firstMessageText.text = ResolveVictoryText(config.firstMessage, _firstMessageText);
        _firstMessageText.richText = false;
        ApplyTextFontForView(_firstMessageText);
        PlayCue(config.firstTextRevealAudio);
        yield return Fade(_firstStageGroup, 0f, 1f, config.firstTextFadeInDuration);
        yield return Hold(config.firstTextHoldBeforeInputDuration);

        _stage = VictoryEpilogueStage.WaitingForName;
        _firstStageGroup.interactable = true;
        _firstStageGroup.blocksRaycasts = true;
        _inputGroup.gameObject.SetActive(true);
        _inputGroup.interactable = true;
        _inputGroup.blocksRaycasts = true;
        PlayCue(config.nameInputRevealAudio);
        yield return Fade(_inputGroup, 0f, 1f, config.inputFieldFadeInDuration);
        _waitingForName = true;
        FocusNameInput();

        while (_waitingForName && _isPlaying)
            yield return null;

        if (!_isPlaying) yield break;
        _stage = VictoryEpilogueStage.FirstFadeOut;
        yield return Fade(_firstStageGroup, 1f, 0f, config.firstStageFadeOutDuration);
        _firstStageGroup.interactable = false;
        _firstStageGroup.blocksRaycasts = false;
        _firstStageGroup.gameObject.SetActive(false);
        _inputGroup.interactable = false;
        _inputGroup.blocksRaycasts = false;
        _inputGroup.gameObject.SetActive(false);

        _stage = VictoryEpilogueStage.SecondBlack;
        yield return Hold(config.secondBlackHoldDuration);
        yield return RevealFinalStage();
        yield return FinishPlayback();
    }

    IEnumerator RunFinalPreview()
    {
        AudioManager.Instance?.BeginVictoryEpilogue();
        yield return RevealFinalStage();
        yield return FinishPlayback();
    }

    IEnumerator RevealFinalStage()
    {
        _stage = VictoryEpilogueStage.FinalReveal;
        _finalStageGroup.gameObject.SetActive(true);
        _finalTitleText.text = ResolveVictoryText(config.finalTitle, _finalTitleText);
        _finalNameText.text = _playerName;
        _finalCoronationText.text = ResolveVictoryText(config.finalCoronationLine, _finalCoronationText);
        _finalTitleText.richText = false;
        _finalNameText.richText = false;
        _finalCoronationText.richText = false;
        ApplyTextFontForView(_finalTitleText);
        ApplyTextFontForView(_finalNameText);
        ApplyTextFontForView(_finalCoronationText);
        PlayCue(config.finalRevealAudio);

        _finalTitleGroup.gameObject.SetActive(true);
        _finalNameGroup.gameObject.SetActive(true);
        _finalCoronationGroup.gameObject.SetActive(true);
        _finalTitleGroup.interactable = true;
        _finalNameGroup.interactable = true;
        _finalCoronationGroup.interactable = true;
        _finalStageGroup.alpha = 0f;
        yield return Fade(_finalStageGroup, 0f, 1f, config.finalStageFadeInDuration);
        yield return RevealFinalLines();

        _stage = VictoryEpilogueStage.FinalHold;
        yield return Hold(config.finalStageHoldDuration);
    }

    IEnumerator RevealFinalLines()
    {
        _finalTitleGroup.alpha = 0f;
        _finalNameGroup.alpha = 0f;
        _finalCoronationGroup.alpha = 0f;
        _finalTitleGroup.blocksRaycasts = false;
        _finalNameGroup.blocksRaycasts = false;
        _finalCoronationGroup.blocksRaycasts = false;
        bool titleShown = false;
        bool nameShown = false;
        bool coronationShown = false;
        float elapsed = 0f;
        float lastDelay = Mathf.Max(config.finalTitleRevealDelay,
            Mathf.Max(config.finalNameRevealDelay, config.finalCoronationRevealDelay));

        while (_isPlaying && elapsed < lastDelay)
        {
            if (!titleShown && elapsed >= config.finalTitleRevealDelay)
            {
                titleShown = true;
                _finalTitleGroup.alpha = 1f;
                PlayCue(config.finalTitleRevealAudio, SfxId.VictoryEpilogueFinalTitleReveal);
            }
            if (!nameShown && elapsed >= config.finalNameRevealDelay)
            {
                nameShown = true;
                _finalNameGroup.alpha = 1f;
                PlayCue(config.finalNameRevealAudio, SfxId.VictoryEpilogueFinalNameReveal);
            }
            if (!coronationShown && elapsed >= config.finalCoronationRevealDelay)
            {
                coronationShown = true;
                _finalCoronationGroup.alpha = 1f;
                PlayCue(config.finalCoronationRevealAudio, SfxId.VictoryEpilogueFinalCoronationReveal);
            }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!titleShown) { _finalTitleGroup.alpha = 1f; PlayCue(config.finalTitleRevealAudio); }
        if (!nameShown) { _finalNameGroup.alpha = 1f; PlayCue(config.finalNameRevealAudio); }
        if (!coronationShown) { _finalCoronationGroup.alpha = 1f; PlayCue(config.finalCoronationRevealAudio); }
    }

    IEnumerator FinishPlayback()
    {
        _stage = VictoryEpilogueStage.ExitBlack;
        yield return Fade(_finalStageGroup, 1f, 0f, config.finalStageFadeOutDuration);
        _finalStageGroup.gameObject.SetActive(false);
        PlayCue(config.exitBlackAudio);
        AudioManager.Instance?.SetVictoryEpilogueExitBgm();
        yield return Hold(config.finalBlackHoldDuration);

        _stage = VictoryEpilogueStage.Leaving;
        ReturnToMainMenu();
    }

    public void ConfirmName()
    {
        if (!_waitingForName || !_isPlaying || _nameInput == null) return;
        string value = SanitizeName(_nameInput.text);
        if (string.IsNullOrEmpty(value))
        {
            _namePromptText.text = config.namePrompt + "\n\n请输入名字";
            FocusNameInput();
            return;
        }

        _playerName = value;
        _nameInput.text = value;
        _nameInput.interactable = false;
        if (_confirmButton != null) _confirmButton.interactable = false;
        _waitingForName = false;
        PlayCue(config.nameConfirmAudio);
    }

    string SanitizeName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        string value = raw.Trim();
        if (value.Length > config.maxNameLength)
            value = value.Substring(0, config.maxNameLength);
        return value;
    }

    void FocusNameInput()
    {
        if (_nameInput == null) return;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_nameInput.gameObject);
        _nameInput.interactable = true;
        _nameInput.Select();
        _nameInput.ActivateInputField();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        StartCoroutine(RefocusNameInputNextFrame());
    }

    IEnumerator RefocusNameInputNextFrame()
    {
        yield return null;
        if (!_waitingForName || _nameInput == null) yield break;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_nameInput.gameObject);
        _nameInput.Select();
        _nameInput.ActivateInputField();
    }

    void PlayCue(SfxId id)
    {
        if (id == SfxId.None) return;
        AudioManager.Instance?.Play(id);
    }

    void PlayCue(SfxId configuredId, SfxId fallbackId)
    {
        PlayCue(configuredId != SfxId.None ? configuredId : fallbackId);
    }

    void ReturnToMainMenu()
    {
        if (!_isPlaying) return;
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OnHomeClicked();
            return;
        }

        RunSession.Instance?.EndRun();
        if (GameManager.Instance != null) GameManager.Instance.ResetGame();
        TimeScaleManager.ResetAll();
        SceneManager.LoadScene("MainMenu");
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "MainMenu") return;
        StartCoroutine(HideAfterMainMenuAppears());
    }

    IEnumerator HideAfterMainMenuAppears()
    {
        yield return null;
        StopPlaybackRoutineOnly();
        _isPlaying = false;
        _formalActive = false;
        _formalStartedForRun = false;
        _stage = VictoryEpilogueStage.Inactive;
        AudioManager.Instance?.ClearVictoryEpilogue();
        ReleaseLocks();
        if (_canvas != null) _canvas.gameObject.SetActive(false);
    }

    bool TryBuildLayoutFromPrefab()
    {
        if (config == null || config.presentationPrefab == null) return false;
        EnsureEventSystem();
        _viewInstance = Instantiate(config.presentationPrefab, transform);
        _viewInstance.name = "VictoryEpilogueView_Runtime";
        if (!_viewInstance.HasRequiredReferences)
        {
            Debug.LogError("[VictoryEpilogue] Presentation Prefab 缺少必要引用，回退到代码生成布局。", _viewInstance);
            Destroy(_viewInstance.gameObject);
            _viewInstance = null;
            return false;
        }

        _usingManualPrefab = true;
        _canvas = _viewInstance.canvas;
        _rootGroup = _viewInstance.rootGroup;
        _blackBackground = _viewInstance.blackBackground;
        _firstStageGroup = _viewInstance.firstStageGroup;
        _firstMessageText = _viewInstance.firstMessageText;
        _namePromptText = _viewInstance.namePromptText;
        _inputGroup = _viewInstance.inputGroup;
        _nameInput = _viewInstance.nameInput;
        _confirmButton = _viewInstance.confirmButton;
        _finalStageGroup = _viewInstance.finalStageGroup;
        _finalTitleGroup = _viewInstance.finalTitleGroup;
        _finalTitleText = _viewInstance.finalTitleText;
        _finalNameGroup = _viewInstance.finalNameGroup;
        _finalNameText = _viewInstance.finalNameText;
        _finalCoronationGroup = _viewInstance.finalCoronationGroup;
        _finalCoronationText = _viewInstance.finalCoronationText;
        _viewInstance.ApplyDefaultRuntimeFlags();
        _nameInput.onSubmit.RemoveListener(OnNameInputSubmit);
        _nameInput.onSubmit.AddListener(OnNameInputSubmit);
        _confirmButton.onClick.RemoveListener(ConfirmName);
        _confirmButton.onClick.AddListener(ConfirmName);
        ApplyConfigToLayout();
        _canvas.gameObject.SetActive(false);
        return true;
    }

    void OnNameInputSubmit(string ignored)
    {
        ConfirmName();
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            if (EventSystem.current.GetComponent<BaseInputModule>() == null)
                EventSystem.current.gameObject.AddComponent<StandaloneInputModule>();
            return;
        }
        var eventSystemGo = new GameObject("VictoryEpilogueEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystemGo);
    }

    void BuildLayout()
    {
        if (_canvas != null)
        {
            ApplyConfigToLayout();
            return;
        }

        if (TryBuildLayoutFromPrefab())
            return;

        EnsureEventSystem();
        var canvasGo = new GameObject("VictoryEpilogueCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 1000;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        _rootGroup = canvasGo.GetComponent<CanvasGroup>();
        _rootGroup.alpha = 0f;
        _rootGroup.blocksRaycasts = false;
        _rootGroup.interactable = false;

        var background = new GameObject("BlackBackground", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvasGo.transform, false);
        Stretch(background.GetComponent<RectTransform>());
        _blackBackground = background.GetComponent<Image>();
        _blackBackground.color = Color.black;
        _blackBackground.raycastTarget = true;

        _firstStageGroup = CreateFullScreenGroup("FirstStageGroup", canvasGo.transform);
        _inputGroup = CreateFullScreenGroup("NameInputGroup", _firstStageGroup.transform);
        _finalStageGroup = CreateFullScreenGroup("FinalStageGroup", canvasGo.transform);
        _finalTitleGroup = CreateGroup("FinalTitle", _finalStageGroup.transform);
        _finalNameGroup = CreateGroup("FinalName", _finalStageGroup.transform);
        _finalCoronationGroup = CreateGroup("FinalCoronation", _finalStageGroup.transform);

        _firstMessageText = CreateText("FirstMessageText", _firstStageGroup.transform, TextAlignmentOptions.Center);
        _namePromptText = CreateText("NamePromptText", _inputGroup.transform, TextAlignmentOptions.Center);
        _finalTitleText = CreateText("TitleText", _finalTitleGroup.transform, TextAlignmentOptions.Center);
        _finalNameText = CreateText("PlayerNameText", _finalNameGroup.transform, TextAlignmentOptions.Center);
        _finalCoronationText = CreateText("CoronationText", _finalCoronationGroup.transform, TextAlignmentOptions.Center);

        BuildNameInput(_inputGroup.transform);
        ApplyConfigToLayout();
        SetAllPresentationGroups(false);
        _canvas.gameObject.SetActive(false);
    }

    CanvasGroup CreateFullScreenGroup(string name, Transform parent)
    {
        var group = CreateGroup(name, parent);
        Stretch(group.GetComponent<RectTransform>());
        return group;
    }

    CanvasGroup CreateGroup(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        return group;
    }

    TextMeshProUGUI CreateText(string name, Transform parent, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.richText = false;
        ApplyVictoryTextFont(text);
        return text;
    }

    void BuildNameInput(Transform parent)
    {
        var fieldGo = new GameObject("TMP_InputField", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        fieldGo.transform.SetParent(parent, false);
        _nameInput = fieldGo.GetComponent<TMP_InputField>();
        var fieldImage = fieldGo.GetComponent<Image>();
        fieldImage.color = new Color(1f, 1f, 1f, 0.12f);
        fieldImage.raycastTarget = true;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(fieldGo.transform, false);
        Stretch(textGo.GetComponent<RectTransform>(), new Vector2(24f, 8f), new Vector2(-24f, -8f));
        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.richText = false;
        text.enableWordWrapping = false;
        ApplyVictoryTextFont(text);
        _nameInput.textComponent = text;

        var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        placeholderGo.transform.SetParent(fieldGo.transform, false);
        Stretch(placeholderGo.GetComponent<RectTransform>(), new Vector2(24f, 8f), new Vector2(-24f, -8f));
        var placeholder = placeholderGo.GetComponent<TextMeshProUGUI>();
        placeholder.alignment = TextAlignmentOptions.Center;
        placeholder.color = new Color(1f, 1f, 1f, 0.45f);
        placeholder.richText = false;
        ApplyVictoryTextFont(placeholder);
        _nameInput.placeholder = placeholder;

        _nameInput.lineType = TMP_InputField.LineType.SingleLine;
        _nameInput.richText = false;
        _nameInput.characterLimit = config.maxNameLength;
        _nameInput.onSubmit.AddListener(_ => ConfirmName());

        var buttonGo = new GameObject("ConfirmButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(parent, false);
        _confirmButton = buttonGo.GetComponent<Button>();
        buttonGo.GetComponent<Image>().color = new Color(0.35f, 0.25f, 0.08f, 0.9f);
        var buttonText = CreateText("Text", buttonGo.transform, TextAlignmentOptions.Center);
        buttonText.text = "确认";
        buttonText.fontSize = 24f;
        ApplyVictoryTextFont(buttonText);
        Stretch(buttonText.rectTransform);
        _confirmButton.onClick.AddListener(ConfirmName);
    }

    void ApplyConfigToLayout()
    {
        if (_firstMessageText == null || config == null) return;
        if (_usingManualPrefab)
        {
            _nameInput.characterLimit = Mathf.Max(1, config.maxNameLength);
            _namePromptText.text = ResolveVictoryText(config.namePrompt, _namePromptText);
            ApplyTextFontForView(_firstMessageText);
            ApplyTextFontForView(_namePromptText);
            ApplyTextFontForView(_nameInput.textComponent);
            TMP_Text manualPlaceholder = _nameInput.placeholder as TMP_Text;
            ApplyTextFontForView(manualPlaceholder);
            ApplyTextFontForView(_finalTitleText);
            ApplyTextFontForView(_finalNameText);
            ApplyTextFontForView(_finalCoronationText);
            return;
        }
        _firstMessageText.fontSize = config.firstMessageFontSize;
        _firstMessageText.lineSpacing = config.firstMessageLineSpacing;
        SetCenteredRect(_firstMessageText.rectTransform, config.firstMessagePosition, new Vector2(1400f, 220f));

        _namePromptText.fontSize = config.namePromptFontSize;
        SetCenteredRect(_namePromptText.rectTransform, config.namePromptPosition, new Vector2(1200f, 100f));
        SetCenteredRect(_nameInput.GetComponent<RectTransform>(), config.inputFieldPosition, config.inputFieldSize);
        var inputText = _nameInput.textComponent;
        if (inputText != null) inputText.fontSize = config.inputTextFontSize;
        var placeholder = _nameInput.placeholder as TMP_Text;
        if (placeholder != null) placeholder.fontSize = config.inputTextFontSize;
        SetCenteredRect(_confirmButton.GetComponent<RectTransform>(), config.inputFieldPosition + new Vector2(0f, -70f), new Vector2(220f, 58f));

        _finalTitleText.fontSize = config.finalTitleFontSize;
        _finalNameText.fontSize = config.playerNameFontSize;
        _finalCoronationText.fontSize = config.coronationLineFontSize;
        SetCenteredRect(_finalTitleText.rectTransform, config.finalTitlePosition, new Vector2(1400f, 80f));
        SetCenteredRect(_finalNameText.rectTransform, config.playerNamePosition, new Vector2(1500f, 150f));
        SetCenteredRect(_finalCoronationText.rectTransform, config.coronationLinePosition, new Vector2(1400f, 100f));
        _nameInput.characterLimit = Mathf.Max(1, config.maxNameLength);
        _namePromptText.text = config.namePrompt;
        ApplyVictoryTextFont(_namePromptText);
        if (_nameInput.textComponent != null) ApplyVictoryTextFont(_nameInput.textComponent);
        TMP_Text placeholderText = _nameInput.placeholder as TMP_Text;
        if (placeholderText != null) ApplyVictoryTextFont(placeholderText);
    }

    static void ApplyVictoryTextFont(TMP_Text text)
    {
        if (text == null) return;
        FontRegistry registry = FontRegistry.Instance;
        if (registry != null && registry.DefaultFont != null)
        {
            text.font = registry.DefaultFont;
            if (registry.DefaultFont.material != null)
                text.fontSharedMaterial = registry.DefaultFont.material;
        }
        else
        {
            UiFontAssets.ApplyTo(text);
        }
        text.richText = false;
    }

    static void ApplyTextFontForView(TMP_Text text)
    {
        if (text == null) return;
        if (text.font == null)
            ApplyVictoryTextFont(text);
        else if (text.font.material != null && text.fontSharedMaterial == null)
            text.fontSharedMaterial = text.font.material;
        text.richText = false;
    }

    static string ResolveVictoryText(string value, TMP_Text target)
    {
        if (string.IsNullOrEmpty(value)) return value;
        TMP_FontAsset font = target != null && target.font != null
            ? target.font
            : (FontRegistry.Instance != null ? FontRegistry.Instance.DefaultFont : TMP_Settings.defaultFontAsset);
        if (font == null) return value;

        // Avoid a TMP missing-glyph box only when the manually selected view font lacks the character.
        if (value.Contains("弑") && !font.HasCharacter('弑'))
            return value.Replace("弑", "");
        return value;
    }

    void SetAllPresentationGroups(bool visible)
    {
        SetGroup(_firstStageGroup, visible);
        SetGroup(_inputGroup, false);
        SetGroup(_finalStageGroup, false);
        SetGroup(_finalTitleGroup, false);
        SetGroup(_finalNameGroup, false);
        SetGroup(_finalCoronationGroup, false);
        if (_nameInput != null)
        {
            _nameInput.text = string.Empty;
            _nameInput.interactable = true;
        }
        if (_confirmButton != null) _confirmButton.interactable = true;
    }

    static void SetGroup(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.gameObject.SetActive(visible);
    }

    void AcquireLocks()
    {
        if (!_timeLockHeld)
        {
            TimeScaleManager.Push(TimeDomain.GameOver, 0f);
            _timeLockHeld = true;
        }
        PlayerController.SetGameplayInputBlocked(true, "VictoryEpilogue");
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    void ReleaseLocks()
    {
        if (_timeLockHeld)
        {
            TimeScaleManager.Pop(TimeDomain.GameOver);
            _timeLockHeld = false;
        }
        PlayerController.SetGameplayInputBlocked(false, "VictoryEpilogue");
    }

    void StopPlaybackRoutineOnly()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }
        _waitingForName = false;
    }

    void ResetPreviewState()
    {
        if (_formalActive) return;
        StopPlaybackRoutineOnly();
        _isPlaying = false;
        _stage = VictoryEpilogueStage.Inactive;
        AudioManager.Instance?.ClearVictoryEpilogue();
        ReleaseLocks();
        if (_canvas != null)
        {
            SetAllPresentationGroups(false);
            _rootGroup.alpha = 0f;
            _rootGroup.blocksRaycasts = false;
            _rootGroup.interactable = false;
            _canvas.gameObject.SetActive(false);
        }
    }

    IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null) yield break;
        group.alpha = from;
        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }
        float elapsed = 0f;
        while (elapsed < duration && _isPlaying)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        group.alpha = to;
    }

    IEnumerator Hold(float duration)
    {
        if (duration <= 0f) yield break;
        float elapsed = 0f;
        while (elapsed < duration && _isPlaying)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    static void Stretch(RectTransform rect, Vector2? min = null, Vector2? max = null)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = min ?? Vector2.zero;
        rect.offsetMax = max ?? Vector2.zero;
    }

    static void SetCenteredRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
