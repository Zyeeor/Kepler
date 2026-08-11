using UnityEngine;

/// <summary>
/// BattleCameraRig 跟随的是这个对象，而不是直接跟随 Player。
/// 第一版：CameraTarget 直接锁定 Player 的 XZ（LookAhead 关闭）。
/// CameraTargetPosition = PlayerPosition + PlayerVelocity.normalized * LookAheadDistance
/// </summary>
public class CameraTarget : MonoBehaviour
{
    public Transform player;
    public float lookAheadDistance = 2f;
    public float smoothSpeed = 4f;

    private Vector3 lastPlayerPos;

    void Start()
    {
        if (player != null) lastPlayerPos = player.position;
    }

    void LateUpdate()
    {
        if (player == null) return;
        Vector3 playerVel = player.position - lastPlayerPos;
        Vector3 targetPos = player.position;
        if (playerVel.sqrMagnitude > 0.01f)
            targetPos += playerVel.normalized * lookAheadDistance;
        transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
        lastPlayerPos = player.position;
    }
}
