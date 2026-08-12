using UnityEngine;

/// <summary>
/// Core interactable object in a room. When the player walks within interactRadius,
/// a 3-option choice UI pops up.
/// </summary>
public class RoomCore : MonoBehaviour
{
    [Tooltip("Distance at which the choice UI triggers.")]
    public float interactRadius = 3f;

    [Header("Optional Visual")]
    public GameObject highlightEffect; // optional glow/highlight

    [Header("Debug")]
    public bool enableDebugLogs = true;

    private bool uiShown;
    private Transform playerTransform;
    private SoulActor soulActor;
    private MonsterActor possessedMonster;
    private string interactionTargetSource;
    private float nextMissingTargetLogTime;
    private bool loggedInRange;

    void Start()
    {
        soulActor = FindObjectOfType<SoulActor>();
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        Log($"Started at {transform.position:F2}, interactRadius={interactRadius:F2}, soul={(soulActor != null ? soulActor.name : "NULL")}, player={(playerTransform != null ? playerTransform.name : "NULL")}");
    }

    void Update()
    {
        if (uiShown) return;

        if (!TryResolveInteractionTarget(out Transform target))
        {
            if (Time.unscaledTime >= nextMissingTargetLogTime)
            {
                nextMissingTargetLogTime = Time.unscaledTime + 2f;
                Log($"Waiting for interaction target. soul={(soulActor != null ? soulActor.name : "NULL")}, player={(playerTransform != null ? playerTransform.name : "NULL")}");
            }
            return;
        }

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist <= interactRadius)
        {
            if (!loggedInRange)
            {
                loggedInRange = true;
                Log($"Target '{target.name}' ({interactionTargetSource}) entered range: distance={dist:F2}, radius={interactRadius:F2}, timeScale={Time.timeScale:F2}");
            }
            ShowChoiceUI();
        }
        else if (loggedInRange)
        {
            loggedInRange = false;
            Log($"Target '{target.name}' ({interactionTargetSource}) left range: distance={dist:F2}, radius={interactRadius:F2}");
        }
    }

    void ShowChoiceUI()
    {
        var ui = CoreChoiceUI.Instance;
        if (ui == null)
        {
            LogWarning("Target is in range but CoreChoiceUI.Instance is null; keeping core retryable.");
            return;
        }

        uiShown = true;
        Log($"Showing choice UI via '{ui.name}', target='{playerTransform.name}' ({interactionTargetSource}), targetDistance={Vector3.Distance(transform.position, playerTransform.position):F2}");
        // 触发源只注入关闭回调，UI 不认识本类型；房间流程（解锁下一房间）由回调负责
        ui.Show(onClosed: OnChoicesConfirmed);
    }

    private bool TryResolveInteractionTarget(out Transform target)
    {
        PossessionManager possession = PossessionManager.Instance;
        possessedMonster = possession != null ? possession.CurrentBody : null;
        if (possessedMonster != null && possessedMonster.isActiveAndEnabled && possessedMonster.IsPlayerControlled)
        {
            playerTransform = possessedMonster.transform;
            interactionTargetSource = "possessed monster";
            target = playerTransform;
            return true;
        }

        if (soulActor == null)
            soulActor = FindObjectOfType<SoulActor>();

        if (soulActor != null && soulActor.isActiveAndEnabled &&
            !soulActor.IsSuppressed && !soulActor.IsInPossessionFlight)
        {
            playerTransform = soulActor.transform;
            interactionTargetSource = "soul";
            target = playerTransform;
            return true;
        }

        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        interactionTargetSource = playerTransform != null ? "Player tag fallback" : "none";
        target = playerTransform;
        return target != null;
    }

    private void Log(string message)
    {
        if (enableDebugLogs) Debug.Log("[RoomCore] " + message);
    }

    private void LogWarning(string message)
    {
        if (enableDebugLogs) Debug.LogWarning("[RoomCore] " + message);
    }

    /// <summary>选卡弹窗关闭时由 CoreChoiceUI 回调：接续房间流程。</summary>
    public void OnChoicesConfirmed()
    {
        uiShown = false;
        // 解锁下一房间（弹窗关闭回调移回触发方）
        RoomManager.Instance?.OnCoreConfirmed();
        // Core stays but interaction is done for this room
        enabled = false;
        // 本房间交互完成，核心可销毁（销毁职责移回触发方）
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
