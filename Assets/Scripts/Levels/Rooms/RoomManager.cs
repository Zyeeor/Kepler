using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// Room manager. Only spawns the FIRST enabled room on start.
/// Subsequent rooms are unlocked when the player interacts with the Core and confirms.
/// </summary>
public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    [Header("Room Templates")]
    public List<RoomEntry> rooms = new List<RoomEntry>();

    private int nextRoomIndex = -1;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Spawn only the first enabled room
        for (int i = 0; i < rooms.Count; i++)
        {
            var entry = rooms[i];
            if (entry == null || entry.template == null || !entry.enabled) continue;
            Debug.Log($"[RoomManager] Loading first room: {entry.template.roomName} (index {i})");
            LoadRoom(i);
            // Set next room index
            nextRoomIndex = FindNextEnabledRoom(i + 1);
            break;
        }
    }

    int FindNextEnabledRoom(int startFrom)
    {
        for (int i = startFrom; i < rooms.Count; i++)
        {
            if (rooms[i] != null && rooms[i].template != null && rooms[i].enabled)
                return i;
        }
        return -1;
    }

    public void LoadRoom(int index)
    {
        if (index < 0 || index >= rooms.Count) return;
        var entry = rooms[index];
        if (entry == null || entry.template == null) return;
        RoomLoader.Instance.LoadRoom(entry.template);
    }

    /// <summary>Called when the player confirms choices on a Core. Unlocks and spawns the next room.</summary>
    public void OnCoreConfirmed()
    {
        if (nextRoomIndex < 0)
        {
            Debug.Log("[RoomManager] No more rooms to load");
            return;
        }
        Debug.Log($"[RoomManager] Core confirmed, loading next room index {nextRoomIndex}: {rooms[nextRoomIndex].template.roomName}");
        LoadRoom(nextRoomIndex);
        nextRoomIndex = FindNextEnabledRoom(nextRoomIndex + 1);
    }

    [Serializable]
    public class RoomEntry
    {
        [Tooltip("Check to include this room in the sequence.")]
        public bool enabled = true;
        [Tooltip("Room template to spawn.")]
        public RoomTemplate template;
    }
}
