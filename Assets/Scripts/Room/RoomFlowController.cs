using UnityEngine;
using System;

/// <summary>
/// Per-room flow controller. One instance per room (not a singleton).
/// </summary>
public class RoomFlowController : MonoBehaviour
{
    public RoomState CurrentState { get; private set; } = RoomState.Loading;

    public event Action<RoomState, RoomState> OnRoomStateChanged;
    public event Action OnRoomCleared;
    public event Action OnExitPhase;

    private RoomTemplate currentTemplate;
    private RoomInstance currentRoom;
    private WaveManager waveManager;

    void Awake()
    {
        waveManager = GetComponent<WaveManager>();
        if (waveManager == null) waveManager = gameObject.AddComponent<WaveManager>();
    }

    public void Initialize(RoomTemplate template, RoomInstance room)
    {
        currentTemplate = template;
        currentRoom = room;
        ChangeState(RoomState.Loading);
    }

    public void StartRoom()
    {
        if (currentTemplate == null) return;
        ChangeState(RoomState.Combat);

        waveManager.Initialize(currentTemplate, currentRoom);
        waveManager.OnAllWavesComplete += OnAllWavesCompleteHandler;
        waveManager.StartWaves();
    }

    public void CompleteRoom()
    {
        ChangeState(RoomState.Completed);
    }

    public void StartExitPhase()
    {
        ChangeState(RoomState.ExitPhase);
        if (currentRoom != null)
            currentRoom.SetExitsEnabled(true);
        OnExitPhase?.Invoke();
    }

    void OnAllWavesCompleteHandler()
    {
        ChangeState(RoomState.Cleared);
        Debug.Log($"[RoomFlow] OnAllWavesComplete for room '{currentTemplate?.roomName}', spawnAfterWavesCleared={currentTemplate?.core.spawnAfterWavesCleared}, corePrefab={currentTemplate?.core.prefab != null}");

        // Spawn core if enabled
        if (currentTemplate != null && currentTemplate.core.spawnAfterWavesCleared && currentTemplate.core.prefab != null)
        {
            SpawnCore();
        }

        OnRoomCleared?.Invoke();
        StartExitPhase();
    }

    void SpawnCore()
    {
        Vector3 coreWorldPos = currentTemplate.roomPosition + currentTemplate.core.GetPosition(currentTemplate.transform);
        Quaternion coreWorldRot = Quaternion.Euler(currentTemplate.roomRotation) * currentTemplate.core.GetRotation();
        var coreGo = Instantiate(currentTemplate.core.prefab, coreWorldPos, coreWorldRot);
        var coreComp = coreGo.GetComponent<RoomCore>();
        if (coreComp == null) coreComp = coreGo.AddComponent<RoomCore>();
        coreComp.interactRadius = currentTemplate.core.interactRadius;
        Debug.Log("[RoomFlow] Core spawned after all waves cleared");
    }

    void ChangeState(RoomState newState)
    {
        var oldState = CurrentState;
        CurrentState = newState;
        if (currentRoom != null && currentRoom.context != null)
            currentRoom.context.State = newState;
        OnRoomStateChanged?.Invoke(oldState, newState);
        Debug.Log($"[RoomFlow] State: {oldState} → {newState}");
    }

    void OnDestroy()
    {
        if (waveManager != null)
            waveManager.OnAllWavesComplete -= OnAllWavesCompleteHandler;
    }
}
