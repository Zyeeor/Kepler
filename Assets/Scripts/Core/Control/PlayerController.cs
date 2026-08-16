using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 玩家控制器：全局唯一实例，只采集输入 → 产出 ControlCommand。
/// 不含任何执行逻辑（移动/朝向/技能触发全在 Actor 执行层消费）。
/// 挂载于玩家根物体（与 PlayerHealth/PlayerCombat/SoulActor 同物体），Awake 注册 Instance。
/// 附身 = 本 Controller 经 SetController 挂到 MonsterActor（PossessionManager 编排）。
/// </summary>
public class PlayerController : MonoBehaviour, IController
{
    /// <summary>全局唯一实例（玩家根物体）。</summary>
    public static PlayerController Instance { get; private set; }
    public static bool IsGameplayInputBlocked { get; private set; }
    public static Vector3 CurrentMoveDirection { get; private set; }

    [Header("Input")]
    public LayerMask groundLayer = -1;
    public bool enableGlobalClickLogs = true;
    [Range(1, 16)] public int globalClickLogRaycastLimit = 8;

    private Camera mainCamera;
    private Transform self;

    void Awake()
    {
        Instance = this;
        mainCamera = Camera.main;
        self = transform;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            IsGameplayInputBlocked = false;
            CurrentMoveDirection = Vector3.zero;
        }
    }

    public void OnAttached(Actor owner)
    {
        // 无宿主状态；仅记录当前控制目标（附身时=MonsterActor，平时=SoulActor）
        _attached = owner;
    }

    public void OnDetached()
    {
        _attached = null;
    }

    private Actor _attached;

    private void Update()
    {
        if (!enableGlobalClickLogs) return;

        if (Input.GetMouseButtonDown(0)) LogGlobalMouseClick(0);
        if (Input.GetMouseButtonDown(1)) LogGlobalMouseClick(1);
        if (Input.GetMouseButtonDown(2)) LogGlobalMouseClick(2);
    }

    private void LogGlobalMouseClick(int button)
    {
        EventSystem eventSystem = EventSystem.current;
        string selected = eventSystem != null && eventSystem.currentSelectedGameObject != null
            ? GetHierarchyPath(eventSystem.currentSelectedGameObject.transform)
            : "NULL";

        Debug.Log($"[GlobalClick] MouseDown button={button}, position={Input.mousePosition}, cursorVisible={Cursor.visible}, cursorLock={Cursor.lockState}, timeScale={Time.timeScale:F2}, gameplayInputBlocked={IsGameplayInputBlocked}, eventSystem={(eventSystem != null ? eventSystem.name : "NULL")}, selected='{selected}'");

        if (eventSystem == null)
        {
            Debug.LogWarning("[GlobalClick] No EventSystem found; UI cannot receive pointer events.");
            return;
        }

        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            button = button == 0 ? PointerEventData.InputButton.Left :
                button == 1 ? PointerEventData.InputButton.Right : PointerEventData.InputButton.Middle,
            position = Input.mousePosition
        };
        List<RaycastResult> results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerData, results);

        if (results.Count == 0)
        {
            Debug.Log("[GlobalClick] RaycastAll result: 0 objects.");
            return;
        }

        int limit = Mathf.Min(globalClickLogRaycastLimit, results.Count);
        for (int i = 0; i < limit; i++)
        {
            GameObject hit = results[i].gameObject;
            Button buttonComponent = hit != null ? hit.GetComponentInParent<Button>() : null;
            Selectable selectable = hit != null ? hit.GetComponentInParent<Selectable>() : null;
            Debug.Log($"[GlobalClick] Hit[{i}] object='{(hit != null ? GetHierarchyPath(hit.transform) : "NULL")}', module='{(results[i].module != null ? results[i].module.GetType().Name : "NULL")}', distance={results[i].distance:F2}, button='{(buttonComponent != null ? buttonComponent.name : "NULL")}', interactable={(buttonComponent != null && buttonComponent.interactable)}, selectable='{(selectable != null ? selectable.name : "NULL")}'");
        }
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null) return "NULL";

        string path = target.name;
        Transform parent = target.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    public static void SetGameplayInputBlocked(bool blocked, string source)
    {
        IsGameplayInputBlocked = blocked;
        Debug.Log($"[PlayerInput] Gameplay input {(blocked ? "blocked" : "enabled")} by {source}.");
    }

    /// <summary>
    /// 每帧采集输入 → 写 ControlCommand。
    /// 移动用 GetAxisRaw（WASD 世界空间方向），按钮用 GetMouseButtonDown/GetKeyDown。
    /// 瞄准点每帧写入（HasAim/AimPoint），供 MonsterActor/SoulActor 静止朝鼠标。
    /// </summary>
    public void Tick(in ActorContext ctx, ref ControlCommand cmd)
    {
        cmd = ControlCommand.Empty;
        CurrentMoveDirection = Vector3.zero;
        if (IsGameplayInputBlocked) return;

        // 暂停/选卡期间（timeScale=0）屏蔽玩家输入，产出空指令
        if (Time.timeScale == 0f) return;

        // 移动（WASD，映射到世界空间）
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dir = new Vector3(h, 0f, v);
        if (dir.magnitude > 1f) dir.Normalize();
        if (dir.magnitude > 0.1f)
        {
            cmd.HasMove = true;
            cmd.MoveDirection = dir;
            CurrentMoveDirection = dir;
        }

        // 瞄准（鼠标在当前控制角色 Y 高度平面的投影）
        Vector3 aim;
        if (TryGetAimPoint(out aim))
        {
            cmd.HasAim = true;
            cmd.AimPoint = aim;
        }

        // 按钮位
        if (Input.GetMouseButtonDown(0)) cmd.Pressed |= CommandButtons.Basic;
        if (Input.GetMouseButtonDown(1)) cmd.Pressed |= CommandButtons.Skill1; // right-click possession / body switch
        if (Input.GetKeyDown(KeyCode.Q)) cmd.Pressed |= CommandButtons.Skill2;  // possessed-monster skill
        if (Input.GetKeyDown(KeyCode.Space)) cmd.Pressed |= CommandButtons.Mobility;
        if (Input.GetKeyDown(KeyCode.E)) cmd.Pressed |= CommandButtons.Skill3;  // possessed-monster bullet time
        if (Input.GetKeyDown(KeyCode.F)) cmd.Pressed |= CommandButtons.Release; // F=脱离
    }

    /// <summary>从鼠标位置构造射线（附身发起 RequestPossess 用）。</summary>
    public Ray GetMouseRay()
    {
        Camera activeCamera = Camera.main;
        if (activeCamera != null) mainCamera = activeCamera;
        if (mainCamera == null)
        {
            Debug.LogWarning("[PossessionInput] Cannot create mouse ray: no active MainCamera.");
            return default(Ray);
        }
        return mainCamera.ScreenPointToRay(Input.mousePosition);
    }

    /// <summary>瞄准点：鼠标射线与当前玩家控制角色 Y 高度平面的交点，不受地形高度或碰撞体影响。</summary>
    public bool TryGetAimPoint(out Vector3 aimPoint)
    {
        aimPoint = Vector3.zero;
        if (mainCamera == null) { mainCamera = Camera.main; if (mainCamera == null) return false; }

        float aimPlaneY = _attached != null ? _attached.transform.position.y : self.position.y;
        Plane plane = new Plane(Vector3.up, new Vector3(0f, aimPlaneY, 0f));
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!plane.Raycast(ray, out float distance)) return false;

        aimPoint = ray.GetPoint(distance);
        return true;
    }
}
