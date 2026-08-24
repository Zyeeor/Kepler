using UnityEngine;

/// <summary>
/// 显式 Boot 初始化序（Kimi 评审整改 P2-3）：
/// 消灭"谁先 Awake 谁赢"的单例初始化赌局——在首场景加载前按固定顺序创建常驻系统，
/// 场景级单例（MapStreamingSystem/CardManager/WaveManager 等）工作时这些系统必已就位。
///
/// 顺序：RunSession（会话/种子）→ AudioManager/AudioSettingsManager（音频，可能被
/// 会话日志/加载流程依赖）→ TimeScaleManager（时间域单写点）。
/// GameManager 保持场景挂载（主菜单/对局场景均有，是入口场景必备对象，Boot 不代建）。
/// </summary>
public static class BootStrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        RunSession.EnsureInstance();          // 会话：WorldSeed/进度（场景单例 Awake 读它时必非空）
        AudioManager.EnsureInstance();        // 音频常驻
        AudioSettingsManager.EnsureInstance();// 音量设置常驻
        TimeScaleManager.EnsureInstance();    // 时间域单写点
        MonsterPool.EnsureInstance();         // 怪物对象池常驻（DDOL 在 Start 完成，跨场景复用）
        NarrativeScheduler.EnsureInstance();  // 叙事调度（Access/Cue/Scheduler 常驻）
        NarrativeEventBus.EnsureInstance();   // 叙事事件总线（归一化事件源）
        Debug.Log("[BootStrapper] 常驻系统初始化序完成：RunSession → AudioManager → AudioSettingsManager → TimeScaleManager → MonsterPool → NarrativeScheduler → NarrativeEventBus。");
    }
}
