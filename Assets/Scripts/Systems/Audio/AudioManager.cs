using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局音频管理器（四路分离，跨场景常驻）——统一配置入口 + 门面：
///   - 配置：四类音频资产（SfxBank / StageBgmMap / VoiceClipSet / MonsterSkillAudioConfig）
///     全部收敛在本组件字段（空引用时 Resources 兜底加载）；
///   - 控制：四个通道控制器（BgmController / CombatAudioManager / UiAudioController / VoiceController）
///     运行时自动装配（EnsureControllers 按 GetComponent 优先 / AddComponent 补挂，引用不序列化），
///     各自统一接口（Initialize / RefreshVolume / PauseAll / ResumeAll / StopAll）；
///     事件接线集中在 AudioEventBinder（同对象挂载）；
///   - 门面：对外 API 同名同语义（转发对应控制器），外部调用零改动。
/// 单例：场景挂载版与 EnsureInstance（prefab 实例化优先）共存，Awake 先注册者胜出。
/// 音量：控制器统一经本组件属性读 AudioSettingsManager 持久化值（BGM/SFX/UI/Voice 四键）。
/// 场景 BGM：由各场景的 SceneBgm 组件经门面请求（本类不主动查找 SceneBgm）。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ── 统一配置：四类音频资产（Inspector 一处配齐）──

    [Header("统一配置（四类音频资产）")]
    [Tooltip("音效映射表（SfxId→clip）；为空时从 Resources/Audio/SfxBank 兜底加载。")]
    public SfxBank sfxBank;
    [Tooltip("阶段 BGM 映射表；为空时从 Resources/Audio/StageBgmMap 兜底加载。")]
    public StageBgmMap stageBgmMap;
    [Tooltip("旁白映射表；为空时从 Resources/Audio/VoiceClipSet 兜底加载。")]
    public VoiceClipSet voiceClipSet;
    [Tooltip("怪物技能施放音映射表（七罪×技能类别）；为空时从 Resources/Audio/MonsterSkillAudioConfig 兜底加载。")]
    public MonsterSkillAudioConfig monsterSkillAudioConfig;

    // ── 通道控制器（运行时自动装配：EnsureControllers 按 GetComponent 优先 / AddComponent 补挂；
    //    引用不序列化、Inspector 不显示——控制器组件本身挂在对象上，各自参数在组件上配置）──

    [System.NonSerialized] public BgmController bgmController;      // BGM：双源交叉淡化 + 终态优先仲裁 + Duck
    [System.NonSerialized] public CombatAudioManager sfxController; // 战斗音效：SFX 池 + 节流 + 3D + 循环租借
    [System.NonSerialized] public UiAudioController uiController;   // UI：独立源 + 通用点击音 + 静默例外
    [System.NonSerialized] public VoiceController voiceController;  // 旁白：单源 + BGM Duck 协作

    // ── 四路音量（控制器统一经此读取；AudioSettingsManager 持久化）──

    public float BgmVolume => AudioSettingsManager.Instance != null ? AudioSettingsManager.Instance.GetMusicVolume() : 0.8f;
    public float SfxVolume => AudioSettingsManager.Instance != null ? AudioSettingsManager.Instance.GetSFXVolume() : 0.8f;
    public float UiVolume => AudioSettingsManager.Instance != null ? AudioSettingsManager.Instance.GetUIVolume() : 0.8f;
    public float VoiceVolume => AudioSettingsManager.Instance != null ? AudioSettingsManager.Instance.GetVoiceVolume() : 0.8f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 资产兜底：空引用 → Resources 加载（加载失败仅告警不阻塞，全部播放静默，Debug 面板可查缺失）
        if (sfxBank == null)
        {
            sfxBank = Resources.Load<SfxBank>("Audio/SfxBank");
            if (sfxBank == null)
                Debug.LogWarning("[AudioManager] Resources 加载 SfxBank 失败（Audio/SfxBank）——SfxId 通道全部静默，属预期（资产未建时）。");
        }
        if (stageBgmMap == null)
        {
            stageBgmMap = Resources.Load<StageBgmMap>("Audio/StageBgmMap");
            if (stageBgmMap == null)
                Debug.LogWarning("[AudioManager] Resources 加载 StageBgmMap 失败（Audio/StageBgmMap）——阶段 BGM 退化为场景曲，属预期（资产未建时）。");
        }
        if (voiceClipSet == null)
        {
            voiceClipSet = Resources.Load<VoiceClipSet>("Audio/VoiceClipSet");
            if (voiceClipSet == null)
                Debug.LogWarning("[AudioManager] Resources 加载 VoiceClipSet 失败（Audio/VoiceClipSet）——旁白全部静默，属预期（资产未建时）。");
        }
        if (monsterSkillAudioConfig == null)
        {
            monsterSkillAudioConfig = Resources.Load<MonsterSkillAudioConfig>("Audio/MonsterSkillAudioConfig");
            if (monsterSkillAudioConfig == null)
                Debug.LogWarning("[AudioManager] Resources 加载 MonsterSkillAudioConfig 失败（Audio/MonsterSkillAudioConfig）——怪物技能音全部静默，属预期（资产未建时）。");
        }

        EnsureControllers();
    }

    void Start()
    {
        // DontDestroyOnLoad 必须在场景加载完成（Start 阶段）后调用：
        // Awake 期间（场景加载中）调用可能不生效，导致常驻对象随场景卸载被销毁（音频中断）。
        // 若 Start 前已被销毁（极端时序），下一场景的 GameManager.Start 会 EnsureInstance 自愈重建。
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>控制器自动装配：缺省补挂 + 统一 Initialize（注入 Owner + 音源就绪）。</summary>
    void EnsureControllers()
    {
        if (bgmController == null) bgmController = GetComponent<BgmController>();
        if (bgmController == null) bgmController = gameObject.AddComponent<BgmController>();
        if (sfxController == null) sfxController = GetComponent<CombatAudioManager>();
        if (sfxController == null) sfxController = gameObject.AddComponent<CombatAudioManager>();
        if (uiController == null) uiController = GetComponent<UiAudioController>();
        if (uiController == null) uiController = gameObject.AddComponent<UiAudioController>();
        if (voiceController == null) voiceController = GetComponent<VoiceController>();
        if (voiceController == null) voiceController = gameObject.AddComponent<VoiceController>();

        bgmController.Initialize(this);
        sfxController.Initialize(this);
        uiController.Initialize(this);
        voiceController.Initialize(this);
    }

    // ── 自举装配（项目惯例：EnsureInstance 幂等） ──

    public static AudioManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        // 统一配置入口：优先实例化 Resources/Audio/AudioManager.prefab
        // （prefab 内已配置资产引用与通道控制器/控制脚本）；prefab 缺失时回退空对象创建（资产走 Resources 兜底）。
        var prefab = Resources.Load<GameObject>("Audio/AudioManager");
        if (prefab != null)
        {
            var am = Instantiate(prefab).GetComponent<AudioManager>();
            if (am != null) return am;
        }
        var go = new GameObject("AudioManager");
        return go.AddComponent<AudioManager>();
    }

    // ── 门面：BGM（转发 BgmController）──

    /// <summary>BGM 乒乓源 A 引用（转发 BgmController；调试面板显示当前曲目用）。</summary>
    public AudioSource bgmSource => bgmController != null ? bgmController.bgmSource : null;

    /// <summary>BGM 乒乓源 B 引用（转发 BgmController；调试面板显示当前曲目用）。</summary>
    public AudioSource bgmSource2 => bgmController != null ? bgmController.bgmSource2 : null;

    /// <summary>Scene 层请求（SceneBgm 唯一入口；场景切换清 Phase/Wave/Override 层）。</summary>
    public void RequestSceneBgm(AudioClip clip) => bgmController?.RequestSceneBgm(clip);

    /// <summary>Phase 层请求（binder 按 RunPhase 调用）。</summary>
    public void SetPhaseBgm(RunPhase phase) => bgmController?.SetPhaseBgm(phase);

    /// <summary>Waves 阶段逐波解析（binder 在每次 OnWaveStarted 调用；玩家侧编号 1-based）。</summary>
    public void SetWaveBgm(int waveNumber) => bgmController?.SetWaveBgm(waveNumber);

    /// <summary>Override 层压栈（token 去重幂等）。</summary>
    public void PushOverrideBgm(string token, AudioClip clip, float fadeOverride = 0f) =>
        bgmController?.PushOverrideBgm(token, clip, fadeOverride);

    /// <summary>Override 层出栈（token 不存在时 no-op）。</summary>
    public void PopOverrideBgm(string token) => bgmController?.PopOverrideBgm(token);

    /// <summary>清除 Phase/Wave/Override 层（返回主菜单等场景兜底）。</summary>
    public void ClearPhaseAndOverrides() => bgmController?.ClearPhaseAndOverrides();

    /// <summary>BGM Duck 压栈（Voice 播放期间压低）。</summary>
    public void PushBgmDuck(float factor = 0.3f) => bgmController?.PushDuck(factor);

    /// <summary>BGM Duck 出栈（与 PushBgmDuck 成对）。</summary>
    public void PopBgmDuck() => bgmController?.PopDuck();

    // ── 门面：SFX（转发 CombatAudioManager，channel=Ui 分流在其内部）──

    /// <summary>统一播放：查 SfxBank 条目；channel=Ui 走 UI 路，World 走池（可选 3D）。</summary>
    public bool Play(SfxId id, Vector3? worldPos = null, float volumeScale = 1f)
    {
        if (sfxController == null) return false;
        return sfxController.Play(id, worldPos, volumeScale);
    }

    /// <summary>
    /// 带字段 override 的统一播放。**专用于 UI 通道的场景序列化字段迁移**（CoreChoiceUI 卡音）：
    /// overrideClip 非空 → 走 UI 路播字段音（旧行为）；为空 → 走 fallbackId 的 bank 条目。
    /// 注意：不适用于 World 通道音效迁移（override 分支忽略 worldPos/3D/pitch），
    /// World 音效字段迁移请直接用 Play(SfxId) + 条目配置。
    /// </summary>
    public bool PlayWithOverride(AudioClip overrideClip, SfxId fallbackId, Vector3? worldPos = null)
    {
        if (overrideClip != null)
        {
            uiController?.PlayClip(overrideClip);
            return true;
        }
        return Play(fallbackId, worldPos);
    }

    // ── 门面：循环音效租借（转发 CombatAudioManager）──

    public CombatAudioManager.SfxLoopHandle StartSfxLoop(SfxId id, Transform parent)
    {
        if (sfxController == null) return default;
        return sfxController.StartSfxLoop(id, parent);
    }

    public void StopSfxLoop(CombatAudioManager.SfxLoopHandle handle) => sfxController?.StopSfxLoop(handle);

    // ── 门面：Voice（转发 VoiceController）──

    /// <summary>Voice 播放期间 BGM 压低系数（转发 VoiceController；叙事调度器自管 Duck 时读取）。</summary>
    public float voiceBgmDuckFactor
    {
        get => voiceController != null ? voiceController.voiceBgmDuckFactor : 0.3f;
        set { if (voiceController != null) voiceController.voiceBgmDuckFactor = value; }
    }

    /// <summary>播放旁白音（单源；duckBgm=true 时 BGM 自动压低，播完恢复）。</summary>
    public bool PlayVoice(string audioId, VoiceChannel channel = VoiceChannel.Mythic, bool duckBgm = true)
    {
        if (voiceController == null) return false;
        return voiceController.PlayVoice(audioId, channel, duckBgm);
    }

    /// <summary>Voice 是否正在播放（叙事调度器完成检测）。</summary>
    public bool IsVoicePlaying => voiceController != null && voiceController.IsPlaying;

    /// <summary>当前 Voice clip 长度（有音频行字幕时长用）；未播放返回 0。</summary>
    public float VoiceClipLength => voiceController != null ? voiceController.CurrentClipLength : 0f;

    /// <summary>暂停 Voice（保留播放位置）。</summary>
    public void PauseVoice() => voiceController?.PauseAll();

    /// <summary>恢复 Voice。</summary>
    public void ResumeVoice() => voiceController?.ResumeAll();

    /// <summary>停止 Voice（取消/打断/完成）。</summary>
    public void StopVoice() => voiceController?.StopAll();

    // ── 门面：UI 点击音静默例外（弹窗自管，OCP）──

    /// <summary>进入"UI 点击音静默"（如选卡弹窗打开期间，由专属音接管）。计数器叠加。</summary>
    public void PushUiClickMute() => uiController?.PushClickMute();

    /// <summary>退出"UI 点击音静默"（与 PushUiClickMute 成对调用）。</summary>
    public void PopUiClickMute() => uiController?.PopClickMute();

    // ── 音量刷新（设置面板变更时；分发四通道）──

    public void RefreshVolumes()
    {
        bgmController?.RefreshVolume();
        sfxController?.RefreshVolume();
        uiController?.RefreshVolume();
        voiceController?.RefreshVolume();
    }

    // ── 缺失清单（静态门面：由 CombatAudioManager 承载，Debug 面板用）──

    public static IReadOnlyDictionary<string, int> GetMissingSfxNames() => CombatAudioManager.GetMissingSfxNames();

    public static IReadOnlyDictionary<SfxId, int> GetMissingSfxIds() => CombatAudioManager.GetMissingSfxIds();

    public static void RegisterMissingSfxName(string name) => CombatAudioManager.RegisterMissingSfxName(name);
}
