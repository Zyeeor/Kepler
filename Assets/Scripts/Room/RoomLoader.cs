using UnityEngine;
using System;

public class RoomLoader : MonoBehaviour
{
    public static RoomLoader Instance { get; private set; }

    public RoomInstance CurrentRoom { get; private set; }
    public RoomRuntimeContext CurrentContext { get; private set; }

    public event Action<RoomInstance> OnRoomLoaded;
    public event Action<RoomInstance> OnRoomUnloaded;

    void Awake()
    {
        Instance = this;
    }

    public RoomInstance LoadRoom(RoomTemplate template)
    {
        if (template == null)
        {
            Debug.LogError("[RoomLoader] Template is null");
            return null;
        }

        if (template.roomPrefab == null)
        {
            Debug.LogError($"[RoomLoader] Room prefab is null for: {template.roomName}");
            return null;
        }

        CurrentContext = new RoomRuntimeContext
        {
            RoomInstanceId = Guid.NewGuid().ToString(),
            State = RoomState.Loading,
            TotalWaves = template.waves.Count
        };

        var go = Instantiate(template.roomPrefab, template.roomPrefab.transform.position, template.roomPrefab.transform.rotation);
        go.name = $"Room_{template.roomName}_{CurrentContext.RoomInstanceId}";

        var room = go.GetComponent<RoomInstance>();
        if (room == null)
            room = go.AddComponent<RoomInstance>();

        // Apply room offset
        go.transform.position = template.roomPosition;
        go.transform.rotation = Quaternion.Euler(template.roomRotation);

        // 放置物件（在 spawnRadius 内随机散布，避开 spawnClearRadius）
        Debug.Log($"[RoomLoader] Placing {template.placedObjects.Count} object entries");
        foreach (var obj in template.placedObjects)
        {
            if (obj.prefab == null)
            {
                Debug.LogWarning("[RoomLoader] Object entry has null prefab, skipping");
                continue;
            }
            Debug.Log($"[RoomLoader] Spawning {obj.amount}x {obj.prefab.name}");
            for (int i = 0; i < obj.amount; i++)
            {
                float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float r = UnityEngine.Random.Range(template.spawnClearRadius, template.spawnRadius);
                Vector3 pos = new Vector3(Mathf.Cos(angle) * r, 0, Mathf.Sin(angle) * r);
                var placed = Instantiate(obj.prefab, go.transform);
                placed.transform.localPosition = pos;
                placed.transform.localRotation = Quaternion.Euler(obj.rotation);
            }
        }

        CurrentContext = null; // reset for next room
        room.context = new RoomRuntimeContext
        {
            RoomInstanceId = Guid.NewGuid().ToString(),
            State = RoomState.Loading,
            TotalWaves = template.waves.Count
        };
        room.Initialize(template, room.context);

        // Add per-room flow controller + wave manager
        var flow = go.GetComponent<RoomFlowController>();
        if (flow == null) flow = go.AddComponent<RoomFlowController>();
        flow.Initialize(template, room);
        flow.StartRoom();

        // Core spawn is deferred until all waves are cleared (see RoomFlowController)

        OnRoomLoaded?.Invoke(room);
        Debug.Log($"[RoomLoader] Loaded room: {template.roomName}");
        return room;
    }

    public void UnloadCurrentRoom()
    {
        if (CurrentRoom == null) return;
        OnRoomUnloaded?.Invoke(CurrentRoom);
        Destroy(CurrentRoom.gameObject);
        CurrentRoom = null;
        CurrentContext = null;
    }
}
