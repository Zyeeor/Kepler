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
        if (currentRoom != null)
            currentRoom.SetExitsEnabled(false);

        waveManager.Initialize(currentTemplate, currentRoom);
        waveManager.OnAllWavesComplete += OnAllWavesCompleteHandler;
        waveManager.OnWaveCompleted += OnWaveCompletedHandler;   // 每波打完触发选卡（单选）
        waveManager.StartWaves();
    }

    /// <summary>
    /// 每波打完自动触发选卡（单选）。
    /// 延迟由 WaveManager 统一处理（choiceUIDelay，默认 2s，弹卡前缓冲看清战果）。
    /// 选卡暂停（timeScale=0）会阻塞波次协程（WaitForSeconds 受 timeScale 影响），
    /// 玩家选完恢复后下一波自动开始。
    /// </summary>
    void OnWaveCompletedHandler(int waveIndex)
    {
        Debug.Log($"[RoomFlow] Wave {waveIndex} completed → trigger choice UI (single pick)");
        if (CoreChoiceUI.Instance != null)
            CoreChoiceUI.Instance.Show(onClosed: null, doublePick: false);
        else
            Debug.LogWarning("[RoomFlow] CoreChoiceUI.Instance is null — cannot show choice UI");
    }

    public void CompleteRoom()
    {
        ChangeState(RoomState.Completed);
    }

    public void StartExitPhase()
    {
        if (waveManager == null || !waveManager.AllWavesComplete)
        {
            Debug.LogWarning($"[RoomFlow] Exit phase rejected for '{currentTemplate?.roomName}': allWavesComplete={waveManager != null && waveManager.AllWavesComplete}");
            return;
        }

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
        {
            waveManager.OnAllWavesComplete -= OnAllWavesCompleteHandler;
            waveManager.OnWaveCompleted -= OnWaveCompletedHandler;
        }
    }
}
