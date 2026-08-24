using UnityEngine;

/// <summary>
/// 音频通道控制器统一基类（BGM / SFX / UI / Voice 四通道共约）：
///   - 生命周期由 AudioManager 统一驱动：Initialize(owner) → 注入归属 + 音源就绪；
///   - 统一接口：RefreshVolume（音量刷新）、PauseAll / ResumeAll / StopAll（通道级控制）；
///   - 音源创建在所属 AudioManager 对象下（层级：AudioManager/&lt;Channel&gt;）；
///   - 配置资产（SfxBank 等）与四路音量统一经 Owner 读取，控制器互不直连、互不持静态引用。
/// </summary>
public abstract class AudioChannelController : MonoBehaviour
{
    /// <summary>所属 AudioManager（Initialize 注入；资产引用 / 四路音量统一经此读取）。</summary>
    public AudioManager Owner { get; private set; }

    /// <summary>装配入口：注入归属 + 确保音源就绪。AudioManager.Awake 统一调用。</summary>
    public virtual void Initialize(AudioManager owner)
    {
        Owner = owner;
        EnsureSources();
    }

    /// <summary>创建/校验本通道音源（幂等；缺省自动补建，挂本对象下）。</summary>
    protected abstract void EnsureSources();

    /// <summary>音量刷新（设置面板变更时）：读最新音量值并应用（感知曲线）。</summary>
    public abstract void RefreshVolume();

    /// <summary>暂停本通道全部音源。</summary>
    public abstract void PauseAll();

    /// <summary>恢复本通道全部音源。</summary>
    public abstract void ResumeAll();

    /// <summary>停止本通道全部音源。</summary>
    public abstract void StopAll();

    /// <summary>感知响度曲线：线性滑块值 → 源音量（pow 2 让低段调节更细腻，符合人耳感知）。</summary>
    protected static float Perceptual(float linear) => linear * linear;

    /// <summary>Mixer 是否接管衰减（接管后源音量固定 1，防双轨双重衰减）。</summary>
    protected static bool MixerActive =>
        AudioSettingsManager.Instance != null && AudioSettingsManager.Instance.audioMixer != null;
}
