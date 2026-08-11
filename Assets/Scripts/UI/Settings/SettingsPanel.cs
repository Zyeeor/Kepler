using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 共用设置弹窗面板。
/// 音效音量 + 音乐音量两个滑块，弹窗覆盖模式。
/// 可挂载到主菜单、暂停菜单、结束界面的 Canvas 下复用。
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [Header("Panel")]
    public GameObject settingsPanel;

    [Header("Sliders")]
    public Slider sfxSlider;
    public Slider musicSlider;

    [Header("Labels")]
    public TMP_Text sfxLabel;
    public TMP_Text musicLabel;

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

        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(OnSFXChanged);

        if (musicSlider != null)
            musicSlider.onValueChanged.AddListener(OnMusicChanged);

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    /// <summary>
    /// 打开设置面板，从 AudioSettingsManager 加载当前值。
    /// </summary>
    public void Show()
    {
        if (settingsPanel == null)
            return;

        float sfxVol = AudioSettingsManager.Instance != null
            ? AudioSettingsManager.Instance.GetSFXVolume()
            : 0.8f;

        float musicVol = AudioSettingsManager.Instance != null
            ? AudioSettingsManager.Instance.GetMusicVolume()
            : 0.8f;

        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(sfxVol);

        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(musicVol);

        UpdateLabels(sfxVol, musicVol);

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

    private void OnSFXChanged(float value)
    {
        if (AudioSettingsManager.Instance != null)
            AudioSettingsManager.Instance.SetSFXVolume(value);
        UpdateSFXLabel(value);
    }

    private void OnMusicChanged(float value)
    {
        if (AudioSettingsManager.Instance != null)
            AudioSettingsManager.Instance.SetMusicVolume(value);
        UpdateMusicLabel(value);
    }

    private void UpdateLabels(float sfx, float music)
    {
        UpdateSFXLabel(sfx);
        UpdateMusicLabel(music);
    }

    private void UpdateSFXLabel(float value)
    {
        if (sfxLabel != null)
            sfxLabel.text = "SFX: " + Mathf.RoundToInt(value * 100) + "%";
    }

    private void UpdateMusicLabel(float value)
    {
        if (musicLabel != null)
            musicLabel.text = "Music: " + Mathf.RoundToInt(value * 100) + "%";
    }
}
