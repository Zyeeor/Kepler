using UnityEngine;

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

    [Header("Input")]
    public LayerMask groundLayer = -1;

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
        if (Instance == this) Instance = null;
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

    /// <summary>
    /// 每帧采集输入 → 写 ControlCommand。
    /// 移动用 GetAxisRaw（WASD 世界空间方向），按钮用 GetMouseButtonDown/GetKeyDown。
    /// 瞄准点每帧写入（HasAim/AimPoint），供 MonsterActor/SoulActor 静止朝鼠标。
    /// </summary>
    public void Tick(in ActorContext ctx, ref ControlCommand cmd)
    {
        cmd = ControlCommand.Empty;

        // 移动（WASD，映射到世界空间）
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dir = new Vector3(h, 0f, v);
        if (dir.magnitude > 1f) dir.Normalize();
        if (dir.magnitude > 0.1f)
        {
            cmd.HasMove = true;
            cmd.MoveDirection = dir;
        }

        // 瞄准（鼠标在地面平面的投影）
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

    /// <summary>瞄准点：鼠标在地面平面的投影。</summary>
    public bool TryGetAimPoint(out Vector3 aimPoint)
    {
        aimPoint = Vector3.zero;
        if (mainCamera == null) { mainCamera = Camera.main; if (mainCamera == null) return false; }
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100f, groundLayer))
        {
            aimPoint = hit.point;
            return true;
        }
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        float dist;
        if (plane.Raycast(ray, out dist))
        {
            aimPoint = ray.GetPoint(dist);
            return true;
        }
        return false;
    }
}
