using UnityEngine;

/// <summary>
/// 相机跟随目标。挂在一个独立的空对象上（不是 Player 的子物体，也不是 Camera 的子物体）。
///
/// BattleCameraRig 跟随的是这个对象，而不是直接跟随 Player。
/// 这样可以在 Player 和 Camera 之间加一层可控的缓冲：
///   - 第一版：CameraTarget 直接锁定 Player 的 XZ（LookAhead 关闭）。
///   - 后续：可开启 LookAhead，让目标点朝玩家移动方向略微前移。
///
/// CameraTargetPosition = PlayerPosition + PlayerVelocity.normalized * LookAheadDistance
/// </summary>
public class CameraTarget : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("要跟随的玩家。留空则自动按 Tag=Player 查找。")]
    public Transform player;

    [Header("Look Ahead (第一版关闭)")]
    [Tooltip("是否启用前瞻：目标点朝玩家移动方向前移。第一版保持关闭，只做稳定跟随。")]
    public bool enableLookAhead = false;
    [Tooltip("前瞻距离（世界单位）。仅 enableLookAhead 时生效。")]
    public float lookAheadDistance = 3f;
    [Tooltip("前瞻平滑时间（秒）。")]
    public float lookAheadSmoothTime = 0.2f;

    private Rigidbody _playerRb;
    private Vector3 _lastPlayerPos;
    private Vector3 _lookAheadCurrent;
    private Vector3 _lookAheadVelocity;

    void Start()
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }
        if (player != null)
        {
            _playerRb = player.GetComponent<Rigidbody>();
            _lastPlayerPos = player.position;
            transform.position = FlattenToGround(player.position);
        }
    }

    void LateUpdate()
    {
        if (player == null) return;

        Vector3 basePos = FlattenToGround(player.position);

        if (enableLookAhead)
        {
            // 用 Rigidbody 速度优先，否则用位移估算
            Vector3 vel;
            if (_playerRb != null)
                vel = _playerRb.velocity;
            else
                vel = (player.position - _lastPlayerPos) / Mathf.Max(Time.deltaTime, 0.0001f);

            vel.y = 0f;
            Vector3 desiredLookAhead = vel.sqrMagnitude > 0.01f
                ? vel.normalized * lookAheadDistance
                : Vector3.zero;

            _lookAheadCurrent = Vector3.SmoothDamp(
                _lookAheadCurrent, desiredLookAhead, ref _lookAheadVelocity, lookAheadSmoothTime);

            basePos += _lookAheadCurrent;
        }

        transform.position = basePos;
        _lastPlayerPos = player.position;
    }

    private Vector3 FlattenToGround(Vector3 p)
    {
        return new Vector3(p.x, 0f, p.z);
    }
}
