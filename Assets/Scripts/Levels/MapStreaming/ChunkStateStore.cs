using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ChunkState 内存快照库：coord → ChunkState，单局内只增不减（不随 Chunk 卸载丢失）。
/// 由 MapStreamingSystem 持有唯一实例（States 属性），写入侧统一走 GetOrCreate，只读路径走 TryGet（不产生空快照污染）。
/// ToJson/FromJson 是 Phase 4 存档落盘预留的序列化出入口（JsonUtility 轻量序列化，不引外部库）。
/// </summary>
public class ChunkStateStore
{
    readonly Dictionary<ChunkCoord, ChunkState> states = new Dictionary<ChunkCoord, ChunkState>();

    /// <summary>已有快照的 Chunk 数（调试 HUD 用）。</summary>
    public int Count => states.Count;

    /// <summary>全部快照（存档遍历 / 调试用）。</summary>
    public IEnumerable<KeyValuePair<ChunkCoord, ChunkState>> All => states;

    /// <summary>获取或创建该 Chunk 的快照（写入侧统一入口）。</summary>
    public ChunkState GetOrCreate(ChunkCoord coord)
    {
        if (!states.TryGetValue(coord, out var state))
        {
            state = new ChunkState();
            states.Add(coord, state);
        }
        return state;
    }

    /// <summary>尝试读取（只读路径用；无快照返回 false，不创建空条目）。</summary>
    public bool TryGet(ChunkCoord coord, out ChunkState state)
    {
        return states.TryGetValue(coord, out state);
    }

    // ── 序列化（显式存档时由存档系统调用；异步 IO 不进  任务队列） ──

    /// <summary>落盘包装：Dictionary 无法被 JsonUtility 序列化，转平行数组。</summary>
    [System.Serializable]
    public class SaveBlob
    {
        public List<ChunkCoord> coords = new List<ChunkCoord>();
        public List<ChunkState> states = new List<ChunkState>();
    }

    /// <summary>全部快照 → JSON（TODO(Phase 4): 存档系统接入时调用，落盘为异步 IO）。</summary>
    public string ToJson()
    {
        var blob = new SaveBlob();
        foreach (var kv in states)
        {
            blob.coords.Add(kv.Key);
            blob.states.Add(kv.Value);
        }
        return JsonUtility.ToJson(blob);
    }

    /// <summary>JSON → 快照库（TODO(Phase 4): 读档时调用；覆盖当前全部内容）。</summary>
    public void FromJson(string json)
    {
        states.Clear();
        if (string.IsNullOrEmpty(json)) return;
        var blob = JsonUtility.FromJson<SaveBlob>(json);
        if (blob == null) return;
        int n = Mathf.Min(blob.coords.Count, blob.states.Count);
        for (int i = 0; i < n; i++)
            states[blob.coords[i]] = blob.states[i];
    }
}
