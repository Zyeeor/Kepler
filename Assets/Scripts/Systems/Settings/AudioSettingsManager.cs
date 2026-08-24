using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 全局音频设置管理器（四路独立音量：Voice / BGM / SFX / UI，跨场景常驻）。
/// 由常驻 GameManager 统一创建（EnsureInstance），也可在场景内挂载（场景配置优先）。
/// 生效链路（双轨）：
///   - 若配置了 AudioMixer（audioMixer 非空）：同步写入 mixer 暴露参数（传统方式）；
///   - 无论是否有 mixer：写 PlayerPrefs + 通知 AudioManager.RefreshVolumes() 立即更新各源音量
///     （项目当前无 mixer 资产，源音量路径是实际生效通道）。
/// </summary>
public class AudioSettingsManager : MonoBehaviour
{
    public static AudioSettingsManager Instance { get; private set; }

    [Header("Audio Mixer（可选）")]
    [Tooltip("项目中的 AudioMixer 资源引用；留空 = 仅走 AudioManager 源音量路径（当前项目状态）。")]
    public AudioMixer audioMixer;

    [Header("Exposed Parameters")]
    [Tooltip("AudioMixer 中暴露的音效音量参数名")]
    public string sfxParameter = "SFXVolume";
    [Tooltip("AudioMixer 中暴露的音乐音量参数名")]
    public string musicParameter = "MusicVolume";

    private const string SFX_KEY = "Audio_SFX";
    private const string MUSIC_KEY = "Audio_Music";
    private const string VOICE_KEY = "Audio_Voice";
    private const string UI_KEY = "Audio_UI";
    private const float DEFAULT_VOLUME = 0.8f;

    /// <summary>PlayerPrefs 落盘防抖：滑块拖动期间每帧 SetFloat，Save 只在停止变更 0.5s 后执行一次。</summary>
    private const float SAVE_DEBOUNCE = 0.5f;
    private bool _saveDirty;
    private Coroutine _saveRoutine;

    /// <summary>自举装配（幂等）：无实例时创建常驻实例。由 GameManager.Start 调用。</summary>
    public static AudioSettingsManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("AudioSettingsManager");
        return go.AddComponent<AudioSettingsManager>();
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // DontDestroyOnLoad 必须在场景加载完成后调用（Awake 期调用会随场景卸载被销毁）
        DontDestroyOnLoad(gameObject);
        MigrateLegacyKeys();
        LoadAndApply();
    }

    /// <summary>
    /// 旧用户数据一次性迁移：2026-08-24 前 UI 音量跟随 SFX（无独立 Audio_UI key），
    /// 首次启动补写当前 SFX 值作为 UI 初始音量，之后独立调节。
    /// </summary>
    void MigrateLegacyKeys()
    {
        if (!PlayerPrefs.HasKey(UI_KEY))
        {
            PlayerPrefs.SetFloat(UI_KEY, GetSFXVolume());
            PlayerPrefs.Save();
        }
    }

    /// <summary>设置音效音量 (0.0 ~ 1.0)。</summary>
    public void SetSFXVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SFX_KEY, volume);
        ScheduleSave(); // 防抖落盘：拖动期间不每帧写盘
        ApplyVolume(sfxParameter, volume);
        if (AudioManager.Instance != null) AudioManager.Instance.RefreshVolumes();
    }

    /// <summary>设置音乐音量 (0.0 ~ 1.0)。</summary>
    public void SetMusicVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(MUSIC_KEY, volume);
        ScheduleSave();
        ApplyVolume(musicParameter, volume);
        if (AudioManager.Instance != null) AudioManager.Instance.RefreshVolumes();
    }

    /// <summary>
    /// 设置 UI 音效音量 (0.0 ~ 1.0)。
    /// 2026-08-24 起独立（此前跟随 SFX）：菜单/Card/提示音独立一路，
    /// 设置面板提供独立滑块（Audio_UI key）。
    /// </summary>
    public void SetUIVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(UI_KEY, volume);
        ScheduleSave();
        if (AudioManager.Instance != null) AudioManager.Instance.RefreshVolumes();
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
    /// 获取当前 UI 音效音量 (0.0 ~ 1.0)。独立 key（2026-08-24 起不再跟随 SFX）。
    /// </summary>
    public float GetUIVolume()
    {
        return PlayerPrefs.GetFloat(UI_KEY, DEFAULT_VOLUME);
    }

    /// <summary>
    /// 获取旁白音量 (0.0 ~ 1.0)。独立 key（2026-08-24 起设置面板提供独立滑块）。
    /// </summary>
    public float GetVoiceVolume()
    {
        return PlayerPrefs.GetFloat(VOICE_KEY, DEFAULT_VOLUME);
    }

    /// <summary>设置旁白音量 (0.0 ~ 1.0)；Debug 面板/未来 Voice 设置 UI 用。</summary>
    public void SetVoiceVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(VOICE_KEY, volume);
        ScheduleSave();
        if (AudioManager.Instance != null) AudioManager.Instance.RefreshVolumes();
    }

    /// <summary>防抖落盘：停止变更 SAVE_DEBOUNCE 秒后统一 Save（拖动滑块不再每帧写磁盘）。</summary>
    private void ScheduleSave()
    {
        _saveDirty = true;
        // 编辑器模式（非 Play）下协程不执行，防抖永不落盘——直接写，保证工具/测试脚本设置生效
        if (!Application.isPlaying) { PlayerPrefs.Save(); return; }
        if (_saveRoutine != null) return; // 已有定时器在跑，刷新即可
        _saveRoutine = StartCoroutine(SaveDebounced());
    }

    IEnumerator SaveDebounced()
    {
        while (_saveDirty)
        {
            _saveDirty = false;
            yield return new WaitForSecondsRealtime(SAVE_DEBOUNCE);
        }
        PlayerPrefs.Save();
        _saveRoutine = null;
    }

    /// <summary>保底落盘：暂停/退出时即使防抖定时器未到期也立即写盘。</summary>
    void OnApplicationPause(bool paused)
    {
        if (paused && _saveRoutine != null) { StopCoroutine(_saveRoutine); _saveRoutine = null; }
        if (_saveDirty) PlayerPrefs.Save();
    }

    void OnApplicationQuit()
    {
        if (_saveDirty) PlayerPrefs.Save();
    }

    /// <summary>从 PlayerPrefs 加载并应用音量。</summary>
    public void LoadAndApply()
    {
        ApplyVolume(sfxParameter, GetSFXVolume());
        ApplyVolume(musicParameter, GetMusicVolume());
        // 源音量路径：同步各 AudioSource 到持久化值
        if (AudioManager.Instance != null) AudioManager.Instance.RefreshVolumes();
    }

    private void ApplyVolume(string parameter, float linearVolume)
    {
        if (audioMixer == null)
            return;

        float dB = LinearToDecibel(linearVolume);
        audioMixer.SetFloat(parameter, dB);
    }

    /// <summary>线性值 (0~1) 转 dB (-80~0)</summary>
    private float LinearToDecibel(float linear)
    {
        if (linear <= 0.001f)
            return -80f;
        return Mathf.Log10(linear) * 20f;
    }
}
