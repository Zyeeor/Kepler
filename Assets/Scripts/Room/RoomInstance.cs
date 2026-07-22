using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 房间 Prefab 上的运行时根节点。
/// 挂在一个空 GameObject 上，该 GameObject 是房间 Prefab 的根。
/// </summary>
public class RoomInstance : MonoBehaviour
{
    [Header("Spawn Points")]
    [Tooltip("Player spawn point.")]
    public Transform playerSpawnPoint;
    [Tooltip("Named spawn point groups (used by WaveConfig.spawnPointGroup).")]
    public List<SpawnPointGroup> spawnPointGroups = new List<SpawnPointGroup>();

    [Header("Exits")]
    [Tooltip("Exit anchor points (arrow / door positions).")]
    public List<Transform> exitAnchors = new List<Transform>();

    [Header("Camera")]
    [Tooltip("Camera boundary (XZ plane).")]
    public Bounds cameraBounds = new Bounds(Vector3.zero, new Vector3(40, 0, 40));

    /// <summary>运行时房间配置引用。</summary>
    [HideInInspector] public RoomTemplate config;
    /// <summary>运行时上下文（唯一 ID、当前状态等）。</summary>
    [HideInInspector] public RoomRuntimeContext context;

    /// <summary>初始化房间实例。</summary>
    public void Initialize(RoomTemplate cfg, RoomRuntimeContext ctx)
    {
        config = cfg;
        context = ctx;
        context.State = RoomState.Ready;
    }

    /// <summary>获取指定名称的生成点组。</summary>
    public SpawnPointGroup GetSpawnPointGroup(string name)
    {
        return spawnPointGroups.Find(g => g.groupName == name);
    }

    /// <summary>启用/禁用出口交互。</summary>
    public void SetExitsEnabled(bool enabled)
    {
        foreach (var anchor in exitAnchors)
        {
            if (anchor != null)
            {
                var col = anchor.GetComponent<Collider>();
                if (col != null) col.enabled = enabled;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + cameraBounds.center, cameraBounds.size);

        if (playerSpawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(playerSpawnPoint.position, 0.5f);
        }

        foreach (var exit in exitAnchors)
        {
            if (exit != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(exit.position, 0.3f);
            }
        }
    }
}

/// <summary>生成点组 — 一组带名称的 Transform。</summary>
[System.Serializable]
public class SpawnPointGroup
{
    public string groupName;
    public List<Transform> points = new List<Transform>();
}

/// <summary>房间运行时上下文。</summary>
public class RoomRuntimeContext
{
    public string RoomInstanceId;
    public RoomState State;
    public int CurrentWaveIndex;
    public int TotalWaves;
    public int EnemiesAlive;
}
