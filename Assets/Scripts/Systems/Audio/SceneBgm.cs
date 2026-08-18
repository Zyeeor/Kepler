using System.Collections;
using UnityEngine;

/// <summary>
/// 场景 BGM 配置（挂在场景任意对象上）：
///   场景加载后由本组件自动播放 bgmClip（AudioManager 跨场景常驻、同曲不重启、切换时 CrossFade）。
///   留空 = 本场景不切换 BGM（保留上一场景音乐继续播放）。
/// 策划自助：给某场景配 BGM = 挂此组件 + 拖一个 AudioClip，无需改代码。
/// 触发机制：Start 协程等待 AudioManager 实例就绪（AM 是常驻单例，可能晚于本场景创建，
/// 因此不能依赖 AudioManager 的 sceneLoaded 时序）；PlayBgm 幂等（同曲不重启、切换淡入淡出）。
/// 若需"按玩法流程切 BGM"（波次/选卡/结算分曲），走阶段驱动方案（见音频配置指南 §6 方案 B）。
/// </summary>
public class SceneBgm : MonoBehaviour
{
    [Tooltip("本场景背景音乐；留空 = 保留上一场景音乐（不切换）。")]
    public AudioClip bgmClip;

    void Start()
    {
        // 音频系统自举：若本场景没有 GameManager（如直接启动/构建首场景为主菜单），
        // 由 SceneBgm 负责创建常驻 AudioManager，保证任何场景都有音频。
        // EnsureInstance 为同步创建（AddComponent 立即执行 Awake），无需等待；
        // PlayBgm 幂等（同曲不重启、切换时交叉淡化）。
        if (AudioManager.Instance == null)
            AudioManager.EnsureInstance();
        if (bgmClip != null)
            AudioManager.Instance?.PlayBgm(bgmClip);
    }
}
