using System.Collections;
using UnityEngine;

/// <summary>
/// 场景 BGM 配置（挂在场景任意对象上）：
///   场景加载后由本组件自动请求 bgmClip（AudioManager 跨场景常驻、同曲不重启、切换时 CrossFade）。
///   留空 = 本场景不切换 BGM（保留上一场景音乐继续播放）。
/// 策划自助：给某场景配 BGM = 挂此组件 + 拖一个 AudioClip，无需改代码。
/// 若需"按玩法流程切 BGM"（波次/选卡/结算分曲），走 StageBgmMap 阶段驱动（见音频配置指南 §3）。
/// </summary>
public class SceneBgm : MonoBehaviour
{
    [Tooltip("本场景背景音乐；留空 = 保留上一场景音乐（不切换）。")]
    public AudioClip bgmClip;

    void Start()
    {
        // 音频系统自举：若本场景没有 GameManager（如直接启动/构建首场景为主菜单），
        // 由 SceneBgm 负责创建常驻 AudioManager，保证任何场景都有音频。
        if (AudioManager.Instance == null)
            AudioManager.EnsureInstance();
        if (bgmClip != null)
            // Scene 层请求（仲裁最底层）：阶段 BGM（终态/Override/基础层）存在时被其覆盖；
            // 场景切换同时清基础层/Override（会话语境重置），主菜单/无会话场景照旧兜底。
            AudioManager.Instance?.RequestSceneBgm(bgmClip);
    }
}
