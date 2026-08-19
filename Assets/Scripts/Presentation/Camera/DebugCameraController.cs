using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 调试飞行相机（开发工具，挂在主相机上，与 CameraDirector 同对象）：
///   - 按 F4 进入自由视角：画面暂停（timeScale=0），WASD 水平平移 / QE 升降 /
///     鼠标右键拖拽旋转 / 滚轮沿视线推进 / Shift 加速；
///   - 再按 F4 退出：还原进入时的镜头位置与朝向，恢复 timeScale 与 CinemachineBrain 跟随。
/// 进入时禁用 CinemachineBrain + 暂停时间（世界冻结——明显视觉反馈，也便于逐帧观察）。
/// 暂停期间仅调试相机可动（unscaledDeltaTime），游戏逻辑全部冻结，退出时恢复。
/// 自举：由 CameraDirector.Awake 调用 EnsureOn 挂载，无需场景手动接线。
/// 按键说明：原 F3 疑似被系统/编辑器占用（用户实测无响应），改用空闲的 F4（调试键
/// 分布：F1 怪测试 / F2 HUD / F4 本相机 / F5 跳波）。
/// </summary>
public class DebugCameraController : MonoBehaviour
{
    public static DebugCameraController Instance { get; private set; }

    [Header("控制")]
    [Tooltip("移动速度（米/秒）。")]
    [Min(0.1f)] public float moveSpeed = 18f;
    [Tooltip("Shift 加速倍数。")]
    [Min(1f)] public float boostMultiplier = 3f;
    [Tooltip("鼠标旋转灵敏度（度/像素）。")]
    public float lookSensitivity = 1f;
    [Tooltip("滚轮推进速度（米/格）。")]
    public float scrollSpeed = 6f;
    [Tooltip("俯仰角限制（度），防镜头翻转。")]
    [Range(1f, 89f)] public float pitchClamp = 89f;

    CinemachineBrain _brain;
    Camera _cam;
    bool _active;
    Vector3 _savedPos;
    Quaternion _savedRot;
    float _yaw;
    float _pitch;
    float _timeScaleBefore;
    bool _savedOrthographic;
    float _savedOrthoSize;

    public bool IsActive => _active;

    /// <summary>自举挂载（幂等）：确保主相机上有本组件。返回挂载后的组件（失败返回 null）。</summary>
    public static DebugCameraController EnsureOn(Camera cam)
    {
        if (cam == null)
        {
            Debug.LogWarning("[DebugCamera] EnsureOn 失败：相机为空（CameraDirector 未挂在相机对象上？）");
            return null;
        }
        var c = cam.GetComponent<DebugCameraController>();
        if (c == null) c = cam.gameObject.AddComponent<DebugCameraController>();
        return c;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        _brain = GetComponent<CinemachineBrain>();
        _cam = GetComponent<Camera>();
    }

    void Update()
    {
        // F4 切换进入/退出
        if (Input.GetKeyDown(KeyCode.F4))
        {
            if (_active) ExitDebug();
            else EnterDebug();
        }
    }

    void LateUpdate()
    {
        if (!_active) return;

        // 每帧强制暂停（LateUpdate 在所有 Update/协程之后执行，压制 GameManager/PossessionManager
        // 等每帧写回 timeScale 的逻辑——否则 EnterDebug 的一次性设置下一帧就被覆盖，世界不会真暂停）。
        Time.timeScale = 0f;

        // 顿帧/子弹时间时调试相机照常可用（unscaled）
        float dt = Time.unscaledDeltaTime;
        // 鼠标在 UI 上时不做镜头操作（避免拖拽误触 UI 交互/音效）
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // 朝向统一从累计 yaw/pitch 派生（与旋转同源），并按 Unity Scene 视图 fly 模式：
        // W/S 沿视线方向飞行（含垂直分量——面朝下按 W 就向下飞），A/D 沿相机左右平移。
        // 不依赖 transform.forward/right（transform 可能被外部驱动干扰）；
        // 不做贴地投影（投影会让面朝下时方向归零、斜视时方向偏离视线）。
        Quaternion camRot = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 fwd = camRot * Vector3.forward;
        Vector3 right = camRot * Vector3.right;

        // WASD 沿相机轴向飞行 + QE 世界垂直升降（Q 下 E 上，Unity fly 惯例）
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? boostMultiplier : 1f);
        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += fwd;
        if (Input.GetKey(KeyCode.S)) move -= fwd;
        if (Input.GetKey(KeyCode.D)) move += right;
        if (Input.GetKey(KeyCode.A)) move -= right;
        if (Input.GetKey(KeyCode.E)) move += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;
        if (move.sqrMagnitude > 0f) transform.position += move.normalized * speed * dt;

        // 滚轮缩放（沿视线推近/拉远）
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
            transform.position += camRot * Vector3.forward * (scroll * scrollSpeed * (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? boostMultiplier : 1f));

        // 左键拖拽平移（Pan，屏幕平面方向）
        if (Input.GetMouseButton(0) && !overUI)
        {
            Vector3 pan = (-Input.GetAxis("Mouse X")) * (camRot * Vector3.right) + (-Input.GetAxis("Mouse Y")) * (camRot * Vector3.up);
            transform.position += pan * speed * dt;
        }
        // 右键拖拽旋转（yaw/pitch 独立累计防漂移）
        if (Input.GetMouseButton(1) && !overUI)
        {
            _yaw += Input.GetAxis("Mouse X") * lookSensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
            _pitch = Mathf.Clamp(_pitch, -pitchClamp, pitchClamp);
        }
        // 中键拖拽平移（与左键同向）
        if (Input.GetMouseButton(2) && !overUI)
        {
            Vector3 pan = (-Input.GetAxis("Mouse X")) * (camRot * Vector3.right) + (-Input.GetAxis("Mouse Y")) * (camRot * Vector3.up);
            transform.position += pan * speed * dt;
        }

        // 每帧末尾强制应用姿态（防外部驱动覆盖回弹；旋转后的朝向立即生效）
        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    void EnterDebug()
    {
        // 幂等保护：已激活时直接返回（防重复进入覆盖保存值，如外部重复调用）
        if (_active) return;
        _savedPos = transform.position;
        _savedRot = transform.rotation;
        var e = transform.eulerAngles;
        _yaw = e.y;
        _pitch = e.x;
        if (_brain != null) _brain.enabled = false;
        // 主相机为正交投影（项目设定），正交下移动只有平移、物体永不靠近——
        // 调试模式临时切透视（fly 手感正确），退出时完整恢复正交状态。
        if (_cam != null)
        {
            _savedOrthographic = _cam.orthographic;
            _savedOrthoSize = _cam.orthographicSize;
            if (_cam.orthographic) _cam.orthographic = false;
        }
        // 暂停世界（画面立即冻结 = 明显进入反馈；退出时恢复原 timeScale，兼容子弹时间等缩放）
        _timeScaleBefore = Time.timeScale;
        Time.timeScale = 0f;
        _active = true;
        Debug.Log("[DebugCamera] F4 进入调试相机（画面已暂停，临时透视投影）：WASD 平移 / QE 升降 / 左键平移 / 右键旋转 / 中键平移 / 滚轮缩放 / Shift 加速。再按 F4 退出并还原。");
    }

    void ExitDebug()
    {
        transform.position = _savedPos;
        transform.rotation = _savedRot;
        if (_brain != null) _brain.enabled = true;
        // 恢复正交投影设定
        if (_cam != null)
        {
            _cam.orthographic = _savedOrthographic;
            _cam.orthographicSize = _savedOrthoSize;
        }
        Time.timeScale = _timeScaleBefore;
        _active = false;
        Debug.Log($"[DebugCamera] F4 退出调试相机，镜头/投影（orthographic={_savedOrthographic}）/时间（timeScale={_timeScaleBefore}）已还原。");
    }

    void OnGUI()
    {
        if (!_active) return;
        // 顶部状态横幅（仅调试模式显示；大字号保证进入时明显可见）
        var style = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        GUI.color = Color.yellow;
        GUI.Label(new Rect(12f, 10f, 900f, 26f), "[F4 调试相机 · 画面已暂停] WASD 平移 / QE 升降 / 左键平移 / 右键旋转 / 中键平移 / 滚轮缩放 / Shift 加速 — 再按 F4 退出并恢复", style);
    }
}
