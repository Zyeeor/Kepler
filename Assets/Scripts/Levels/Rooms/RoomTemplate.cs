using UnityEngine;
using System;
using System.Collections.Generic;

// ============================================================
// 核心数据结构
// ============================================================

public enum RoomType { Combat, Boss, Reward, Start }
public enum RoomState { Loading, Ready, Combat, Cleared, ExitPhase, Completed }

/// <summary>
/// 波次模式：
/// CountKill = 数量波：刷满 totalCount 只后不再补，玩家清完触发选卡；
/// Timed = 时间波：持续 duration 秒，时间到即结算触发选卡。
/// </summary>
public enum WaveMode { CountKill, Timed }

/// <summary>
/// 波内编队条目：引用一个 MonsterWaveDef（刷哪些怪）+ 本波内的抽取权重（占比）。
/// 权重放在条目上而非 def 资产内——同一 def 可被多波引用，各自独立调占比。
/// </summary>
[Serializable]
public class WaveDefEntry
{
    [Tooltip("怪物编队（MonsterWaveDef 资产：本组刷哪些怪、组内数量）。")]
    public MonsterWaveDef def;
    [Tooltip("本波内该编队的抽取权重（占比）：值越大越常出。仅本波生效，不影响其他波引用同一编队。")]
    [Min(0f)] public float weight = 1f;
}

[Serializable]
public class WaveConfig
{
    [Tooltip("怪物编队表：本波按条目 weight 抽取刷怪（每波独立占比，同一编队可被多波引用）。")]
    public List<WaveDefEntry> weightedTable = new List<WaveDefEntry>();
    [Tooltip("数量波：本波刷怪总数，刷满后不再补充；玩家清完场上本波怪触发选卡。仅 CountKill 模式生效。")]
    [Min(1)] public int totalCount = 20;
    [Tooltip("时间波：本波时长（秒）。时间到即结算（触发选卡），剩余在场怪按回收策略处理。仅 Timed 模式生效。")]
    [Min(1f)] public float duration = 60f;
    [Tooltip("时间波：本波累计刷怪总数上限（含被击杀/回收）。0 = 不限制（仅受 MonsterSpawner 全场配额 maxCombatMonsters 约束）。仅 Timed 模式生效。")]
    [Min(0)] public int maxSpawnCount = 0;
    [Tooltip("本波清场后选卡：true=双选（连续选 2 张），false=单选。")]
    public bool doublePick = false;
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
    [Tooltip("Index of the RoomTemplate in RoomManager.roomTemplates to load when player passes through. -1 = no linked room.")]
    public int leadsToRoomIndex = -1;
}

[Serializable]
public class CoreEntry
{
    [Tooltip("Core prefab to spawn.")]
    public GameObject prefab;
    [Tooltip("Optional transform override for spawn location. If set, uses this transform's position/rotation instead of the vector fields below.")]
    public Transform locationTransform;
    [Tooltip("World position where the core appears (ignored if locationTransform is set).")]
    public Vector3 position;
    [Tooltip("Rotation of the core (ignored if locationTransform is set).")]
    public Vector3 rotation;
    [Tooltip("Distance at which the interaction UI pops up.")]
    public float interactRadius = 3f;
    [Tooltip("If true, core only spawns after all waves are cleared.")]
    public bool spawnAfterWavesCleared = true;

    public Vector3 GetPosition(Transform roomRoot) => locationTransform != null ? roomRoot.position + locationTransform.localPosition : roomRoot.position + position;
    public Quaternion GetRotation() => locationTransform != null ? locationTransform.rotation : Quaternion.Euler(rotation);
}

// ============================================================
// RoomTemplate — 挂在场景 GameObject 上直接编辑
// ============================================================

public class RoomTemplate : MonoBehaviour
{
    [Header("Identity")]
    public string roomName = "New Room";
    public RoomType roomType = RoomType.Combat;

    [Header("Room Position")]
    [Tooltip("Overall room world position offset. All content (objects, spawns, core, exits) is shifted by this.")]
    public Vector3 roomPosition;
    [Tooltip("Overall room rotation offset.")]
    public Vector3 roomRotation;

    [Header("Room Prefab")]
    [Tooltip("房间 Prefab（必须挂有 RoomInstance 组件）。")]
    public GameObject roomPrefab;

    [Header("Spawn Settings")]
    [Tooltip("敌人物件生成区域半径（以房间中心为原点）。")]
    public float spawnRadius = 10f;
    [Tooltip("生成点之间最小间距。")]
    public float spawnClearRadius = 2.5f;

    [Header("Waves")]
    [Tooltip("波次模式（整体）：CountKill=全部波为数量波（刷满 totalCount 清完过波）；Timed=全部波为时间波（撑满 duration 过波）。")]
    public WaveMode waveMode = WaveMode.CountKill;
    public List<WaveConfig> waves = new List<WaveConfig>();
    [Tooltip("第一波开始前的等待时间（秒）。")]
    public float gracePeriod = 2f;

    [Header("Objects")]
    public List<ObjectPlacementEntry> placedObjects = new List<ObjectPlacementEntry>();

    [Header("Exits")]
    public List<ExitEntry> exits = new List<ExitEntry>();

    [Header("Core")]
    [Tooltip("Core prefab and placement. Interactable object that triggers a choice UI.")]
    public CoreEntry core;

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

        // Core position
        if (core.prefab != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position + core.position, core.interactRadius);
            Gizmos.DrawWireCube(transform.position + core.position, Vector3.one * 0.8f);
        }
    }
}
