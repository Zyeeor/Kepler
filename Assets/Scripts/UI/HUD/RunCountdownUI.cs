using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Run countdown shown in the lower-right corner of gameplay HUD.
/// It reads the effective combat clock owned by RunSpawnDirector so pauses and card-choice
/// screens do not consume time, while bullet time continues to count as intended.
/// </summary>
public sealed class RunCountdownUI : MonoBehaviour
{
    const float DefaultDurationSeconds = 420f;

    [Header("显示")]
    [Tooltip("倒计时总时长；默认 7 分钟。运行时优先读取 RunSpawnDirector.bossCombatTime。")]
    [Min(1f)] public float durationSeconds = DefaultDurationSeconds;
    [Tooltip("右下角安全边距（像素）。")]
    public Vector2 screenMargin = new Vector2(32f, 32f);
    [Tooltip("倒计时文字字号。")]
    [Min(1f)] public float fontSize = 36f;
    public Color textColor = Color.white;

    TextMeshProUGUI label;
    int lastShownSeconds = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InstallSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForGameplayScene();
    }

    static void EnsureForGameplayScene()
    {
        // MainMenu has a persistent RunSpawnDirector after a run; require a combat-scene
        // marker so the countdown is never created on the menu.
        if (FindObjectOfType<WaveManager>() == null && FindObjectOfType<ENGPOSS001SceneInstaller>() == null)
            return;

        Canvas canvas = FindGameplayCanvas();
        if (canvas != null && canvas.GetComponent<RunCountdownUI>() == null)
            canvas.gameObject.AddComponent<RunCountdownUI>();
    }

    static Canvas FindGameplayCanvas()
    {
        GameObject namedCanvas = GameObject.Find("UICanvas");
        if (namedCanvas != null)
        {
            Canvas canvas = namedCanvas.GetComponent<Canvas>();
            if (canvas != null) return canvas;
        }

        Canvas[] canvases = FindObjectsOfType<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
                return canvas;
        }

        return null;
    }

    void Awake()
    {
        EnsureLabel();
        RefreshLabel(force: true);
    }

    void EnsureLabel()
    {
        GameObject go = new GameObject("RunCountdownText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-screenMargin.x, screenMargin.y);
        rect.sizeDelta = new Vector2(220f, 64f);

        label = go.GetComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.BottomRight;
        label.fontStyle = FontStyles.Bold;
        label.color = textColor;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        UiFontAssets.ApplyTo(label);
        label.text = FormatSeconds(Mathf.CeilToInt(durationSeconds));
    }

    void Update()
    {
        RefreshLabel(force: false);
    }

    void RefreshLabel(bool force)
    {
        if (label == null) return;

        float total = durationSeconds;
        float elapsed = 0f;
        if (RunSpawnDirector.Instance != null)
        {
            total = Mathf.Max(1f, RunSpawnDirector.Instance.bossCombatTime);
            elapsed = RunSpawnDirector.Instance.ActiveCombatSeconds;
        }
        else if (RunSession.Instance != null)
        {
            elapsed = RunSession.Instance.ActiveCombatSeconds;
        }

        int remainingSeconds = Mathf.Clamp(Mathf.CeilToInt(total - elapsed), 0, Mathf.CeilToInt(total));
        if (!force && remainingSeconds == lastShownSeconds) return;

        lastShownSeconds = remainingSeconds;
        label.text = FormatSeconds(remainingSeconds);
    }

    static string FormatSeconds(int seconds)
    {
        return $"{seconds / 60:0}:{seconds % 60:00}";
    }
}
