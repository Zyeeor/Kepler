using UnityEngine;
using System;

public class RoomFlowController : MonoBehaviour
{
    public static RoomFlowController Instance { get; private set; }

    public RoomState CurrentState { get; private set; } = RoomState.Loading;

    public event Action<RoomState, RoomState> OnRoomStateChanged;
    public event Action OnRoomCleared;
    public event Action OnExitPhase;

    private RoomTemplate currentTemplate;
    private RoomInstance currentRoom;

    void Awake()
    {
        Instance = this;
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

        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.Initialize(currentTemplate, currentRoom);
            WaveManager.Instance.OnAllWavesComplete += OnAllWavesCompleteHandler;
            WaveManager.Instance.StartWaves();
        }
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
        OnRoomCleared?.Invoke();
        StartExitPhase();
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
        if (WaveManager.Instance != null)
            WaveManager.Instance.OnAllWavesComplete -= OnAllWavesCompleteHandler;
    }
}
