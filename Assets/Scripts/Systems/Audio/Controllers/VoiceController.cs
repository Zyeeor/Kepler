using System.Collections;
using UnityEngine;

/// <summary>
/// Voice 通道控制器（挂 AudioManager 下）：旁白单源（同时只播一条）+ BGM Duck 协作。
/// 录音映射经 Owner.VoiceClipSet 读取（统一配置入口在 AudioManager）；
/// Duck 经 Owner.BgmController 压栈（跨通道协作统一经 Owner 完成，控制器互不直连）。
/// 完整旁白调度（何时说、说什么）由 NarrativeScheduler 承载，本控制器只管"播"。
/// </summary>
public class VoiceController : AudioChannelController
{
    [Header("Voice 参数")]
    [System.NonSerialized] public AudioSource voiceSource; // 运行时自动创建（非序列化，非配置项）
    [Tooltip("Voice 播放期间 BGM 压低系数（0.3 = 压到 30%）。")]
    [Range(0f, 1f)] public float voiceBgmDuckFactor = 0.3f;

    /// <summary>Duck 短淡协程引用。Voice 播完自动 Pop 的协程。</summary>
    Coroutine _voiceDuckRoutine;
    /// <summary>当前播放的 Voice clip 引用（PlayOneShot 不写 source.clip，故单独记录）。</summary>
    AudioClip _currentVoiceClip;

    protected override void EnsureSources()
    {
        if (voiceSource == null)
        {
            var go = new GameObject("Voice");
            go.transform.SetParent(transform);
            voiceSource = go.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
        }
    }

    // ── 播放 ──

    /// <summary>
    /// 播放旁白音（独立 Voice 源，不占 SFX 池；单源 = 新 Voice 打断旧的）。
    /// audioId 未配置 / clip 空 → 静默 false（不阻塞文本与流程，Narrative Baseline §5）。
    /// duckBgm=true 时播放期间 BGM 自动压低（计数器，多句叠加安全），播完自动恢复。
    /// </summary>
    public bool PlayVoice(string audioId, VoiceChannel channel = VoiceChannel.Mythic, bool duckBgm = true)
    {
        if (string.IsNullOrEmpty(audioId)) return false;
        if (voiceSource == null) return false;
        var set = Owner != null ? Owner.voiceClipSet : null;
        if (set == null)
            set = Resources.Load<VoiceClipSet>("Audio/VoiceClipSet");
        if (set == null || !set.TryGet(audioId, channel, out var clip) || clip == null)
            return false;

        // 打断清理（无论新旧 Voice 的 duckBgm 设置）：旧 Voice 若还挂着 Duck（AutoPop 未跑完），
        // 先归还旧 Push 再处理新 Push——保证"一条 Voice 恰好一对 Push/Pop"，防计数泄漏导致 BGM 永久压低。
        if (_voiceDuckRoutine != null)
        {
            StopCoroutine(_voiceDuckRoutine);
            _voiceDuckRoutine = null;
            PopDuck();
        }

        if (duckBgm && Owner != null && Owner.bgmController != null)
            Owner.bgmController.PushDuck(voiceBgmDuckFactor);
        voiceSource.volume = MixerActive ? 1f : Perceptual(Owner.VoiceVolume);
        voiceSource.Stop(); // 打断策略：单源同时只播一条（细化归叙事调度）
        _currentVoiceClip = clip; // 记录当前 clip（PlayOneShot 不写 voiceSource.clip，ClipLength 依赖此引用）
        voiceSource.PlayOneShot(clip);
        if (duckBgm && Owner != null && Owner.bgmController != null)
            _voiceDuckRoutine = StartCoroutine(AutoPopDuck(clip.length));
        return true;
    }

    void PopDuck()
    {
        if (Owner != null && Owner.bgmController != null)
            Owner.bgmController.PopDuck();
    }

    IEnumerator AutoPopDuck(float clipLength)
    {
        yield return new WaitForSecondsRealtime(clipLength + 0.1f);
        PopDuck();
        _voiceDuckRoutine = null;
    }

    // ── 控制面（叙事调度器用：查询/暂停/恢复/停止）──

    /// <summary>Voice 是否正在播放（叙事调度器完成检测）。</summary>
    public bool IsPlaying => voiceSource != null && voiceSource.isPlaying;

    /// <summary>当前 Voice clip 引用（PlayOneShot 不写 source.clip，故单独记录）。</summary>
    public AudioClip CurrentClip => _currentVoiceClip;

    /// <summary>当前 Voice clip 长度（有音频行字幕时长用）；未播放返回 0。</summary>
    public float CurrentClipLength => _currentVoiceClip != null ? _currentVoiceClip.length : 0f;

    /// <summary>暂停 Voice（保留播放位置）。</summary>
    public void Pause()
    {
        if (voiceSource != null) voiceSource.Pause();
    }

    /// <summary>恢复 Voice。</summary>
    public void Resume()
    {
        if (voiceSource != null) voiceSource.UnPause();
    }

    /// <summary>停止 Voice（取消/打断/完成；清理 clip 引用）。</summary>
    public void Stop()
    {
        if (voiceSource == null) return;
        voiceSource.Stop();
        _currentVoiceClip = null;
    }

    // ── 统一接口 ──

    public override void RefreshVolume()
    {
        if (voiceSource != null)
            voiceSource.volume = MixerActive ? 1f : Perceptual(Owner.VoiceVolume);
    }

    public override void PauseAll() => Pause();

    public override void ResumeAll() => Resume();

    public override void StopAll() => Stop();
}
