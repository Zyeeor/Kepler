using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Editor 房间搭建工具。挂在场景里的一个 GameObject 上。
/// 使用方式：
///   1. 在场景中创建一个空 GameObject，命名为 "RoomManager"
///   2. 挂上这个脚本
///   3. 再创建一个子 GameObject，挂上 RoomTemplate，在 Inspector 里编辑
///   4. 把 RoomTemplate GameObject 拖入下方的 roomTemplates 列表
///   5. 运行场景时选择要加载的索引
/// </summary>
public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    [Header("Room Templates")]
    [Tooltip("所有可用的房间模板（场景中的 GameObject，挂有 RoomTemplate 组件）。")]
    public List<RoomTemplate> roomTemplates = new List<RoomTemplate>();

    [Header("Runtime — Load On Start")]
    [Tooltip("启动时自动加载哪个房间模板（1-based；0 = 不自动加载）。")]
    public int loadRoomIndexOnStart = 0;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Debug.Log($"[RoomManager] Start: templates={roomTemplates.Count}, loadIndex={loadRoomIndexOnStart}");
        if (loadRoomIndexOnStart > 0 && loadRoomIndexOnStart <= roomTemplates.Count)
        {
            Debug.Log($"[RoomManager] Loading room index {loadRoomIndexOnStart - 1}");
            LoadRoomByIndex(loadRoomIndexOnStart - 1);
        }
        else
        {
            Debug.LogWarning($"[RoomManager] Not loading: loadRoomIndexOnStart={loadRoomIndexOnStart}, templateCount={roomTemplates.Count}");
        }
    }

    public void LoadRoomByIndex(int index)
    {
        if (index < 0 || index >= roomTemplates.Count)
        {
            Debug.LogError($"[RoomManager] Invalid room index: {index}");
            return;
        }
        var template = roomTemplates[index];
        if (template == null) return;

        var room = RoomLoader.Instance.LoadRoom(template);
        if (room != null)
        {
            RoomFlowController.Instance.Initialize(template, room);
            RoomFlowController.Instance.StartRoom();
        }
    }

    public void LoadRoomByName(string name)
    {
        var template = roomTemplates.Find(t => t != null && t.roomName == name);
        if (template == null)
        {
            Debug.LogError($"[RoomManager] Room template not found: {name}");
            return;
        }
        var room = RoomLoader.Instance.LoadRoom(template);
        if (room != null)
        {
            RoomFlowController.Instance.Initialize(template, room);
            RoomFlowController.Instance.StartRoom();
        }
    }
}
