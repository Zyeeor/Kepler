using UnityEngine;

/// <summary>行为树节点运行状态。</summary>
public enum BTNodeState
{
    /// <summary>成功：Selector 短路 / Sequence 继续。</summary>
    Success,
    /// <summary>失败：Selector 尝试下一子节点 / Sequence 中断。</summary>
    Failure,
}

/// <summary>
/// 行为树节点基类。Evaluate 每次调用即时返回状态，无内部记忆、无 Running——
/// 框架定位为「节拍式反应选择器」：决策节拍由驱动方（AIController）控制，
/// 每次决策点从根重新评估，长时动作由「脉冲 + 协程」在 Actor 层补偿，
/// 因此不存在经典 BT 的 Running 状态清理负担（池化/附身切换天然安全）。
/// </summary>
public abstract class BTNode
{
    public abstract BTNodeState Evaluate(BTBlackboard bb);
}

/// <summary>条件节点：Check 为 true → Success，否则 Failure。</summary>
public abstract class BTCondition : BTNode
{
    public sealed override BTNodeState Evaluate(BTBlackboard bb)
    {
        return Check(bb) ? BTNodeState.Success : BTNodeState.Failure;
    }

    protected abstract bool Check(BTBlackboard bb);
}

/// <summary>动作节点：Execute 为 true → Success，否则 Failure。</summary>
public abstract class BTAction : BTNode
{
    public sealed override BTNodeState Evaluate(BTBlackboard bb)
    {
        return Execute(bb) ? BTNodeState.Success : BTNodeState.Failure;
    }

    protected abstract bool Execute(BTBlackboard bb);
}
