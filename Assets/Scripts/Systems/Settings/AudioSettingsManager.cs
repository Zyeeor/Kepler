using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 全局音频设置管理器。
/// 跨场景持久化，通过 AudioMixer 控制音效和音乐音量。
/// </summary>
public class AudioSettingsManager : MonoBehaviour
{
    public static AudioSettingsManager Instance { get; private set; }

    [Header("Audio Mixer")]
    [Tooltip("项目中的 AudioMixer 资源引用")]
    public AudioMixer audioMixer;

    [Header("Exposed Parameters")]
    [Tooltip("AudioMixer 中暴露的音效音量参数名")]
    public string sfxParameter = "SFXVolume";
    [Tooltip("AudioMixer 中暴露的音乐音量参数名")]
    public string musicParameter = "MusicVolume";

    private const string SFX_KEY = "Audio_SFX";
    private const string MUSIC_KEY = "Audio_Music";
    private const float DEFAULT_VOLUME = 0.8f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        LoadAndApply();
    }

    /// <summary>
    /// 设置音效音量 (0.0 ~ 1.0)
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SFX_KEY, volume);
        PlayerPrefs.Save();
        ApplyVolume(sfxParameter, volume);
    }

    /// <summary>
    /// 设置音乐音量 (0.0 ~ 1.0)
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(MUSIC_KEY, volume);
        PlayerPrefs.Save();
        ApplyVolume(musicParameter, volume);
    }

    /// <summary>
    /// 获取当前音效音量 (0.0 ~ 1.0)
    /// </summary>
    public float GetSFXVolume()
    {
        return PlayerPrefs.GetFloat(SFX_KEY, DEFAULT_VOLUME);
    }

    /// <summary>
    /// 获取当前音乐音量 (0.0 ~ 1.0)
    /// </summary>
    public float GetMusicVolume()
    {
        return PlayerPrefs.GetFloat(MUSIC_KEY, DEFAULT_VOLUME);
    }

    /// <summary>
    /// 从 PlayerPrefs 加载并应用到 AudioMixer
    /// </summary>
    public void LoadAndApply()
    {
        ApplyVolume(sfxParameter, GetSFXVolume());
        ApplyVolume(musicParameter, GetMusicVolume());
    }

    private void ApplyVolume(string parameter, float linearVolume)
    {
        if (audioMixer == null)
            return;

        float dB = LinearToDecibel(linearVolume);
        audioMixer.SetFloat(parameter, dB);
    }

    /// <summary>
    /// 线性值 (0~1) 转 dB (-80~0)
    /// </summary>
    private float LinearToDecibel(float linear)
    {
        if (linear <= 0.001f)
            return -80f;
        return Mathf.Log10(linear) * 20f;
    }
}
