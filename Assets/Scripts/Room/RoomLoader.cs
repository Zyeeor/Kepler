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

        if (CurrentRoom != null)
            UnloadCurrentRoom();

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

        CurrentRoom = go.GetComponent<RoomInstance>();
        if (CurrentRoom == null)
            CurrentRoom = go.AddComponent<RoomInstance>();

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
                // Use prefab's own scale, don't override
            }
        }

        CurrentRoom.context = CurrentContext;
        CurrentRoom.Initialize(template, CurrentContext);

        // Spawn Core if defined (world position, not parented so it doesn't get offset)
        if (template.core.prefab != null)
        {
            var coreGo = Instantiate(template.core.prefab, template.core.GetPosition(template.transform), template.core.GetRotation());
            var coreComp = coreGo.GetComponent<RoomCore>();
            if (coreComp == null) coreComp = coreGo.AddComponent<RoomCore>();
            coreComp.interactRadius = template.core.interactRadius;
        }

        OnRoomLoaded?.Invoke(CurrentRoom);
        Debug.Log($"[RoomLoader] Loaded room: {template.roomName}");
        return CurrentRoom;
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
