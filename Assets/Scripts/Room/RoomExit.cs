using UnityEngine;

/// <summary>
/// Attach to an exit GameObject. When the player walks through, loads the linked room.
/// </summary>
public class RoomExit : MonoBehaviour
{
    [Tooltip("Index of the RoomTemplate in RoomManager.roomTemplates to load.")]
    public int leadsToRoomIndex = -1;

    private bool triggered;

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (other.CompareTag("Player"))
        {
            triggered = true;
            if (RoomManager.Instance != null && leadsToRoomIndex >= 0)
            {
                Debug.Log($"[RoomExit] Player entered exit, loading room index {leadsToRoomIndex}");
                RoomManager.Instance.LoadRoom(leadsToRoomIndex);
            }
        }
    }
}
