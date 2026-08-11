using UnityEngine;

/// <summary>
/// 只在地面平面（XZ）上跟随 CameraTarget，不跟随玩家高度 / 旋转 / 朝向。
/// 相机 rig 结构：Camera 本身不是 Player 的子物体，跟随的是独立的 CameraTarget。
/// </summary>
public class BattleCameraRig : MonoBehaviour
{
    public enum ProjectionMode { Perspective, Orthographic }

    [Tooltip("相机跟随的目标（通常是 CameraTarget 对象）。")]
    public Transform target;
    [Tooltip("相机偏移（基于目标的本地坐标，X=右, Y=上, Z=前/后）。")]
    public Vector3 offset = new Vector3(0, 8, -6);
    [Tooltip("跟随平滑度。")]
    public float smoothSpeed = 6f;
    [Tooltip("look-at 平滑度。")]
    public float lookSmoothSpeed = 8f;
    [Tooltip("投影模式。")]
    public ProjectionMode projection = ProjectionMode.Perspective;
    [Tooltip("正交尺寸。")]
    public float orthographicSize = 8f;
    [Tooltip("透视 FOV。")]
    public float fieldOfView = 50f;

    void Start()
    {
        if (target == null)
        {
            var camTarget = FindObjectOfType<CameraTarget>();
            if (camTarget != null) { target = camTarget.transform; Debug.Log("[BattleCameraRig] Auto-found CameraTarget."); }
            else Debug.LogWarning("[BattleCameraRig] No target assigned and no CameraTarget found in scene.");
        }
        ApplyProjection();
    }

    void ApplyProjection()
    {
        var cam = GetComponent<Camera>();
        if (cam == null) return;
        if (projection == ProjectionMode.Orthographic)
        {
            cam.orthographic = true;
            cam.orthographicSize = orthographicSize;
        }
        else
        {
            cam.orthographic = false;
            cam.fieldOfView = fieldOfView;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPos = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPos, smoothSpeed * Time.deltaTime);

        Quaternion desiredRot = Quaternion.LookRotation(target.position - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, lookSmoothSpeed * Time.deltaTime);
    }
}
