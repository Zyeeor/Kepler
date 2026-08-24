#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

/// <summary>
/// 教学 Debug 入口（Decision #34 要求程序提供）：
///   - Editor 菜单：模拟事实 / 强制完成 Step / 重置 Profile / 查看未完成原因；
///   - 运行时：Keyboard 快捷键（Play 模式调试用）。
/// 全部经 TutorialController 公开 API 驱动，与正式路径共用判定逻辑。
/// </summary>
public static class TutorialDebugPanel
{
#if UNITY_EDITOR
    [MenuItem("Kepler/Tutorial/模拟事实: 首次击杀")]
    static void DebugFactKill() => TutorialFactBus.ReportDebug(TutorialFact.KilledFirstMonster);

    [MenuItem("Kepler/Tutorial/模拟事实: 尸体存在")]
    static void DebugFactCorpse() => TutorialFactBus.ReportDebug(TutorialFact.CorpseExists);

    [MenuItem("Kepler/Tutorial/模拟事实: 首次附身")]
    static void DebugFactPossess() => TutorialFactBus.ReportDebug(TutorialFact.PossessedFirstBody);

    [MenuItem("Kepler/Tutorial/模拟事实: 脱离附身")]
    static void DebugFactRelease() => TutorialFactBus.ReportDebug(TutorialFact.ReleasedBody);

    [MenuItem("Kepler/Tutorial/模拟事实: 死亡接力(M3 预留)")]
    static void DebugFactRelay() => TutorialFactBus.ReportDebug(TutorialFact.DeathRelaySucceeded);

    [MenuItem("Kepler/Tutorial/模拟事实: 神龛恢复(M3 预留)")]
    static void DebugFactShrine() => TutorialFactBus.ReportDebug(TutorialFact.SoulShrineRestored);

    [MenuItem("Kepler/Tutorial/重置教学 Profile")]
    static void DebugResetProfile() => TutorialProfileStore.ResetProfile();

    [MenuItem("Kepler/Tutorial/查看当前未完成 Step")]
    static void DebugDescribe()
    {
        var c = TutorialController.Instance;
        Debug.Log("[TutorialDebug]\n" + (c != null ? c.DebugDescribeActive() : "TutorialController 未在场景中"));
    }

    [MenuItem("Kepler/Tutorial/强制完成: TUT-01")]
    static void DebugCompleteT01() => ForceComplete("TUT-01");
    [MenuItem("Kepler/Tutorial/强制完成: TUT-02")]
    static void DebugCompleteT02() => ForceComplete("TUT-02");
    [MenuItem("Kepler/Tutorial/强制完成: TUT-03")]
    static void DebugCompleteT03() => ForceComplete("TUT-03");
    [MenuItem("Kepler/Tutorial/强制完成: TUT-04")]
    static void DebugCompleteT04() => ForceComplete("TUT-04");
    [MenuItem("Kepler/Tutorial/强制完成: TUT-05")]
    static void DebugCompleteT05() => ForceComplete("TUT-05");

    static void ForceComplete(string stepId)
    {
        var c = TutorialController.Instance;
        if (c != null) c.DebugForceCompleteStep(stepId);
        else Debug.LogWarning("[TutorialDebug] TutorialController 未在场景中");
    }
#endif

    /// <summary>运行时快捷键（Play 模式）：Shift+T = 查看状态；Shift+Y = 重置 Profile。</summary>
    static float nextKeyCheck;
    public static void TickRuntimeHotkeys()
    {
        if (!Application.isPlaying || Time.unscaledTime < nextKeyCheck) return;
        nextKeyCheck = Time.unscaledTime + 0.3f;
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.T))
        {
            var c = TutorialController.Instance;
            Debug.Log("[TutorialDebug]\n" + (c != null ? c.DebugDescribeActive() : "TutorialController 未在场景中"));
        }
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.Y))
        {
            TutorialProfileStore.ResetProfile();
        }
    }
}
