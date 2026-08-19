using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 全局音频管理器（三路分离，跨场景常驻）：
///   - BGM：双源乒乓交叉淡化（真 CrossFade，无静默谷），循环播放
///   - SFX：战斗/世界音效，AudioSource 池（并发播放，位置可选；per-clip 节流 + 轮询调度）
///   - UI：UI 音效（按钮/弹窗/选卡），独立一路，与战斗音效分离
///
/// 音量：读 AudioSettingsManager 持久化值（BGM=Music / SFX / UI 三键），无管理器时用默认值。
/// 感知曲线：源音量施加 pow(v,2)（滑块低段更细腻，符合人耳响度感知）；mixer 接入后源固定 1，
/// 衰减全交 mixer（防双轨双重衰减）。
/// 资产：所有 AudioClip 由检查器配置；未配置（null）时静默跳过——框架先行，资产后接。
///
/// 事件挂接：订阅 WaveManager（波次开始/完成/全完成）与 PossessionManager（附身成功）。
/// 订阅幂等：先退后订（C# += 不去重，直接 += 会重复挂 handler 导致叠音）。
/// 订阅时序：本组件为常驻单例，可能在 WaveManager 之前创建（如主菜单先建 AudioManager，
/// 对局场景才出现 WaveManager）——OnEnable/Start/sceneLoaded 三处补订阅（先退后订保证幂等）。
/// 遵守"订阅必退订"（OnDisable/OnDestroy 双退订，防常驻悬空委托）。
///
/// 场景 BGM：由各场景的 SceneBgm 组件自举触发（本类不主动查找 SceneBgm——双通道收敛，
/// 见 .docs 审核文档 B1）。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM（音乐）")]
    [Tooltip("BGM 淡入淡出时长（秒）。")]
    [Min(0f)] public float bgmFadeDuration = 1f;
    [Tooltip("BGM 乒乓源 A（自动创建，交叉淡化用；两源固定不重写）。")]
    public AudioSource bgmSource;
    [Tooltip("BGM 乒乓源 B（自动创建，交叉淡化用；两源固定不重写）。")]
    public AudioSource bgmSource2;

    [Header("SFX（战斗/世界音效池）")]
    [Tooltip("音效池大小：并发音效数上限。")]
    [Min(1)] public int sfxPoolSize = 6;
    [Tooltip("音效池源（自动创建，长度=sfxPoolSize）。")]
    public AudioSource[] sfxPool;
    [Tooltip("同 clip 最小重播间隔（秒）：窗口内重复触发直接丢弃，防高频战斗事件（受击/技能命中）爆音。")]
    [Min(0f)] public float sfxMinInterval = 0.06f;

    [Header("UI（UI 音效，独立一路）")]
    [Tooltip("UI 音效源（自动创建）。")]
    public AudioSource uiSource;

    [Header("游戏事件音效（可配置 Clip，null 跳过）")]
    public AudioClip waveStartSfx;
    public AudioClip waveClearSfx;
    public AudioClip waveAllCompleteSfx;
    public AudioClip possessionStartSfx;

    [Header("通用 UI 点击音")]
    [Tooltip("任意 UI 点击触发（EventSystem 指针判定）。需要静默的弹窗用 PushUiClickMute/PopUiClickMute 自管。")]
    public AudioClip uiClickSfx;

    /// <summary>CrossFade 进行中标志（防止重复触发打断淡入）。</summary>
    bool _bgmFading;
    /// <summary>当前输出 BGM 的源（乒乓 A/B 之一）。两个字段固定指向各自源对象，禁止重写（避免别名错乱）。</summary>
    AudioSource _activeBgm;
    /// <summary>BGM 淡入淡出协程引用（定点停止，避免 StopAllCoroutines 误杀其他协程）。</summary>
    Coroutine _bgmFadeRoutine;
    /// <summary>UI 点击音静默计数器（弹窗 Push/Pop 自管，AudioManager 不感知任何具体 UI）。</summary>
    int _uiClickMuteCount;
    /// <summary>SFX 池轮询指针（全忙时均匀覆盖，消灭固定 pool[0] 热点）。</summary>
    int _sfxCursor;
    /// <summary>per-clip 最近播放时间（节流用）。</summary>
    readonly Dictionary<AudioClip, float> _lastSfxTime = new Dictionary<AudioClip, float>();
    /// <summary>点击判定用 Raycast 结果缓冲（复用，避免每帧分配）。</summary>
    readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>();

    float BgmVolume => AudioSettingsManager.Instance != null ? AudioSettingsManager.Instance.GetMusicVolume() : 0.8f;
    float SfxVolume => AudioSettingsManager.Instance != null ? AudioSettingsManager.Instance.GetSFXVolume() : 0.8f;
    float UiVolume => AudioSettingsManager.Instance != null ? AudioSettingsManager.Instance.GetUIVolume() : 0.8f;

    /// <summary>感知响度曲线：线性滑块值 → 源音量（pow 2 让低段调节更细腻，符合人耳感知）。</summary>
    static float Perceptual(float linear) => linear * linear;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 通用 UI 点击音兜底：常驻实例（GameManager 创建，非场景组件）没有场景序列化配置，
        // 从 Resources 加载默认资产；场景内显式配置优先（本兜底不覆盖已配置值）。
        // 测试期资产 Assets/Resources/Audio/UI/ui_click.wav；正式资产接入后仍可显式覆盖。
        if (uiClickSfx == null)
        {
            uiClickSfx = Resources.Load<AudioClip>("Audio/UI/ui_click");
            if (uiClickSfx == null)
                Debug.LogWarning("[AudioManager] Resources 加载默认 UI 点击音失败（Audio/UI/ui_click）——若未配置则属正常静默。");
        }

        EnsureSources();
    }

    void OnEnable()
    {
        SubscribeEvents();
        // 常驻对象可能先于 WaveManager/PossessionManager 创建：场景加载完成后补订阅（幂等）
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // DontDestroyOnLoad 必须在场景加载完成（Start 阶段）后调用：
        // Awake 期间（场景加载中）调用可能不生效，导致常驻对象随场景卸载被销毁（音频中断）。
        // 若 Start 前已被销毁（极端时序），下一场景的 GameManager.Start 会 EnsureInstance 自愈重建。
        DontDestroyOnLoad(gameObject);
        // 所有场景对象 Awake 完成后补订阅：OnEnable 时 WaveManager 可能尚未 Awake（Instance 未设置），
        // Start 必在其后执行，保证本场景的游戏事件不会漏挂（先退后订，幂等）。
        SubscribeEvents();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SubscribeEvents();
        // 场景 BGM 由 SceneBgm 组件自举触发（本类不再查找——双通道收敛，见审核 B1）
    }

    void Update()
    {
        // 通用 UI 点击音：仅当点击命中"可交互 Selectable"（按钮/滑块/开关等，含其子级文本）
        // 才发声——点在背景/面板等不可交互 UI 上不响。
        // 静默例外由各弹窗 PushUiClickMute/PopUiClickMute 自管（AudioManager 不感知具体 UI）。
        if (uiClickSfx == null || _uiClickMuteCount > 0) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (!TryHitInteractable()) return;
        PlayUiSfx(uiClickSfx);
    }

    /// <summary>指针点击位置是否命中可交互的 Selectable（Button/Slider/Toggle 等，含其子级文本）。</summary>
    bool TryHitInteractable()
    {
        var es = EventSystem.current;
        if (es == null) return false;
        var ped = new PointerEventData(es) { position = Input.mousePosition };
        _uiRaycastResults.Clear();
        es.RaycastAll(ped, _uiRaycastResults);
        foreach (var r in _uiRaycastResults)
        {
            if (r.gameObject == null) continue;
            var sel = r.gameObject.GetComponentInParent<Selectable>();
            if (sel != null && sel.isActiveAndEnabled && sel.interactable)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 订阅游戏事件（幂等：先退后订，防重复订阅叠音）。
    /// 注意：C# 事件 += 不自动去重（Delegate.Combine 重复追加），本方法会被 OnEnable / Start /
    /// sceneLoaded 多次调用，若只加不退，同一 handler 会注册多份 → 波次事件双倍触发、音效叠音。
    /// </summary>
    void SubscribeEvents()
    {
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveStarted -= OnWaveStarted;
            WaveManager.Instance.OnWaveStarted += OnWaveStarted;
            WaveManager.Instance.OnWaveCompleted -= OnWaveCompleted;
            WaveManager.Instance.OnWaveCompleted += OnWaveCompleted;
            WaveManager.Instance.OnAllWavesComplete -= OnAllWavesComplete;
            WaveManager.Instance.OnAllWavesComplete += OnAllWavesComplete;
        }
        if (PossessionManager.Instance != null)
        {
            PossessionManager.Instance.OnPossessionStarted -= OnPossessionStarted;
            PossessionManager.Instance.OnPossessionStarted += OnPossessionStarted;
        }
    }

    void UnsubscribeEvents()
    {
        if (WaveManager.Instance != null) WaveManager.Instance.OnWaveStarted -= OnWaveStarted;
        if (WaveManager.Instance != null) WaveManager.Instance.OnWaveCompleted -= OnWaveCompleted;
        if (WaveManager.Instance != null) WaveManager.Instance.OnAllWavesComplete -= OnAllWavesComplete;
        if (PossessionManager.Instance != null) PossessionManager.Instance.OnPossessionStarted -= OnPossessionStarted;
    }

    void OnDisable()
    {
        UnsubscribeEvents();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnDestroy()
    {
        // 常驻对象销毁时同样退订（场景重载时 OnDisable 已处理；此处双保险）
        UnsubscribeEvents();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ── 自举装配（项目惯例：EnsureInstance 幂等） ──

    public static AudioManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("AudioManager");
        return go.AddComponent<AudioManager>();
    }

    // ── UI 点击音静默例外（弹窗自管，OCP） ──

    /// <summary>进入"UI 点击音静默"（如选卡弹窗打开期间，由专属音接管）。计数器叠加。</summary>
    public void PushUiClickMute()
    {
        _uiClickMuteCount++;
    }

    /// <summary>退出"UI 点击音静默"（与 Push 成对调用）。</summary>
    public void PopUiClickMute()
    {
        _uiClickMuteCount = Mathf.Max(0, _uiClickMuteCount - 1);
    }

    // ── 内部：源就绪 ──

    void EnsureSources()
    {
        if (bgmSource == null)
        {
            var go = new GameObject("BGM");
            go.transform.SetParent(transform);
            bgmSource = go.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
        }
        if (bgmSource2 == null)
        {
            var go = new GameObject("BGM2");
            go.transform.SetParent(transform);
            bgmSource2 = go.AddComponent<AudioSource>();
            bgmSource2.loop = true;
            bgmSource2.playOnAwake = false;
        }
        if (_activeBgm == null)
            _activeBgm = bgmSource; // 初始主源 = A

        if (uiSource == null)
        {
            var go = new GameObject("UI");
            go.transform.SetParent(transform);
            uiSource = go.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
        }

        if (sfxPool == null || sfxPool.Length != sfxPoolSize)
        {
            sfxPool = new AudioSource[sfxPoolSize];
            for (int i = 0; i < sfxPoolSize; i++)
            {
                var go = new GameObject("SFX_" + i);
                go.transform.SetParent(transform);
                sfxPool[i] = go.AddComponent<AudioSource>();
                sfxPool[i].playOnAwake = false;
            }
        }
    }

    // ── 公共 API ──

    /// <summary>播放 BGM（同 clip 不重启；切换时双源真交叉淡化，无静默谷）。</summary>
    public void PlayBgm(AudioClip clip)
    {
        if (clip == null || bgmSource == null || bgmSource2 == null) return;
        // 正在播放/淡入同一曲：不打断（场景加载重复触发、同曲重请求均安全）
        if (_bgmFading) return;
        if (_activeBgm != null && _activeBgm.isPlaying && _activeBgm.clip == clip) return;
        if (_bgmFadeRoutine != null) { StopCoroutine(_bgmFadeRoutine); _bgmFadeRoutine = null; }
        _bgmFadeRoutine = StartCoroutine(CrossFadeBgm(clip));
    }

    /// <summary>停止 BGM（淡出）。</summary>
    public void StopBgm()
    {
        if (_bgmFadeRoutine != null) { StopCoroutine(_bgmFadeRoutine); _bgmFadeRoutine = null; }
        _bgmFadeRoutine = StartCoroutine(FadeOutBgm());
    }

    /// <summary>播放战斗/世界音效（池选空闲源；可选世界位置；per-clip 节流）。</summary>
    public void PlaySfx(AudioClip clip, Vector3? worldPos = null, float volumeScale = 1f)
    {
        if (clip == null) return;
        if (!CanPlayNow(clip)) return; // 节流：窗口内同 clip 丢弃
        var src = GetFreeSfxSource();
        if (src == null) return;
        if (worldPos.HasValue)
        {
            src.spatialBlend = 1f;
            src.transform.position = worldPos.Value;
        }
        else src.spatialBlend = 0f;
        // 基础音量存源 volume（RefreshVolumes 可统一刷新），scale 走 PlayOneShot 运行时叠加
        src.volume = Perceptual(SfxVolume);
        src.PlayOneShot(clip, volumeScale);
    }

    /// <summary>播放 UI 音效（独立一路）。</summary>
    public void PlayUiSfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || uiSource == null) return;
        uiSource.volume = Perceptual(UiVolume);
        uiSource.PlayOneShot(clip, volumeScale);
    }

    /// <summary>
    /// 设置面板调整音量后调用：三路源音量立即生效（BGM / 播放中的 SFX 池 / UI）。
    /// - mixer 生效时源音量固定 1，衰减全交 mixer（防双轨双重衰减）；
    /// - BGM 淡入/淡出中跳过直写（协程每帧读最新目标音量，无跳变冲突）。
    /// </summary>
    public void RefreshVolumes()
    {
        bool mixerActive = AudioSettingsManager.Instance != null && AudioSettingsManager.Instance.audioMixer != null;
        if (!_bgmFading && _activeBgm != null)
            _activeBgm.volume = mixerActive ? 1f : Perceptual(BgmVolume);
        if (uiSource != null)
            uiSource.volume = mixerActive ? 1f : Perceptual(UiVolume);
        if (sfxPool != null)
            foreach (var s in sfxPool)
                if (s != null && s.isPlaying) s.volume = mixerActive ? 1f : Perceptual(SfxVolume);
    }

    // ── 内部：节流与池调度 ──

    /// <summary>per-clip 最小重播间隔节流（高频事件防爆音，对调用方透明）。</summary>
    bool CanPlayNow(AudioClip clip)
    {
        if (sfxMinInterval <= 0f) return true;
        float now = Time.unscaledTime;
        if (_lastSfxTime.TryGetValue(clip, out float last) && now - last < sfxMinInterval)
            return false;
        _lastSfxTime[clip] = now;
        return true;
    }

    /// <summary>轮询调度取池源：优先空闲；全忙时轮询指针覆盖（非固定 pool[0] 热点）。</summary>
    AudioSource GetFreeSfxSource()
    {
        if (sfxPool == null || sfxPool.Length == 0) return null;
        for (int i = 0; i < sfxPool.Length; i++)
        {
            int idx = (_sfxCursor + i) % sfxPool.Length;
            if (sfxPool[idx] != null && !sfxPool[idx].isPlaying)
            {
                _sfxCursor = (idx + 1) % sfxPool.Length;
                return sfxPool[idx];
            }
        }
        var busy = sfxPool[_sfxCursor];
        _sfxCursor = (_sfxCursor + 1) % sfxPool.Length;
        return busy;
    }

    // ── 内部：双源交叉淡化 ──

    /// <summary>双源乒乓交叉淡化：旧源淡出的同时新源淡入（无静默谷）。</summary>
    IEnumerator CrossFadeBgm(AudioClip clip)
    {
        _bgmFading = true;
        // 当前在播源（首次播放 = null）
        var from = (_activeBgm != null && _activeBgm.isPlaying && _activeBgm.clip != null) ? _activeBgm : null;
        // 乒乓选另一源（from==A → B；from==null 首播 → A）
        var to = (from == bgmSource) ? bgmSource2 : bgmSource;
        if (to == null) { _bgmFading = false; _bgmFadeRoutine = null; yield break; }

        float fromVol = from != null ? from.volume : 0f;
        to.clip = clip;
        to.volume = 0f;
        to.Play();

        float t = 0f;
        while (t < bgmFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = bgmFadeDuration > 0f ? Mathf.Clamp01(t / bgmFadeDuration) : 1f;
            if (from != null) from.volume = Mathf.Lerp(fromVol, 0f, k);
            // 目标音量每帧实时读（调音量即时生效，与 RefreshVolumes 无冲突）
            to.volume = Mathf.Lerp(0f, Perceptual(BgmVolume), k);
            yield return null;
        }
        if (from != null) { from.Stop(); from.clip = null; }
        _activeBgm = to; // 当前输出源切换（两字段保持固定指向各自对象，不重写）
        _bgmFading = false;
        _bgmFadeRoutine = null;
    }

    IEnumerator FadeOutBgm()
    {
        if (_activeBgm == null || !_activeBgm.isPlaying) { _bgmFadeRoutine = null; yield break; }
        float t = 0f;
        float start = _activeBgm.volume;
        while (t < bgmFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _activeBgm.volume = Mathf.Lerp(start, 0f, Mathf.Clamp01(t / bgmFadeDuration));
            yield return null;
        }
        _activeBgm.Stop();
        _activeBgm.clip = null;
        _bgmFadeRoutine = null;
    }

    // ── 游戏事件响应（Clip 未配置时静默） ──

    /// <summary>波次开始。仅第 1 波发声——语义为"对局开始"（与策划对齐确认前保留，见审核 Y7）。</summary>
    void OnWaveStarted(int waveIndex, WaveConfig wave)
    {
        if (waveIndex == 0) PlaySfx(waveStartSfx);
    }

    void OnWaveCompleted(int waveIndex)
    {
        PlaySfx(waveClearSfx);
    }

    void OnAllWavesComplete()
    {
        PlaySfx(waveAllCompleteSfx);
    }

    void OnPossessionStarted(MonsterActor body)
    {
        PlaySfx(possessionStartSfx);
    }
}
