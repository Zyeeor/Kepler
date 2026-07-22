using UnityEngine;

/// <summary>
/// Drives the minimap camera: a top-down orthographic camera that renders the
/// world into a RenderTexture displayed by the minimap UI (RawImage).
///
/// It follows a target on the XZ plane while staying at a fixed height and
/// looking straight down. Attach this to the "MinimapCamera" GameObject (the one
/// whose Camera has the minimap RenderTexture assigned as its Target Texture).
/// </summary>
[RequireComponent(typeof(Camera))]
public class MinimapCamera : MonoBehaviour
{
    public enum RotationMode
    {
        /// <summary>Minimap always aligned to the world (north stays up).</summary>
        FixedNorthUp,
        /// <summary>Minimap rotates with the target's heading (player-forward stays up).</summary>
        RotateWithTarget
    }

    [Header("Follow Target")]
    [Tooltip("Transform to follow. If empty, uses CameraDirector's target, then falls back to the object tagged 'Player'.")]
    public Transform target;

    [Header("Framing")]
    [Tooltip("Absolute world height the camera hovers at while looking down.")]
    public float height = 100f;
    [Tooltip("Orthographic half-size (visible radius). Smaller = more zoomed in.")]
    public float zoom = 62.7f;
    [Tooltip("Follow catch-up speed. 0 = snap instantly to the target each frame.")]
    public float followSmooth = 0f;

    [Header("Rotation")]
    [Tooltip("FixedNorthUp keeps the map world-aligned; RotateWithTarget spins it with the target.")]
    public RotationMode rotationMode = RotationMode.FixedNorthUp;

    private Camera _cam;

    void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    void Start()
    {
        if (target == null) AcquireTarget();
        _cam.orthographic = true;
        _cam.orthographicSize = zoom;
        ApplyTransform(instant: true);
    }

    void LateUpdate()
    {
        if (target == null)
        {
            AcquireTarget();
            if (target == null) return;
        }

        // Keep zoom live so it can be tuned in the inspector during play.
        _cam.orthographicSize = zoom;
        ApplyTransform(instant: followSmooth <= 0f);
    }

    /// <summary>Change the followed target at runtime (e.g. when possessing an enemy).</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        ApplyTransform(instant: true);
    }

    private void ApplyTransform(bool instant)
    {
        if (target == null) return;

        Vector3 desired = new Vector3(target.position.x, height, target.position.z);
        transform.position = instant
            ? desired
            : Vector3.Lerp(transform.position, desired, followSmooth * Time.deltaTime);

        float yaw = rotationMode == RotationMode.RotateWithTarget ? target.eulerAngles.y : 0f;
        transform.rotation = Quaternion.Euler(90f, yaw, 0f);
    }

    private void AcquireTarget()
    {
        // Prefer the same target the main gameplay camera is following.
        if (CameraDirector.Instance != null && CameraDirector.Instance.Target != null)
        {
            target = CameraDirector.Instance.Target;
            return;
        }
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) target = player.transform;
    }
}
