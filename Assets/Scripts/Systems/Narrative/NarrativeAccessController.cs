using System;
using UnityEngine;

/// <summary>
/// Access 状态机：单调守卫 + 重置 + 广播。不感知任何 Cue/UI/音频。
/// 由 NarrativeScheduler 持有（纯 C#，非 Mono），生命周期随调度器。
/// </summary>
public class NarrativeAccessController
{
    public NarrativeAccess Current { get; private set; } = NarrativeAccess.A0;

    /// <summary>(prev, next)。Display 层刷新文本线、Debug 面板同步用。</summary>
    public event Action<NarrativeAccess, NarrativeAccess> OnAccessChanged;

    /// <summary>推进请求（幂等单调：target&lt;=Current 忽略并返回 false）。</summary>
    public bool RequestAdvance(NarrativeAccess target, string reason)
    {
        if (target <= Current) return false;
        var prev = Current;
        Current = target;
        Debug.Log($"[Narrative] Access {prev} → {target}（{reason}）");
        OnAccessChanged?.Invoke(prev, target);
        return true;
    }

    /// <summary>Debug 强制设置（契约 §10：可升可降；仅 Debug 面板调用）。</summary>
    public void ForceSet(NarrativeAccess target)
    {
        var p = Current;
        if (p == target) return;
        Current = target;
        OnAccessChanged?.Invoke(p, target);
    }

    /// <summary>读档恢复：直接赋值（恢复语义非"推进"，不触发单调守卫拒绝）。</summary>
    public void Restore(NarrativeAccess target)
    {
        var p = Current;
        Current = target;
        if (p != target) OnAccessChanged?.Invoke(p, target);
    }

    /// <summary>新局重置（Restart 只清 Run-local，契约 §9）。</summary>
    public void ResetForNewRun()
    {
        var p = Current;
        Current = NarrativeAccess.A0;
        if (p != NarrativeAccess.A0) OnAccessChanged?.Invoke(p, NarrativeAccess.A0);
    }
}
