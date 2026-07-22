using UnityEngine;
using System;
using System.Collections.Generic;

// ============================================================
// 核心数据结构
// ============================================================

public enum RoomType { Combat, Boss, Reward, Start }
public enum RoomState { Loading, Ready, Combat, Cleared, ExitPhase, Completed }
public enum WaveType { Normal, Elite, Boss }

[Serializable]
public class EnemySpawnEntry
{
    [Tooltip("Enemy prefab to spawn.")]
    public GameObject enemyPrefab;
    [Tooltip("How many of this enemy in this wave.")]
    public int count;
    [Tooltip("Spawn delay from wave start.")]
    public float delay;
}

[Serializable]
public class WaveConfig
{
    public WaveType waveType = WaveType.Normal;
    [Tooltip("Seconds before this wave starts (relative to room start).")]
    public float startTime;
    public List<EnemySpawnEntry> enemies = new List<EnemySpawnEntry>();
    [Tooltip("Optional spawn point group name on the RoomInstance (empty = random around room center).")]
    public string spawnPointGroup;
}

[Serializable]
public class ObjectPlacementEntry
{
    public GameObject prefab;
    [Tooltip("How many of this object to place.")]
    public int amount = 1;
    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale = Vector3.one;
}

[Serializable]
public class ExitEntry
{
    [Tooltip("Exit prefab (door, portal, etc).")]
    public GameObject prefab;
    [Tooltip("Position in the room where the exit is placed.")]
    public Vector3 position;
    [Tooltip("Rotation of the exit.")]
    public Vector3 rotation;
}

// ============================================================
// RoomTemplate — 挂在场景 GameObject 上直接编辑
// ============================================================

public class RoomTemplate : MonoBehaviour
{
    [Header("Identity")]
    public string roomName = "New Room";
    public RoomType roomType = RoomType.Combat;

    [Header("Room Prefab")]
    [Tooltip("房间 Prefab（必须挂有 RoomInstance 组件）。")]
    public GameObject roomPrefab;

    [Header("Spawn Settings")]
    [Tooltip("敌人物件生成区域半径（以房间中心为原点）。")]
    public float spawnRadius = 10f;
    [Tooltip("生成点之间最小间距。")]
    public float spawnClearRadius = 2.5f;

    [Header("Waves")]
    public List<WaveConfig> waves = new List<WaveConfig>();
    [Tooltip("第一波开始前的等待时间（秒）。")]
    public float gracePeriod = 2f;

    [Header("Objects")]
    public List<ObjectPlacementEntry> placedObjects = new List<ObjectPlacementEntry>();

    [Header("Exits")]
    public List<ExitEntry> exits = new List<ExitEntry>();

    void OnDrawGizmosSelected()
    {
        // Spawn radius
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

        // Spawn clear radius
        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, spawnClearRadius);

        // Exit positions
        Gizmos.color = Color.cyan;
        foreach (var exit in exits)
            Gizmos.DrawWireSphere(transform.position + exit.position, 0.5f);

        // Object positions
        Gizmos.color = Color.yellow;
        foreach (var obj in placedObjects)
        {
            if (obj.amount == 1)
                Gizmos.DrawWireCube(transform.position + obj.position, Vector3.one * 0.5f);
        }
    }
}
