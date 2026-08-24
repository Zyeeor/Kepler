using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// First Clear 八步序列控制器（Scheduler 自举创建，DDOL，用完隐藏）。
/// 首通（Result + FirstClearCompleted==false）时呈现固定八步结构；次通走普通 Result + Recognition Cue。
/// 最低表现：全屏黑底 + 中央 TMP + 按钮组（无新场景/立绘/CG）。
/// </summary>
public class FirstClearController : MonoBehaviour
{
    public FirstClearConfig config;

    public static FirstClearController Instance { get; private set; }

    GameObject _panel;
    TextMeshProUGUI _text;
    GameObject _buttonGroup;
    bool _busy;

    public static FirstClearController EnsureInstance(FirstClearConfig cfg)
    {
        if (Instance != null) { if (cfg != null && Instance.config == null) Instance.config = cfg; return Instance; }
        var go = new GameObject("FirstClearController");
        DontDestroyOnLoad(go);
        var c = go.AddComponent<FirstClearController>();
        c.config = cfg;
        return c;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>启动八步序列。onComplete 回调后落普通 Result。</summary>
    public void Begin(RunTendencyResult tendency, Action onComplete)
    {
        if (_busy) return;
        _busy = true;
        StartCoroutine(RunSequence(tendency, onComplete));
    }

    IEnumerator RunSequence(RunTendencyResult tendency, Action onComplete)
    {
        BuildPanel();

        // S1 Mythic closure
        yield return ShowText(TextCatalog.Get(config.step1MythicClosureKey), false);
        // S2 我是谁？
        yield return ShowText(TextCatalog.Get(config.step2WhoAmIKey), false);
        // S3 Self-Declaration 三按钮
        yield return ShowDeclaration();
        // S4 Functional Summary
        yield return ShowText(BuildSummary(tendency), false);
        // S5 Model / Version / Instance
        string model = tendency != null && !string.IsNullOrEmpty(tendency.modelIdText)
            ? tendency.modelIdText : "CARRIER-?-V?-I?";
        yield return ShowText($"{TextCatalog.Get(config.step5ModelTitleKey)}\n{model}", false);
        // S6 System Confirmation
        yield return ShowText(TextCatalog.Get(config.step6ConfirmationKey), false);
        // S7 黑屏停顿
        yield return BlackHold();
        // S8 最终句
        yield return ShowText(TextCatalog.Get(config.step8DistillationKey), false, finalHold: true);

        NarrativeProfileStore.MarkFirstClearCompleted();
        HidePanel();
        _busy = false;
        onComplete?.Invoke();
    }

    string BuildSummary(RunTendencyResult tendency)
    {
        if (tendency == null || tendency.primary == SinType.None)
            return TextCatalog.Get("nar.firstclear.s4.fallback");
        string primary = TextCatalog.Get(config.summarySinTextKeyPrefix + RunStatsUtil.WireName(tendency.primary));
        string secondary = tendency.secondary != SinType.None
            ? TextCatalog.Get(config.summarySinTextKeyPrefix + RunStatsUtil.WireName(tendency.secondary)) : "";
        string style = TextCatalog.Get(tendency.behaviorTextKey);
        string template = TextCatalog.Get(config.summaryTemplateKey);
        return template.Replace("{PRIMARY}", primary).Replace("{SECONDARY}", secondary).Replace("{STYLE}", style);
    }

    // ── 步骤呈现（点击/按键推进）──

    IEnumerator ShowText(string text, bool showButtons, bool finalHold = false)
    {
        _text.text = text;
        _text.gameObject.SetActive(true);
        _buttonGroup.SetActive(showButtons);
        _panel.SetActive(true);

        float t = 0f;
        bool advanced = false;
        while (!advanced)
        {
            t += Time.unscaledDeltaTime;
            if (t >= config.stepMinReadSeconds
                && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
                advanced = true;
            if (Input.GetKeyDown(KeyCode.Escape)) advanced = true; // Esc 跳过
            yield return null;
        }

        if (finalHold) yield return new WaitForSecondsRealtime(config.finalHoldSeconds);
    }

    IEnumerator ShowDeclaration()
    {
        _text.text = ""; // 声明选项用按钮承载
        _text.gameObject.SetActive(false);
        _buttonGroup.SetActive(true);
        _panel.SetActive(true);

        string chosen = null;
        // 动态建按钮
        for (int i = 0; i < _buttonGroup.transform.childCount; i++)
            Destroy(_buttonGroup.transform.GetChild(i).gameObject);
        var btns = _buttonGroup.transform;
        var chosenId = new string[1];
        for (int i = 0; i < config.declarationKeys.Length; i++)
        {
            int idx = i;
            var bgo = new GameObject("Decl_" + idx, typeof(Image), typeof(Button));
            bgo.transform.SetParent(btns, false);
            var rect = (RectTransform)bgo.transform;
            rect.sizeDelta = new Vector2(500f, 70f);
            rect.anchoredPosition = new Vector2(0f, 60f - idx * 90f);
            var bbtn = bgo.GetComponent<Button>();
            bbtn.onClick.AddListener(() =>
            {
                chosenId[0] = config.declarationIds[idx];
                NarrativeProfileStore.SelectedDeclarationId = chosenId[0];
            });
            // 按钮文本
            var tgo = new GameObject("Text", typeof(TextMeshProUGUI));
            tgo.transform.SetParent(bgo.transform, false);
            var tr = (RectTransform)tgo.transform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            var tmp = tgo.GetComponent<TextMeshProUGUI>();
            tmp.text = TextCatalog.Get(config.declarationKeys[idx]);
            tmp.fontSize = 28f; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
        }

        // 等待选择（任意按钮点击后 chosenId 非空）
        while (chosenId[0] == null)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) chosenId[0] = config.declarationIds[0]; // Esc 默认第一项
            yield return null;
        }
        _buttonGroup.SetActive(false);
        yield return null;
    }

    IEnumerator BlackHold()
    {
        _text.gameObject.SetActive(false);
        _buttonGroup.SetActive(false);
        _panel.SetActive(true); // 黑底
        yield return new WaitForSecondsRealtime(config.blackHoldSeconds);
    }

    // ── UI 自举 ──

    void BuildPanel()
    {
        if (_panel != null) return;
        var canvasGo = new GameObject("FirstClearCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300; // 最高层，覆盖结算
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        _panel = new GameObject("Panel", typeof(Image));
        _panel.transform.SetParent(canvasGo.transform, false);
        var pr = (RectTransform)_panel.transform;
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        _panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.96f);

        var textGo = new GameObject("Text", typeof(TextMeshProUGUI));
        textGo.transform.SetParent(_panel.transform, false);
        var tr = (RectTransform)textGo.transform;
        tr.anchorMin = new Vector2(0.1f, 0.25f); tr.anchorMax = new Vector2(0.9f, 0.9f);
        tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        _text = textGo.GetComponent<TextMeshProUGUI>();
        _text.fontSize = 40f; _text.alignment = TextAlignmentOptions.Center; _text.color = Color.white;

        _buttonGroup = new GameObject("Buttons", typeof(RectTransform));
        _buttonGroup.transform.SetParent(_panel.transform, false);
        var br = (RectTransform)_buttonGroup.transform;
        br.anchorMin = new Vector2(0.5f, 0.3f); br.anchorMax = new Vector2(0.5f, 0.3f);
        br.anchoredPosition = Vector2.zero; br.sizeDelta = new Vector2(600f, 300f);

        _panel.SetActive(false);
    }

    void HidePanel()
    {
        if (_panel != null) _panel.SetActive(false);
    }
}
