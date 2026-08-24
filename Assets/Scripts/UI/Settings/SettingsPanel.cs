using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 共用设置弹窗面板。
/// 四个独立音量滑块：Voice（旁白）/ Music（BGM）/ SFX（战斗与世界）/ UI（菜单、Card 与提示），
/// 弹窗覆盖模式。可挂载到主菜单、暂停菜单、结束界面的 Canvas 下复用。
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [Header("Panel")]
    public GameObject settingsPanel;

    [Header("Sliders（四路独立）")]
    public Slider voiceSlider;
    public Slider musicSlider;
    public Slider sfxSlider;
    public Slider uiSlider;

    [Header("Labels")]
    public TMP_Text voiceLabel;
    public TMP_Text musicLabel;
    public TMP_Text sfxLabel;
    public TMP_Text uiLabel;

    [Header("Buttons")]
    public Button closeButton;
    public TMP_Text closeButtonText;

    private bool inited = false;

    /// <summary>
    /// Init from a controller that IS always active (Canvas root).
    /// Binds listeners even if this panel starts inactive.
    /// </summary>
    public void Init()
    {
        if (inited) return;
        inited = true;

        // 音频系统自举：设置面板可能在无 GameManager 的场景（如主菜单）被打开，
        // 确保音量调节链路（AudioSettingsManager + AudioManager）始终可用。
        AudioSettingsManager.EnsureInstance();
        AudioManager.EnsureInstance();

        // 场景 YAML 未升级四滑块时，运行时自动补齐（复制现有滑块/标签模板并重排布局）
        EnsureFourSliders();

        if (voiceSlider != null)
            voiceSlider.onValueChanged.AddListener(OnVoiceChanged);
        if (musicSlider != null)
            musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        if (uiSlider != null)
            uiSlider.onValueChanged.AddListener(OnUIChanged);

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    /// <summary>
    /// 四滑块 UI 运行时补齐（零场景 YAML 改动，多场景自动生效）：
    /// 场景里旧版只有 music/sfx 两个滑块，此处复制出 voice/ui 两组并把四组纵向排布
    /// （Voice→Music→SFX→UI），closeButton 同步下移避免重叠。
    /// </summary>
    void EnsureFourSliders()
    {
        if (voiceSlider != null && uiSlider != null) return; // 场景已升级四滑块

        // ── Voice 组：复制 Music 模板，保持原 Music 行位（label 100 / slider 60）──
        if (voiceSlider == null && musicSlider != null)
        {
            voiceSlider = CloneSlider(musicSlider, "VoiceSlider", new Vector2(0f, 60f));
            if (voiceLabel == null && musicLabel != null)
                voiceLabel = CloneLabel(musicLabel, "VoiceLabel", new Vector2(0f, 100f));
            // 原 Music 组下移为第二行
            musicSlider.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -20f);
            if (musicLabel != null)
                musicLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 20f);
        }

        // ── UI 组：复制 SFX 模板，放到最底行；原 SFX 组下移为第三行 ──
        if (uiSlider == null && sfxSlider != null)
        {
            uiSlider = CloneSlider(sfxSlider, "UISlider", new Vector2(0f, -180f));
            if (uiLabel == null && sfxLabel != null)
                uiLabel = CloneLabel(sfxLabel, "UILabel", new Vector2(0f, -140f));
            sfxSlider.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -100f);
            if (sfxLabel != null)
                sfxLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -60f);
        }

        // ── closeButton 下移，避开最底 UI 行 ──
        if (closeButton != null)
            closeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -230f);
    }

    /// <summary>复制滑块模板（同父、清空复制来的持久化监听、定位）。</summary>
    static Slider CloneSlider(Slider template, string goName, Vector2 anchoredPos)
    {
        var go = Instantiate(template.gameObject, template.transform.parent);
        go.name = goName;
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        var slider = go.GetComponent<Slider>();
        slider.onValueChanged = new Slider.SliderEvent(); // 复制体不继承模板的持久化监听
        return slider;
    }

    /// <summary>复制标签模板（同父、定位）。</summary>
    static TMP_Text CloneLabel(TMP_Text template, string goName, Vector2 anchoredPos)
    {
        var go = Instantiate(template.gameObject, template.transform.parent);
        go.name = goName;
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        return go.GetComponent<TMP_Text>();
    }

    /// <summary>
    /// 打开设置面板，从 AudioSettingsManager 加载四路当前值。
    /// </summary>
    public void Show()
    {
        if (settingsPanel == null)
            return;

        var asm = AudioSettingsManager.Instance;

        float voiceVol = asm != null ? asm.GetVoiceVolume() : 0.8f;
        float musicVol = asm != null ? asm.GetMusicVolume() : 0.8f;
        float sfxVol = asm != null ? asm.GetSFXVolume() : 0.8f;
        float uiVol = asm != null ? asm.GetUIVolume() : 0.8f;

        if (voiceSlider != null)
            voiceSlider.SetValueWithoutNotify(voiceVol);
        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(musicVol);
        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(sfxVol);
        if (uiSlider != null)
            uiSlider.SetValueWithoutNotify(uiVol);

        UpdateLabels(voiceVol, musicVol, sfxVol, uiVol);

        settingsPanel.SetActive(true);
        settingsPanel.transform.SetAsLastSibling();
    }

    /// <summary>
    /// 关闭设置面板。
    /// </summary>
    public void Hide()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public bool IsVisible()
    {
        return settingsPanel != null && settingsPanel.activeSelf;
    }

    private void OnVoiceChanged(float value)
    {
        if (AudioSettingsManager.Instance != null)
            AudioSettingsManager.Instance.SetVoiceVolume(value);
        UpdateVoiceLabel(value);
    }

    private void OnMusicChanged(float value)
    {
        if (AudioSettingsManager.Instance != null)
            AudioSettingsManager.Instance.SetMusicVolume(value);
        UpdateMusicLabel(value);
    }

    private void OnSFXChanged(float value)
    {
        if (AudioSettingsManager.Instance != null)
            AudioSettingsManager.Instance.SetSFXVolume(value);
        UpdateSFXLabel(value);
    }

    private void OnUIChanged(float value)
    {
        if (AudioSettingsManager.Instance != null)
            AudioSettingsManager.Instance.SetUIVolume(value);
        UpdateUILabel(value);
    }

    private void UpdateLabels(float voice, float music, float sfx, float ui)
    {
        UpdateVoiceLabel(voice);
        UpdateMusicLabel(music);
        UpdateSFXLabel(sfx);
        UpdateUILabel(ui);
    }

    private void UpdateVoiceLabel(float value)
    {
        if (voiceLabel != null)
            voiceLabel.text = string.Format(TextCatalog.Get("ui.settings.voice"), Mathf.RoundToInt(value * 100));
    }

    private void UpdateMusicLabel(float value)
    {
        if (musicLabel != null)
            musicLabel.text = string.Format(TextCatalog.Get("ui.settings.music"), Mathf.RoundToInt(value * 100));
    }

    private void UpdateSFXLabel(float value)
    {
        if (sfxLabel != null)
            sfxLabel.text = string.Format(TextCatalog.Get("ui.settings.sfx"), Mathf.RoundToInt(value * 100));
    }

    private void UpdateUILabel(float value)
    {
        if (uiLabel != null)
            uiLabel.text = string.Format(TextCatalog.Get("ui.settings.ui"), Mathf.RoundToInt(value * 100));
    }
}
