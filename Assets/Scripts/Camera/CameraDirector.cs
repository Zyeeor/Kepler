using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;

/// <summary>
/// Central camera controller for the game.
///  - 45-degree top-down follow driven by Cinemachine.
///  - Screen shake and hit-stop (frame freeze / 顿帧) for hit feedback.
///
/// Attach this to the Main Camera. It bootstraps the required Cinemachine rig
/// (Brain + virtual camera + impulse listener + impulse source) at runtime, so
/// no manual scene wiring is needed. Replaces the legacy CameraFollow component.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraDirector : MonoBehaviour
{
    public static CameraDirector Instance { get; private set; }

    [Header("Follow Target")]
    [Tooltip("Transform the camera follows. If empty, the object tagged 'Player' is used.")]
    [SerializeField] private Transform target;

    [Header("45 Top-Down Framing")]
    [Tooltip("Straight-line distance from the target to the camera.")]
    [SerializeField] private float followDistance = 18f;
    [Tooltip("Downward look angle in degrees (45 = classic top-down).")]
    [Range(10f, 89f)]
    [SerializeField] private float pitchAngle = 45f;
    [Tooltip("Horizontal rotation of the rig around the target, in degrees.")]
    [SerializeField] private float yawAngle = 0f;
    [Tooltip("How smoothly the camera tracks the target. Larger = heavier/slower.")]
    [SerializeField] private float followDamping = 1f;

    [Header("Hit Stop (顿帧)")]
    [Tooltip("Time scale used during a hit-stop. 0 = full freeze.")]
    [Range(0f, 1f)]
    [SerializeField] private float hitStopTimeScale = 0f;

    [Header("Cinemachine (optional)")]
    [Tooltip("Assign an existing CinemachineCamera to drive. If empty, one is created at runtime.")]
    [SerializeField] private CinemachineCamera virtualCamera;

    private CinemachineFollow _follow;
    private CinemachineImpulseSource _impulseSource;
    private Coroutine _hitStopRoutine;

    /// <summary>Follow target. Setting it re-points the Cinemachine virtual camera.</summary>
    public Transform Target
    {
        get => target;
        set
        {
            target = value;
            ApplyTarget();
        }
    }

    void Awake()
    {
        Instance = this;
        EnsureRig();
    }

    void Start()
    {
        if (target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
        }
        ApplyFraming();
        ApplyTarget();
    }

    void OnValidate()
    {
        // Keep framing live while tweaking values in the inspector during play.
        if (Application.isPlaying && _follow != null)
            ApplyFraming();
    }

    // ---- Public API -------------------------------------------------------

    /// <summary>Trigger a screen shake. force scales the configured impulse strength.</summary>
    public void Shake(float force)
    {
        if (_impulseSource != null)
            _impulseSource.GenerateImpulseWithForce(force);
    }

    /// <summary>Trigger a screen shake in a specific world-space direction/magnitude.</summary>
    public void Shake(Vector3 velocity)
    {
        if (_impulseSource != null)
            _impulseSource.GenerateImpulseWithVelocity(velocity);
    }

    /// <summary>Freeze time for the given unscaled duration, then restore.</summary>
    public void HitStop(float duration)
    {
        if (!isActiveAndEnabled || duration <= 0f) return;
        if (_hitStopRoutine != null) StopCoroutine(_hitStopRoutine);
        _hitStopRoutine = StartCoroutine(HitStopRoutine(duration));
    }

    // ---- Internal ---------------------------------------------------------

    private IEnumerator HitStopRoutine(float duration)
    {
        // Don't fight a full pause (menu / death / bullet-time controlled elsewhere).
        float restore = Time.timeScale;
        if (restore <= 0.0001f)
        {
            _hitStopRoutine = null;
            yield break;
        }

        Time.timeScale = hitStopTimeScale;
        yield return new WaitForSecondsRealtime(duration);

        // Only restore if no other system took over the time scale meanwhile.
        if (Mathf.Approximately(Time.timeScale, hitStopTimeScale))
            Time.timeScale = restore;
        _hitStopRoutine = null;
    }

    private void EnsureRig()
    {
        // 1. Brain on the real camera.
        if (!TryGetComponent(out CinemachineBrain _))
            gameObject.AddComponent<CinemachineBrain>();

        // 2. Virtual camera (create a standalone one if none assigned).
        if (virtualCamera == null)
        {
            var go = new GameObject("CM VirtualCamera (CameraDirector)");
            virtualCamera = go.AddComponent<CinemachineCamera>();
        }

        // 3. Position control: fixed-angle follow.
        if (!virtualCamera.TryGetComponent(out _follow))
            _follow = virtualCamera.gameObject.AddComponent<CinemachineFollow>();

        // 4. Impulse listener so the virtual camera reacts to shakes.
        if (!virtualCamera.TryGetComponent(out CinemachineImpulseListener listener))
        {
            listener = virtualCamera.gameObject.AddComponent<CinemachineImpulseListener>();
            // AddComponent skips Reset(), so initialise the defaults manually.
            listener.ChannelMask = 1;
            listener.Gain = 1f;
        }

        // 5. Impulse source used to emit the shakes.
        if (!TryGetComponent(out _impulseSource))
        {
            _impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
            _impulseSource.ImpulseDefinition.ImpulseChannel = 1;
            _impulseSource.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            _impulseSource.ImpulseDefinition.ImpulseDuration = 0.25f;
            _impulseSource.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            _impulseSource.DefaultVelocity = new Vector3(0.3f, 0.3f, 0f);
        }
    }

    private void ApplyFraming()
    {
        if (_follow == null || virtualCamera == null) return;

        float pitch = pitchAngle * Mathf.Deg2Rad;
        // Behind (-Z) and above (+Y) the target, then rotated around Y by yaw.
        Vector3 offset = new Vector3(0f, followDistance * Mathf.Sin(pitch), -followDistance * Mathf.Cos(pitch));
        offset = Quaternion.Euler(0f, yawAngle, 0f) * offset;

        _follow.FollowOffset = offset;
        var tracking = _follow.TrackerSettings;
        tracking.BindingMode = BindingMode.WorldSpace;
        tracking.PositionDamping = Vector3.one * followDamping;
        _follow.TrackerSettings = tracking;

        // No Aim component => the virtual camera keeps this fixed look direction.
        virtualCamera.transform.rotation = Quaternion.Euler(pitchAngle, yawAngle, 0f);
    }

    private void ApplyTarget()
    {
        if (virtualCamera != null)
            virtualCamera.Follow = target;
    }
}
