using UnityEngine;

/// <summary>
/// 固定斜俯视战斗相机（类《Hades》）。
///
/// 核心设计：
/// - 相机始终 LOOK AT 聚焦点（不是固定旋转），确保聚焦点 = 画面中心。
/// - 只在地面平面（XZ）上跟随 CameraTarget，不跟随玩家高度 / 旋转 / 朝向。
/// - cameraPitch + cameraDistance 决定镜头位置；cameraHeight 自动计算（= distance × tan(pitch)），三者几何自洽。
/// - 玩家屏幕位置由 PlayerScreenOffset 控制：通过 SHIFT 聚焦点（而非移动相机）实现偏移。
/// - SmoothDamp 平滑跟随（帧率无关），带死区（Dead Zone）避免小漂移。
/// - Confiner 按房间边界 Clamp 聚焦点，且考虑视野覆盖宽度，确保不露出场景外。
/// - 所有关键参数暴露到 Inspector。
///
/// 相机 rig 结构：Camera 本身不是 Player 的子物体，跟随的是独立的 CameraTarget。
/// </summary>
[RequireComponent(typeof(Camera))]
public class BattleCameraRig : MonoBehaviour
{
    public enum ProjectionMode { Perspective, Orthographic }

    // ── 跟随目标 ─────────────────────────────────────────
    [Header("Follow Target")]
    [Tooltip("相机跟随的目标（通常是 CameraTarget 对象）。")]
    public Transform target;

    // ── 投影 ─────────────────────────────────────────────
    [Header("Projection")]
    [Tooltip("投影模式。优先 Perspective；若透视变形过强再切 Orthographic。")]
    public ProjectionMode projection = ProjectionMode.Perspective;
    [Tooltip("透视视野角度（度）。25–35 可减弱近大远小。仅 Perspective 生效。")]
    [Range(10f, 60f)]
    public float fieldOfView = 30f;
    [Tooltip("正交视野半高。仅 Orthographic 生效。")]
    public float orthographicSize = 11f;

    // ── 相机位姿 ─────────────────────────────────────────
    [Header("Camera Pose")]
    [Tooltip("相机俯角（绕 X 轴）。Hades 风格 45°–60°。")]
    [Range(30f, 80f)]
    public float cameraPitch = 55f;
    [Tooltip("相机绕 Y 轴的朝向（0 或 45，根据房间统一）。")]
    public float cameraYaw = 0f;
    [Tooltip("相机相对聚焦点的水平后退距离（固定）。")]
    public float cameraDistance = 12f;
    [Tooltip("相机高度（自动计算 = distance × tan(pitch)）。只读——改 Pitch 或 Distance 来调整。")]
    public float cameraHeight = 17f;

    // ── 玩家屏幕位置 ─────────────────────────────────────
    [Header("Player Screen Position")]
    [Tooltip("玩家在屏幕中的归一化位置。(0.5,0.5)=正中。y<0.5=玩家偏下（推荐 0.35）。")]
    public Vector2 playerScreenOffset = new Vector2(0.5f, 0.35f);

    // ── 跟随平滑 ─────────────────────────────────────────
    [Header("Follow Smoothing")]
    [Tooltip("SmoothDamp 平滑时间（秒）。0.1–0.25 较合适。")]
    [Range(0.02f, 0.6f)]
    public float followSmoothTime = 0.15f;
    [Tooltip("死区半径（世界单位）。目标在此半径内移动，聚焦点不动，避免抖动。")]
    public float deadZoneRadius = 0.5f;

    // ── 房间边界 ─────────────────────────────────────────
    [Header("Room Bounds (Confiner)")]
    [Tooltip("是否启用房间边界限制。")]
    public bool useConfiner = true;
    [Tooltip("房间中心（世界坐标 XZ）。")]
    public Vector2 roomCenter = Vector2.zero;
    [Tooltip("房间尺寸（世界单位，X=宽 Z=深）。")]
    public Vector2 roomSize = new Vector2(64f, 48f);
    [Tooltip("额外内缩边距（世界单位）。")]
    public float confinerPadding = 0f;

    // ── 内部状态 ─────────────────────────────────────────
    private Camera _cam;
    private Vector3 _focusPoint;       // 当前聚焦点（经死区 / 平滑 / clamp 后）
    private Vector3 _focusVelocity;    // SmoothDamp 速度
    private bool _warnedNoTarget;      // 只警告一次

    // ══════════════════════════════════════════════════════
    void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    void OnEnable()
    {
        _cam = GetComponent<Camera>();
        ApplyProjection();
        UpdateCameraHeight();

        if (target != null)
        {
            _focusPoint = FlattenToGround(target.position);
            SnapCamera();
        }
        else
        {
            TryAutoFindTarget();
            if (target != null)
            {
                _focusPoint = FlattenToGround(target.position);
                SnapCamera();
            }
        }
    }

    void LateUpdate()
    {
        if (target == null)
        {
            TryAutoFindTarget();
            return;
        }

        ApplyProjection();
        UpdateCameraHeight();

        // 1. 目标地面位置（仅 XZ，忽略高度）
        Vector3 targetGroundPos = FlattenToGround(target.position);

        // 2. 死区：小移动不推聚焦点
        Vector3 desiredFocus = ApplyDeadZone(targetGroundPos);

        // 3. SmoothDamp（帧率无关，无漂移）
        _focusPoint = Vector3.SmoothDamp(
            _focusPoint, desiredFocus, ref _focusVelocity, followSmoothTime);

        // 4. 房间边界 Clamp
        if (useConfiner)
            _focusPoint = ClampFocusToRoom(_focusPoint);

        // 5. 屏幕偏移 + 设置相机位姿（LookAt 聚焦点）
        ApplyCameraPose();
    }

    // ══════════════════════════════════════════════════════
    // 工具方法
    // ══════════════════════════════════════════════════════

    private void TryAutoFindTarget()
    {
        var camTarget = FindObjectOfType<CameraTarget>();
        if (camTarget != null)
        {
            target = camTarget.transform;
            _focusPoint = FlattenToGround(target.position);
            _warnedNoTarget = false;
            Debug.Log("[BattleCameraRig] Auto-found CameraTarget.");
            return;
        }

        if (!_warnedNoTarget)
        {
            Debug.LogWarning("[BattleCameraRig] No target assigned and no CameraTarget found in scene. " +
                             "Camera will not follow anything.");
            _warnedNoTarget = true;
        }
    }

    private void UpdateCameraHeight()
    {
        // 高度由 Pitch 和 Distance 自动推导，保证几何自洽
        cameraHeight = cameraDistance * Mathf.Tan(cameraPitch * Mathf.Deg2Rad);
    }

    private void ApplyProjection()
    {
        if (_cam == null) _cam = GetComponent<Camera>();
        if (projection == ProjectionMode.Perspective)
        {
            _cam.orthographic = false;
            _cam.fieldOfView = fieldOfView;
        }
        else
        {
            _cam.orthographic = true;
            _cam.orthographicSize = orthographicSize;
        }
    }

    private Vector3 ApplyDeadZone(Vector3 targetGroundPos)
    {
        Vector3 flatFocus = new Vector3(_focusPoint.x, 0f, _focusPoint.z);
        float dist = Vector3.Distance(targetGroundPos, flatFocus);

        if (dist <= deadZoneRadius)
            return flatFocus; // 死区内：聚焦点不移动

        // 超出死区：目标位置减去死区半径（避免边界抖动）
        Vector3 dir = (targetGroundPos - flatFocus).normalized;
        return targetGroundPos - dir * deadZoneRadius;
    }

    /// <summary>
    /// 设置相机的位置和朝向。
    /// 位置 = effectiveLookTarget + backDir * distance + up * height
    /// 朝向 = LookAt(effectiveLookTarget) —— 始终锁定聚焦点
    /// </summary>
    private void ApplyCameraPose()
    {
        // 屏幕偏移：shift 聚焦点，使玩家出现在 playerScreenOffset 位置
        Vector3 effectiveLookTarget = ComputeScreenOffset(_focusPoint);

        // 相机位置
        Quaternion yawRot = Quaternion.Euler(0f, cameraYaw, 0f);
        Vector3 backDir = yawRot * Vector3.back;
        Vector3 camPos = effectiveLookTarget
                       + backDir * cameraDistance
                       + Vector3.up * cameraHeight;

        // 相机始终看向聚焦点（略高于地面，避免只看脚底）
        transform.position = camPos;
        transform.LookAt(effectiveLookTarget + Vector3.up * 0.5f);
    }

    /// <summary>
    /// 根据 playerScreenOffset 将聚焦点在世界中平移。
    ///
    /// 原理：如果想让玩家在屏幕下方（offset.y < 0.5），需要让相机看向更远处。
    /// 等价于把聚焦点沿相机前方（地面投影）向前推。
    ///
    /// 世界偏移量 = -screenDelta * viewHalfSizeWorld
    ///   - X: halfWidthView（直接，无 pitch 影响）
    ///   - Y: halfHeightView / sin(pitch)（地面深度被 pitch 拉伸）
    /// </summary>
    private Vector3 ComputeScreenOffset(Vector3 focus)
    {
        Vector2 delta = playerScreenOffset - new Vector2(0.5f, 0.5f);
        if (Mathf.Abs(delta.x) < 0.001f && Mathf.Abs(delta.y) < 0.001f)
            return focus;

        // 视野在世界中的半尺寸（聚焦距离处）
        float distToFocus = Mathf.Sqrt(cameraHeight * cameraHeight + cameraDistance * cameraDistance);
        float halfHeightView;
        if (projection == ProjectionMode.Orthographic)
            halfHeightView = _cam.orthographicSize;
        else
            halfHeightView = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) * distToFocus;

        float halfWidthView = halfHeightView * _cam.aspect;

        // 地面深度半尺寸：斜俯视时，屏幕纵向在地面上被拉长
        float pitchRad = Mathf.Max(cameraPitch, 1f) * Mathf.Deg2Rad;
        float groundHalfDepth = halfHeightView / Mathf.Sin(pitchRad);

        // 偏移方向（XZ 平面）
        Quaternion yawRot = Quaternion.Euler(0f, cameraYaw, 0f);
        Vector3 rightOnGround = yawRot * Vector3.right;
        Vector3 forwardOnGround = yawRot * Vector3.forward;

        // screenDelta > 0（右/上） → 聚焦点向左/向后移 → 玩家出现在右/上方
        Vector3 offset = rightOnGround   * (-delta.x * halfWidthView)
                       + forwardOnGround * (-delta.y * groundHalfDepth);

        return focus + offset;
    }

    private void SnapCamera()
    {
        UpdateCameraHeight();
        ApplyCameraPose();
    }

    // ══════════════════════════════════════════════════════
    // 房间边界限制
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 将聚焦点 Clamp 到房间内部，考虑相机视野覆盖宽度。
    /// 房间比视野小时，聚焦点固定在房间中心。
    /// </summary>
    private Vector3 ClampFocusToRoom(Vector3 focus)
    {
        float distToFocus = Mathf.Sqrt(cameraHeight * cameraHeight + cameraDistance * cameraDistance);
        float halfHeightView;
        if (projection == ProjectionMode.Orthographic)
            halfHeightView = _cam.orthographicSize;
        else
            halfHeightView = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) * distToFocus;

        float halfWidthView = halfHeightView * _cam.aspect;

        float pitchRad = Mathf.Max(cameraPitch, 1f) * Mathf.Deg2Rad;
        float groundHalfDepth = halfHeightView / Mathf.Sin(pitchRad);
        float groundHalfWidth = halfWidthView;

        float roomHalfW = roomSize.x * 0.5f - confinerPadding;
        float roomHalfD = roomSize.y * 0.5f - confinerPadding;

        // X
        float minX = roomCenter.x - roomHalfW + groundHalfWidth;
        float maxX = roomCenter.x + roomHalfW - groundHalfWidth;
        float cx = (minX > maxX) ? roomCenter.x : Mathf.Clamp(focus.x, minX, maxX);

        // Z
        float minZ = roomCenter.y - roomHalfD + groundHalfDepth;
        float maxZ = roomCenter.y + roomHalfD - groundHalfDepth;
        float cz = (minZ > maxZ) ? roomCenter.y : Mathf.Clamp(focus.z, minZ, maxZ);

        return new Vector3(cx, focus.y, cz);
    }

    private static Vector3 FlattenToGround(Vector3 p)
    {
        return new Vector3(p.x, 0f, p.z);
    }

    // ══════════════════════════════════════════════════════
    // Scene 视图 Gizmo
    // ══════════════════════════════════════════════════════
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        Vector3 c = new Vector3(roomCenter.x, 0.05f, roomCenter.y);
        Gizmos.DrawWireCube(c, new Vector3(roomSize.x, 0.1f, roomSize.y));

        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_focusPoint, 0.4f);
            Gizmos.DrawLine(transform.position, _focusPoint);
        }
    }
}
