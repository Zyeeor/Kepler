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

    [Header("Zoom Mode")]
    [Tooltip("Use orthographic size instead of followDistance for zoom control.")]
    [SerializeField] private bool useOrthographicZoom = false;
    [Tooltip("Base orthographic size (when useOrthographicZoom is true).")]
    [SerializeField] private float baseOrthoSize = 10f;

    [Header("Start Animation")]
    [Tooltip("Enable a zoom-in then zoom-out animation on game start.")]
    [SerializeField] private bool playStartAnimation = true;
    [Tooltip("Starting distance multiplier (zoomed in).")]
    [SerializeField] private float startZoomInMult = 0.3f;
    [Tooltip("Peak distance multiplier (zoomed out after zoom in).")]
    [SerializeField] private float startZoomOutMult = 1.2f;
    [Tooltip("Duration of the entire start animation.")]
    [SerializeField] private float startAnimDuration = 2f;
    [Tooltip("Animation curve: 0=start, 1=end. X axis is normalized time.")]
    [SerializeField] private AnimationCurve startAnimCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

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
    private LensSettings _savedLens;                                    // 透视演出前保存的完整 Lens，恢复时写回
    private CinemachineBrain.LensModeOverrideSettings _savedBrainLensModeOverride; // 演出前 Brain 的投影覆盖设置，恢复时写回
    private bool _lensOverrideActive;                                   // 透视演出覆盖是否生效（防重复/错误恢复）

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

    /// <summary>直距（镜头到目标）。运行时演出可读写；Inspector 的 [Range] 仅约束编辑期拖拽。</summary>
    public float FollowDistance
    {
        get => followDistance;
        set { followDistance = value; ApplyFraming(); }
    }

    /// <summary>俯仰角（度）：45=经典俯视；负值=仰视（摄像机在目标下方），供开场演出"仰视→俯视"过渡。</summary>
    public float PitchAngle
    {
        get => pitchAngle;
        set { pitchAngle = value; ApplyFraming(); }
    }

    /// <summary>跟随阻尼（越大越平滑滞后）。演出切位前临时设 0 可让摄像机瞬时贴位（无滑移）。</summary>
    public float FollowDamping
    {
        get => followDamping;
        set { followDamping = value; ApplyFraming(); }
    }

    void Awake()
    {
        Instance = this;
        EnsureRig();
        EnsureCombatEffectManager();
        EnsureCombatAudioManager();
        // 调试飞行相机（开发工具，F4 切换；与 CombatEffectManager 同款自举模式）
        DebugCameraController.EnsureOn(GetComponent<Camera>());
    }

    private static void EnsureCombatEffectManager()
    {
        if (CombatEffectManager.Instance != null)
            return;
        if (FindFirstObjectByType<CombatEffectManager>() != null)
            return;

        // Boot on the camera so combat scenes get hit feedback without manual wiring.
        CameraDirector director = Instance;
        if (director != null)
            director.gameObject.AddComponent<CombatEffectManager>();
    }

    private static void EnsureCombatAudioManager()
    {
        if (CombatAudioManager.Instance != null)
            return;
        if (FindFirstObjectByType<CombatAudioManager>() != null)
            return;

        CameraDirector director = Instance;
        if (director != null)
            director.gameObject.AddComponent<CombatAudioManager>();
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

        if (playStartAnimation)
            StartCoroutine(StartAnimationRoutine());
    }

    IEnumerator StartAnimationRoutine()
    {
        float baseVal = useOrthographicZoom ? baseOrthoSize : followDistance;
        float elapsed = 0f;

        while (elapsed < startAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / startAnimDuration);
            float curveVal = startAnimCurve.Evaluate(t);
            float distMult = Mathf.Lerp(startZoomInMult, startZoomOutMult, curveVal);

            if (useOrthographicZoom)
                SetOrthoSize(baseVal * distMult);
            else
            {
                followDistance = baseVal * distMult;
                ApplyFraming();
            }

            yield return null;
        }

        if (useOrthographicZoom)
            SetOrthoSize(baseVal);
        else
        {
            followDistance = baseVal;
            ApplyFraming();
        }
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
        Shake(force, 0f);
    }

    /// <summary>
    /// Trigger a screen shake. When <paramref name="duration"/> &gt; 0, temporarily overrides impulse duration.
    /// </summary>
    public void Shake(float force, float duration)
    {
        if (_impulseSource == null) return;

        float previousDuration = _impulseSource.ImpulseDefinition.ImpulseDuration;
        if (duration > 0f)
            _impulseSource.ImpulseDefinition.ImpulseDuration = duration;

        _impulseSource.GenerateImpulseWithForce(force);

        if (duration > 0f)
            _impulseSource.ImpulseDefinition.ImpulseDuration = previousDuration;
    }

    /// <summary>Trigger a screen shake in a specific world-space direction/magnitude.</summary>
    public void Shake(Vector3 velocity)
    {
        if (_impulseSource != null)
            _impulseSource.GenerateImpulseWithVelocity(velocity);
    }

    /// <summary>Scale Time.timeScale for the given unscaled duration, then restore.</summary>
    public void HitStop(float duration)
    {
        HitStop(duration, hitStopTimeScale);
    }

    /// <summary>
    /// Scale Time.timeScale for the given unscaled duration, then restore.
    /// Prefer Animator-only hit-stop via CombatEffectManager unless useGlobalTimeScale is required.
    /// </summary>
    public void HitStop(float duration, float timeScale)
    {
        if (!isActiveAndEnabled || duration <= 0f) return;
        if (_hitStopRoutine != null) StopCoroutine(_hitStopRoutine);
        _hitStopRoutine = StartCoroutine(HitStopRoutine(duration, timeScale));
    }

    // ---- Internal ---------------------------------------------------------

    private IEnumerator HitStopRoutine(float duration, float timeScale)
    {
        // Don't fight a full pause (menu / death / bullet-time controlled elsewhere).
        if (Time.timeScale <= 0.0001f)
        {
            _hitStopRoutine = null;
            yield break;
        }

        // HitStop 域请求：顿帧期间压住 BulletTime 等低优先级域；结束 Pop 恢复（栈自动仲裁，无需 Approximately 守卫）
        TimeScaleManager.Push(TimeDomain.HitStop, timeScale);
        yield return new WaitForSecondsRealtime(duration);

        TimeScaleManager.Pop(TimeDomain.HitStop);
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

    /// <summary>Set orthographic size on the virtual camera lens.</summary>
    public void SetOrthoSize(float size)
    {
        if (virtualCamera != null)
        {
            virtualCamera.Lens.OrthographicSize = size;
        }
    }

    /// <summary>
    /// 临时把虚拟相机切为透视投影（等同 F4 调试相机手感）。
    /// 项目主相机为正交投影（无纵深，物体等大平移），开场演出（降落+拉远+仰视）在正交下不自然；
    /// 切透视后 FollowDistance/PitchAngle 插值才有真实纵深感。演出结束调用 EndPerspectiveLens 完整还原。
    ///
    /// 关键：CinemachineBrain.LensModeOverride.Enabled 默认 false——不打开它，虚拟相机 Lens.ModeOverride
    /// 不会被推给主相机（Brain 仅在 Enabled=true 时写 cam.orthographic）。本方法临时打开并在结束还原。
    /// </summary>
    public void BeginPerspectiveLens(float fov = 60f)
    {
        if (virtualCamera == null || _lensOverrideActive) return;
        _savedLens = virtualCamera.Lens;
        var lens = _savedLens;
        lens.ModeOverride = LensSettings.OverrideModes.Perspective;
        lens.FieldOfView = fov;
        virtualCamera.Lens = lens;

        var brain = FindBrain();
        if (brain != null)
        {
            _savedBrainLensModeOverride = brain.LensModeOverride;
            var brainOverride = brain.LensModeOverride;
            brainOverride.Enabled = true; // 打开投影模式覆盖开关，演出期间推透视
            brain.LensModeOverride = brainOverride;
        }
        _lensOverrideActive = true;
    }

    /// <summary>结束透视演出，完整还原演出前的 Lens 与 Brain 投影覆盖设置。幂等。</summary>
    public void EndPerspectiveLens()
    {
        if (!_lensOverrideActive) return;
        if (virtualCamera != null) virtualCamera.Lens = _savedLens;
        var brain = FindBrain();
        if (brain != null)
        {
            brain.LensModeOverride = _savedBrainLensModeOverride;
            // Brain 仅在 LensModeOverride.Enabled=true 时推投影模式（CinemachineBrain 762-772 行）；
            // 恢复 Enabled=false 后主相机停在透视态，需手动推回正交（项目主相机恒正交）。
            if (brain.OutputCamera != null)
            {
                brain.OutputCamera.orthographic = true;
                brain.OutputCamera.orthographicSize = _savedLens.OrthographicSize;
            }
        }
        _lensOverrideActive = false;
    }

    CinemachineBrain FindBrain()
    {
        // CM 3.1.6 中 CinemachineCamera 无 OutputCamera 属性；项目单主相机单 Brain，直接取活动 Brain。
        return CinemachineBrain.ActiveBrainCount > 0 ? CinemachineBrain.GetActiveBrain(0) : null;
    }

    /// <summary>Current orthographic size. Setter 供演出（透视→正交视觉匹配过渡）缓动。</summary>
    public float OrthoSize
    {
        get => virtualCamera != null ? virtualCamera.Lens.OrthographicSize : 0f;
        set => SetOrthoSize(value);
    }
}
