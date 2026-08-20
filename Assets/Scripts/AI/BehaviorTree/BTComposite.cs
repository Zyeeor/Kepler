using System;

/// <summary>
/// 选择器：按序尝试子节点，返回第一个非 Failure 的结果（优先级互斥短路）。
/// </summary>
public class BTSelector : BTNode
{
    private readonly BTNode[] children;

    public BTSelector(params BTNode[] children)
    {
        this.children = children ?? Array.Empty<BTNode>();
    }

    public override BTNodeState Evaluate(BTBlackboard bb)
    {
        for (int i = 0; i < children.Length; i++)
        {
            BTNodeState s = children[i].Evaluate(bb);
            if (s != BTNodeState.Failure) return s;
        }
        return BTNodeState.Failure;
    }
}

/// <summary>
/// 顺序节点：按序执行，全部 Success → Success；任一 Failure 立即中断并返回 Failure。
/// </summary>
public class BTSequence : BTNode
{
    private readonly BTNode[] children;

    public BTSequence(params BTNode[] children)
    {
        this.children = children ?? Array.Empty<BTNode>();
    }

    public override BTNodeState Evaluate(BTBlackboard bb)
    {
        for (int i = 0; i < children.Length; i++)
        {
            BTNodeState s = children[i].Evaluate(bb);
            if (s == BTNodeState.Failure) return BTNodeState.Failure;
        }
        return BTNodeState.Success;
    }
}

/// <summary>
/// 权重随机选择器：按权重随机挑一个子节点执行，失败则继续从其余子节点中按权重随机挑；
/// 全部失败才返回 Failure。用于"多个攻击行为按概率互斥选择"。
/// </summary>
public class BTWeightedSelector : BTNode
{
    public struct Entry
    {
        public float Weight;
        public BTNode Node;

        public Entry(float weight, BTNode node)
        {
            Weight = weight;
            Node = node;
        }
    }

    private readonly Entry[] entries;
    private readonly bool[] tried; // 复用缓冲，避免评估时 GC

    public BTWeightedSelector(params Entry[] entries)
    {
        this.entries = entries ?? Array.Empty<Entry>();
        tried = new bool[this.entries.Length];
    }

    public override BTNodeState Evaluate(BTBlackboard bb)
    {
        if (entries.Length == 0) return BTNodeState.Failure;
        for (int i = 0; i < entries.Length; i++) tried[i] = false;

        while (true)
        {
            // 计算未尝试子节点的总权重
            float total = 0f;
            for (int i = 0; i < entries.Length; i++)
                if (!tried[i]) total += entries[i].Weight;
            if (total <= 0f) return BTNodeState.Failure;

            // 权重采样（种子流：宿主怪 AI 子流，同种子可复现）
            float r = bb.Host.AiRandomValue() * total;
            float acc = 0f;
            int pick = -1;
            for (int i = 0; i < entries.Length; i++)
            {
                if (tried[i]) continue;
                acc += entries[i].Weight;
                if (r <= acc) { pick = i; break; }
            }
            if (pick < 0)
            {
                // 浮点兜底：取最后一个未尝试
                for (int i = entries.Length - 1; i >= 0; i--)
                    if (!tried[i]) { pick = i; break; }
            }

            tried[pick] = true;
            BTNodeState s = entries[pick].Node.Evaluate(bb);
            if (s != BTNodeState.Failure) return s;

            // 全部尝试完仍未成功
            bool allTried = true;
            for (int i = 0; i < entries.Length; i++)
                if (!tried[i]) { allTried = false; break; }
            if (allTried) return BTNodeState.Failure;
        }
    }
}
