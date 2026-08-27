using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 音频事件订阅器（挂在 AudioManager 对象上，与四通道控制器同生命周期）：
/// 集中订阅 WaveManager / PossessionManager 等游戏事件：
///   - 音效事件：handler 内 Play(SfxId.X)（事件源保持音频无感知，播放器语义在控制器内）；
///   - BGM 事件：波次开始 → SetWaveBgm（逐波解析）、场景加载 → ClearPhaseAndOverrides（会话语境重置兜底）。
///
/// 订阅幂等：先退后订（C# += 不去重）；OnEnable/Start/sceneLoaded 三处补订阅
/// （常驻对象可能先于场景单例创建）；OnDisable/OnDestroy 双退订（防常驻悬空委托）。
/// </summary>
public class AudioEventBinder : MonoBehaviour
{
    public static AudioEventBinder Instance { get; private set; }

    [Header("行为开关（语义待确认项，可配置）")]
    [Tooltip("Wave Start 音：false=仅第 1 波（对局开始语义，现状默认）；true=每波开始。")]
    public bool waveStartEveryWave = false;
    [Tooltip("Corpse Window 音相对清场音的延迟（秒）。")]
    [Min(0f)] public float corpseWindowSfxDelay = 0.5f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // 初始状态捕获：避免开局误判"离开 BulletTime"等边沿（GameManager 若已存在取其当前状态）
        _prevState = GameManager.Instance != null ? GameManager.Instance.currentState : GameManager.GameState.Soul;
    }

    void OnEnable()
    {
        SubscribeAll();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // 常驻对象可能先于 WaveManager/PossessionManager Awake（Instance 未设置）：
        // Start 必在其后执行，补订阅保证本场景事件不漏挂（先退后订幂等）。
        DontDestroyOnLoad(gameObject);
        SubscribeAll();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _waveStartSeenThisScene = 0; // 每场景首个 OnWaveStarted 视为"本局首波"（读档 resume 场景首波 index 可能 >0）
        SubscribeAll();
        // F7 残留防御（双保险）：返回主菜单（EndRun 不广播阶段事件）时清 Phase/Override 层，
        // 让 SceneBgm 的场景曲正常接管。SceneBgm 的 RequestSceneBgm 本身也会清层，此处兜异常路径。
        if (RunSession.Instance != null && !RunSession.Instance.HasActiveRun)
            AudioManager.Instance?.ClearPhaseAndOverrides();
    }

    void OnDisable()
    {
        UnsubscribeAll();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnDestroy()
    {
        // 常驻销毁双保险退订（场景重载时 OnDisable 已处理）
        UnsubscribeAll();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    /// <summary>自举（GameManager Start/sceneLoaded 调用，幂等）。</summary>
    public static AudioEventBinder EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("AudioEventBinder");
        return go.AddComponent<AudioEventBinder>();
    }

    // ── 订阅/退订（先退后订幂等）──

    void SubscribeAll()
    {
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveStarted -= HandleWaveStarted;
            WaveManager.Instance.OnWaveStarted += HandleWaveStarted;
            WaveManager.Instance.OnWaveCompleted -= HandleWaveCompleted;
            WaveManager.Instance.OnWaveCompleted += HandleWaveCompleted;
            WaveManager.Instance.OnAllWavesComplete -= HandleAllWavesComplete;
            WaveManager.Instance.OnAllWavesComplete += HandleAllWavesComplete;
        }
        if (PossessionManager.Instance != null)
        {
            PossessionManager.Instance.OnPossessionStarted -= HandlePossessionStarted;
            PossessionManager.Instance.OnPossessionStarted += HandlePossessionStarted;
            PossessionManager.Instance.OnPossessionEndedEx -= HandlePossessionEndedEx;
            PossessionManager.Instance.OnPossessionEndedEx += HandlePossessionEndedEx;
        }
        if (RunSession.Instance != null)
        {
            RunSession.Instance.OnPhaseChanged -= HandlePhaseChanged;
            RunSession.Instance.OnPhaseChanged += HandlePhaseChanged;
        }
        if (EliteBuildDirector.Instance != null)
        {
            EliteBuildDirector.Instance.OnEliteSpawned -= HandleEliteSpawned;
            EliteBuildDirector.Instance.OnEliteSpawned += HandleEliteSpawned;
        }
        // 静态事件：先退后订同样幂等（-= 未订阅为 no-op）
        GameManager.OnStateChanged -= HandleStateChanged;
        GameManager.OnStateChanged += HandleStateChanged;
    }

    void UnsubscribeAll()
    {
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveStarted -= HandleWaveStarted;
            WaveManager.Instance.OnWaveCompleted -= HandleWaveCompleted;
            WaveManager.Instance.OnAllWavesComplete -= HandleAllWavesComplete;
        }
        if (PossessionManager.Instance != null)
        {
            PossessionManager.Instance.OnPossessionStarted -= HandlePossessionStarted;
            PossessionManager.Instance.OnPossessionEndedEx -= HandlePossessionEndedEx;
        }
        if (RunSession.Instance != null)
            RunSession.Instance.OnPhaseChanged -= HandlePhaseChanged;
        if (EliteBuildDirector.Instance != null)
            EliteBuildDirector.Instance.OnEliteSpawned -= HandleEliteSpawned;
        GameManager.OnStateChanged -= HandleStateChanged;
    }

    // ── Handler（只做音频一件事；clip 未配置静默由 AudioManager.Play 保证）──

    /// <summary>本场景已见过的波次开始次数（"本局首波"语义：读档 resume 场景首波 index 可能 >0，不能用 waveIndex==0 判定）。</summary>
    int _waveStartSeenThisScene;
    /// <summary>上一帧游戏状态（BulletTime 离开边沿判定用；Awake 捕获初始值防开局误报）。</summary>
    GameManager.GameState _prevState;

    void HandleWaveStarted(int waveIndex, WaveConfig wave)
    {
        // BGM 逐波解析：每次波开始都通知（waveIndex 为 0-based 循环索引，转玩家侧编号 +1）。
        // 合同 §8：配置了该波的曲 → 切换；未配置 → 保持当前曲（BgmController.SetWaveBgm 内保证）。
        AudioManager.Instance?.SetWaveBgm(waveIndex + 1);

        // 音效：语义开关 false=仅本局首波（对局开始，含读档恢复后第一个波次）；true=每波（待策划确认默认值）
        bool firstWave = _waveStartSeenThisScene == 0;
        _waveStartSeenThisScene++;
        if (!firstWave && !waveStartEveryWave) return;
        AudioManager.Instance?.Play(SfxId.WaveStart);
    }

    void HandleWaveCompleted(int waveIndex)
    {
        var am = AudioManager.Instance;
        am?.Play(SfxId.WaveClear);
        am?.PopOverrideBgm(EliteBgmToken); // 精英波语义：本波结束退出精英曲（初版；精确"精英死亡即退"待战斗侧事件）
        // Corpse Window：清场后尸体窗口期开始（延迟可配；与 WaveClear 同刻）
        if (corpseWindowSfxDelay >= 0f && isActiveAndEnabled)
            StartCoroutine(PlayCorpseWindowDelayed());
    }

    IEnumerator PlayCorpseWindowDelayed()
    {
        yield return new WaitForSecondsRealtime(corpseWindowSfxDelay); // realtime：选卡 timeScale=0 期间也能响
        AudioManager.Instance?.Play(SfxId.CorpseWindow);
    }

    void HandleAllWavesComplete()
    {
        AudioManager.Instance?.Play(SfxId.AllWavesComplete);
    }

    void HandlePossessionStarted(MonsterActor body)
    {
        AudioManager.Instance?.Play(SfxId.PossessionStart);
    }

    void HandlePossessionEndedEx(PossessionManager.PossessionEndReason reason)
    {
        switch (reason)
        {
            case PossessionManager.PossessionEndReason.VoluntaryRelease:
                AudioManager.Instance?.Play(SfxId.PossessionEnd);
                break;
            case PossessionManager.PossessionEndReason.BodyDied:
                AudioManager.Instance?.Play(SfxId.PossessBodyDied);
                break;
            // SystemReset（读档/场景切换）：不发声
        }
    }

    void HandleStateChanged(GameManager.GameState state)
    {
        var am = AudioManager.Instance;
        if (am == null) { _prevState = state; return; }

        // 同值重入守卫：GameManager.SwitchState 不拦截同值重复广播，直接忽略（防重复播 Start/Enter）。
        // 真正的状态往返必先经过其他状态（BulletTime→Possessed/Soul→BulletTime），边沿判定不受影响。
        if (_prevState == state) return;

        if (state == GameManager.GameState.Soul)
        {
            am.Play(SfxId.SoulEnter);
            PushBgmOverride(am, SoulBgmToken, am.stageBgmMap != null ? am.stageBgmMap.soul : null); // 灵魂态曲
        }
        else if (state == GameManager.GameState.GameOver) am.Play(SfxId.SoulDeath);
        else if (state == GameManager.GameState.BulletTime) am.Play(SfxId.BulletTimeStart);

        // 离开 BulletTime 边沿（含 GameOver 打断路径）
        if (_prevState == GameManager.GameState.BulletTime && state != GameManager.GameState.BulletTime)
            am.Play(SfxId.BulletTimeEnd);
        // 离开灵魂态（附身成功/其他）退出灵魂曲
        if (_prevState == GameManager.GameState.Soul && state != GameManager.GameState.Soul)
            am.PopOverrideBgm(SoulBgmToken);

        _prevState = state;
    }

    void HandlePhaseChanged(RunPhase phase)
    {
        var am = AudioManager.Instance;
        if (am == null) return;
        if (phase == RunPhase.Final) am.Play(SfxId.FinalBegin);
        else if (phase == RunPhase.Result) am.Play(SfxId.FinalClear);
        am.SetPhaseBgm(phase); // Phase 层 BGM 请求（Opening/Tutorial 无映射 → 清 Phase 层回落 Scene 层）
    }

    void HandleEliteSpawned(MonsterActor monster)
    {
        var am = AudioManager.Instance;
        if (am == null) return;
        if (monster != null)
            am.Play(SfxId.EliteSpawn, monster.transform.position);
        PushBgmOverride(am, EliteBgmToken, am.stageBgmMap != null ? am.stageBgmMap.elite : null); // 精英曲
    }

    // ── BGM Override 辅助 ──

    const string SoulBgmToken = "soul";
    const string EliteBgmToken = "elite";

    /// <summary>按映射槽位 Push BGM override（槽位 clip 空 = 不切曲，平滑降级）。</summary>
    static void PushBgmOverride(AudioManager am, string token, StageBgmMap.Slot slot)
    {
        if (slot == null || slot.clip == null) return;
        am.PushOverrideBgm(token, slot.clip, slot.fadeOverride, slot.volumeScale);
    }
}
