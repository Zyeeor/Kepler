using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BGM 通道控制器（挂 AudioManager 下）：
///   双源乒乓交叉淡化（真 CrossFade，无静默谷）+ 仲裁单点（合同 BGM v1.0）：
///     终态（Result/Failed）> Override 栈（soul/elite）> 基础层（Phase/Wave，Action 三态）> Scene 兜底；
///   Action 三态：Play 切曲 / Inherit 保持当前曲 / Stop 淡出停止；Waves 阶段逐波配置（waveTiers，未配波不切）。
/// 配置：fade 时长在本组件字段；阶段映射资产经 Owner.StageBgmMap 读取（统一配置入口在 AudioManager）。
/// 对场景 BGM：由各场景的 SceneBgm 组件经 Owner 门面请求（本控制器不主动查找 SceneBgm）。
/// </summary>
public class BgmController : AudioChannelController
{
    [Header("BGM 参数")]
    [Tooltip("BGM 淡入淡出时长（秒）。")]
    [Min(0f)] public float bgmFadeDuration = 1f;
    [System.NonSerialized] public AudioSource bgmSource;   // 运行时自动创建（非序列化，非配置项）
    [System.NonSerialized] public AudioSource bgmSource2;  // 运行时自动创建（非序列化，非配置项）

    /// <summary>CrossFade 进行中标志（防止重复触发打断淡入）。</summary>
    bool _bgmFading;
    /// <summary>当前输出 BGM 的源（乒乓 A/B 之一）。两字段固定指向各自源对象，禁止重写。</summary>
    AudioSource _activeBgm;
    /// <summary>BGM 淡入淡出协程引用（定点停止，避免 StopAllCoroutines 误杀其他协程）。</summary>
    Coroutine _bgmFadeRoutine;
    /// <summary>BGM Duck 系数（1=不压低；Voice 播放期间乘 voiceBgmDuckFactor）。</summary>
    float _bgmDuckFactor = 1f;
    /// <summary>Duck 压栈计数（多句叠加安全）。</summary>
    int _bgmDuckCount;
    /// <summary>Duck 短淡协程引用。</summary>
    Coroutine _duckFadeRoutine;
    /// <summary>下一次 CrossFade 的时长覆盖（ReconcileBgm 按槽位 fadeOverride 设置；≤0 走全局 bgmFadeDuration）。</summary>
    float _pendingFadeOverride;

    struct BgmOverride
    {
        public string token;
        public AudioClip clip;
        public float fadeOverride;
    }

    AudioClip _sceneBgm;                                          // Scene 层（SceneBgm 请求）
    BgmAction _baseAction;                                        // 基础层（Phase/Wave）目标动作
    AudioClip _baseClip;                                          // 基础层 Play 目标 clip
    float _baseFade;                                              // 基础层 fade 覆盖（≤0 用全局）
    bool _baseExplicit;                                           // 当前阶段是否有显式配置（Inherit 也是显式=保持；false=无映射落 Scene 层）
    bool _bgmStopped;                                             // Stop 已执行守卫（防重复淡出）
    RunPhase _currentPhase = RunPhase.Opening;                    // 当前 RunPhase（SetPhaseBgm 记录）
    int _currentWaveNumber;                                       // 当前玩家侧波次编号（1-based；0=未知）
    bool _wavesActive;                                            // 是否处于 Waves 阶段（未配置波次时保持当前曲，不回落 Scene 层）
    readonly List<BgmOverride> _bgmOverrideStack = new List<BgmOverride>(); // Override 层压栈

    protected override void EnsureSources()
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
    }

    // ── 播放与交叉淡化 ──

    /// <summary>播放 BGM（同 clip 不重启；切换时双源真交叉淡化，无静默谷）。</summary>
    public void PlayBgm(AudioClip clip)
    {
        if (clip == null || bgmSource == null || bgmSource2 == null)
        {
            _pendingFadeOverride = 0f; // 早退防御：不残留覆盖值（Reconcile 每次重设，此处双保险）
            return;
        }
        // 正在播放/淡入同一曲：不打断（场景加载重复触发、同曲重请求均安全）
        if (_bgmFading) return;
        if (_activeBgm != null && _activeBgm.isPlaying && _activeBgm.clip == clip)
        {
            _pendingFadeOverride = 0f; // 同曲不重启：消费掉本次覆盖，防残留被下一次切换误用
            return;
        }
        if (_bgmFadeRoutine != null) { StopCoroutine(_bgmFadeRoutine); _bgmFadeRoutine = null; }
        _bgmFadeRoutine = StartCoroutine(CrossFadeBgm(clip, _pendingFadeOverride));
        _pendingFadeOverride = 0f;
    }

    /// <summary>停止 BGM（淡出）。</summary>
    public void StopBgm()
    {
        if (_bgmFadeRoutine != null) { StopCoroutine(_bgmFadeRoutine); _bgmFadeRoutine = null; }
        _bgmFadeRoutine = StartCoroutine(FadeOutBgm());
    }

    /// <summary>双源乒乓交叉淡化：旧源淡出的同时新源淡入（无静默谷）。fadeOverride≤0 用全局 bgmFadeDuration。</summary>
    IEnumerator CrossFadeBgm(AudioClip clip, float fadeOverride = 0f)
    {
        _bgmFading = true;
        float fadeDuration = fadeOverride > 0f ? fadeOverride : bgmFadeDuration;
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
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = fadeDuration > 0f ? Mathf.Clamp01(t / fadeDuration) : 1f;
            if (from != null) from.volume = Mathf.Lerp(fromVol, 0f, k);
            // 目标音量每帧实时读（调音量即时生效）；BGM Duck 系数实时乘入
            to.volume = Mathf.Lerp(0f, Perceptual(Owner.BgmVolume) * _bgmDuckFactor, k);
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

    // ── 仲裁单点（终态 > Override 栈 > 基础层 > Scene；禁止其他组件直接 PlayBgm 切阶段曲）──

    /// <summary>Scene 层请求（SceneBgm 唯一入口）。场景切换 = 会话语境重置：清基础层/Override 层。</summary>
    public void RequestSceneBgm(AudioClip clip)
    {
        _sceneBgm = clip;
        _baseAction = BgmAction.Inherit;
        _baseClip = null;
        _baseFade = 0f;
        _baseExplicit = false;
        _bgmStopped = false;
        _currentPhase = RunPhase.Opening;
        _currentWaveNumber = 0;
        _wavesActive = false;
        _bgmOverrideStack.Clear();
        ReconcileBgm();
    }

    /// <summary>
    /// Phase 层请求（binder 按 RunPhase 调用；合同 §3.1 Action 三态语义）：
    ///   - 无映射（Opening/Tutorial）→ 清基础层回落 Scene 层；
    ///   - Inherit → 保持当前曲不切（合同 §4.3，不回落 Scene）；
    ///   - Play + clip → 切曲；Play + clip 空 → 警告并回退 Inherit（合同 §7.1）；
    ///   - Stop → 淡出停止（终态用）。
    ///   - Waves → 转逐波解析（SetWaveBgm）。
    /// </summary>
    public void SetPhaseBgm(RunPhase phase)
    {
        _currentPhase = phase;
        var map = Owner != null ? Owner.stageBgmMap : null;

        if (phase == RunPhase.Waves)
        {
            _wavesActive = true;
            SetWaveBgm(_currentWaveNumber);
            return;
        }

        _wavesActive = false;

        if (map == null || !map.TryGetPhase(phase, out var slot) || slot == null)
        {
            // 无映射（Opening/Tutorial）：清基础层回落 Scene 层
            _baseAction = BgmAction.Inherit;
            _baseClip = null;
            _baseFade = 0f;
            _baseExplicit = false;
            _bgmStopped = false;
            ReconcileBgm();
            return;
        }
        ApplySlotToBase(slot, $"阶段 {phase}");
        ReconcileBgm();
    }

    /// <summary>
    /// Waves 阶段逐波解析（binder 在每次 OnWaveStarted 调用；玩家侧编号 1-based）：
    /// waveTiers 命中 → 按槽位 action 处理（Play 切曲 / Inherit 保持 / Stop 停止）；
    /// 未命中 → Inherit 保持当前曲不切（用户逐波逻辑）。
    /// waveTiers 列表为空 → 回退旧行为（combat 槽统一波次曲）。
    /// </summary>
    public void SetWaveBgm(int waveNumber)
    {
        _currentWaveNumber = waveNumber;
        if (!_wavesActive) return; // 非 Waves 阶段：仅记录编号，等进入 Waves 时解析

        var map = Owner != null ? Owner.stageBgmMap : null;
        var tier = map != null ? map.TryGetWaveTier(waveNumber) : null;
        if (tier != null)
        {
            ApplySlotToBase(tier.slot, $"第 {waveNumber} 波");
        }
        else if (map != null && map.waveTiers.Count == 0 && map.combat != null && map.combat.action == BgmAction.Play && map.combat.clip != null)
        {
            // 旧行为兜底：未使用逐波配置时，所有波次共用 combat 槽（Play 且 clip 非空才生效）
            _baseAction = BgmAction.Play;
            _baseClip = map.combat.clip;
            _baseFade = map.combat.fadeOverride;
            _baseExplicit = true;
        }
        else
        {
            // 未配置该波：Inherit 保持当前曲（显式保持，不回落 Scene）
            _baseAction = BgmAction.Inherit;
            _baseClip = null;
            _baseFade = 0f;
            _baseExplicit = true;
        }
        ReconcileBgm();
    }

    /// <summary>把槽位按合同 Action 三态语义落到基础层目标。</summary>
    void ApplySlotToBase(StageBgmMap.Slot slot, string context)
    {
        _baseExplicit = true;
        switch (slot.action)
        {
            case BgmAction.Stop:
                _baseAction = BgmAction.Stop;
                _baseClip = null;
                _baseFade = 0f;
                break;
            case BgmAction.Play:
                if (slot.clip != null)
                {
                    _baseAction = BgmAction.Play;
                    _baseClip = slot.clip;
                    _baseFade = slot.fadeOverride;
                }
                else
                {
                    Debug.LogWarning($"[BgmController] {context} Action=Play 但 Clip 未配置——按合同 §7.1 回退 Inherit（保持当前音乐），请检查 StageBgmMap。");
                    _baseAction = BgmAction.Inherit;
                    _baseClip = null;
                    _baseFade = 0f;
                }
                break;
            default: // Inherit
                _baseAction = BgmAction.Inherit;
                _baseClip = null;
                _baseFade = 0f;
                break;
        }
    }

    /// <summary>Override 层压栈（token 去重幂等：同 token 重复 Push 不叠加）。clip 空 = no-op。</summary>
    public void PushOverrideBgm(string token, AudioClip clip, float fadeOverride = 0f)
    {
        if (string.IsNullOrEmpty(token) || clip == null) return;
        for (int i = 0; i < _bgmOverrideStack.Count; i++)
            if (_bgmOverrideStack[i].token == token) return;
        _bgmOverrideStack.Add(new BgmOverride { token = token, clip = clip, fadeOverride = fadeOverride });
        ReconcileBgm();
    }

    /// <summary>Override 层出栈（token 不存在时 no-op）；栈空回落 Phase→Scene。</summary>
    public void PopOverrideBgm(string token)
    {
        for (int i = _bgmOverrideStack.Count - 1; i >= 0; i--)
        {
            if (_bgmOverrideStack[i].token == token)
            {
                _bgmOverrideStack.RemoveAt(i);
                break;
            }
        }
        ReconcileBgm();
    }

    /// <summary>清除基础层/Override 层（binder sceneLoaded 兜底：返回主菜单 EndRun 不广播阶段的场景）。</summary>
    public void ClearPhaseAndOverrides()
    {
        _baseAction = BgmAction.Inherit;
        _baseClip = null;
        _baseFade = 0f;
        _baseExplicit = false;
        _bgmStopped = false;
        _currentPhase = RunPhase.Opening;
        _currentWaveNumber = 0;
        _wavesActive = false;
        _bgmOverrideStack.Clear();
        ReconcileBgm();
    }

    /// <summary>
    /// 仲裁单点（合同 §2.2 / §5）：
    ///   终态（Result/Failed）> Override 栈顶（soul/elite）> 基础层（Phase/Wave 按 Action 三态）> Scene。
    ///   - 终态期间 Override 不覆盖（合同 §5：Result/Fail > Final > Override > Wave Tier）；
    ///   - Stop → 淡出停止（_bgmStopped 守卫防重复）；
    ///   - Inherit（含未配置波次）→ 保持当前曲，不回落 Scene 层；
    ///   - 无映射（_baseExplicit=false）→ 回落 Scene 层。
    /// </summary>
    void ReconcileBgm()
    {
        bool terminal = _currentPhase == RunPhase.Result || _currentPhase == RunPhase.Failed;

        // 终态优先：Result/Failed 期间忽略 Override 栈，只看基础层目标
        if (terminal)
        {
            ApplyBaseTarget();
            return;
        }

        if (_bgmOverrideStack.Count > 0)
        {
            var top = _bgmOverrideStack[_bgmOverrideStack.Count - 1];
            _bgmStopped = false;
            _pendingFadeOverride = top.fadeOverride;
            PlayBgm(top.clip);
            return;
        }
        ApplyBaseTarget();
    }

    /// <summary>应用基础层目标（Action 三态）。</summary>
    void ApplyBaseTarget()
    {
        if (_baseAction == BgmAction.Stop)
        {
            StopBgmOnce();
            return;
        }
        if (_baseAction == BgmAction.Play && _baseClip != null)
        {
            _bgmStopped = false;
            _pendingFadeOverride = _baseFade;
            PlayBgm(_baseClip);
            return;
        }
        // Inherit：保持当前曲（含 Waves 未配置波次、显式 Inherit 槽位）
        if (_baseExplicit || _wavesActive) return;
        // 无映射：回落 Scene 层
        if (_sceneBgm != null)
        {
            _bgmStopped = false;
            _pendingFadeOverride = 0f;
            PlayBgm(_sceneBgm);
        }
    }

    /// <summary>Stop 执行守卫：仅在未停止时淡出（防 Reconcile 反复触发重复淡出）。</summary>
    void StopBgmOnce()
    {
        if (_bgmStopped) return;
        _bgmStopped = true;
        StopBgm();
    }

    // ── BGM Duck（Voice 播放期间压低；计数器压栈，多句叠加安全）──

    /// <summary>Duck 压栈（factor 乘到 BGM 目标音量；Voice 控制器经 Owner 调用，其他系统也可直调）。</summary>
    public void PushDuck(float factor = 0.3f)
    {
        _bgmDuckCount++;
        if (_bgmDuckCount == 1)
            ApplyDuck(factor);
    }

    /// <summary>Duck 出栈（与 PushDuck 成对）；计数归零恢复原音量。</summary>
    public void PopDuck()
    {
        _bgmDuckCount = Mathf.Max(0, _bgmDuckCount - 1);
        if (_bgmDuckCount == 0)
            ApplyDuck(1f);
    }

    /// <summary>应用 Duck 系数（当前 BGM 0.2s 短淡到新目标音量）。</summary>
    void ApplyDuck(float factor)
    {
        _bgmDuckFactor = factor;
        if (_duckFadeRoutine != null) StopCoroutine(_duckFadeRoutine);
        _duckFadeRoutine = StartCoroutine(FadeDuckRoutine(Perceptual(Owner.BgmVolume) * factor));
    }

    IEnumerator FadeDuckRoutine(float target)
    {
        if (_activeBgm == null || !_activeBgm.isPlaying || _bgmFading) { _duckFadeRoutine = null; yield break; }
        float from = _activeBgm.volume;
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            _activeBgm.volume = Mathf.Lerp(from, target, t / 0.2f);
            yield return null;
        }
        _activeBgm.volume = target;
        _duckFadeRoutine = null;
    }

    // ── 统一接口 ──

    public override void RefreshVolume()
    {
        if (!_bgmFading && _activeBgm != null)
            _activeBgm.volume = MixerActive ? 1f : Perceptual(Owner.BgmVolume) * _bgmDuckFactor;
    }

    public override void PauseAll()
    {
        if (bgmSource != null) bgmSource.Pause();
        if (bgmSource2 != null) bgmSource2.Pause();
    }

    public override void ResumeAll()
    {
        if (bgmSource != null) bgmSource.UnPause();
        if (bgmSource2 != null) bgmSource2.UnPause();
    }

    public override void StopAll() => StopBgm();
}
