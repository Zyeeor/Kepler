using System.Collections;
using UnityEngine;

/// <summary>
/// 开场降落演出（Opening Landing，新需求 2026-08-21）：
///   1. 新局 Tutorial 阶段开始时，灵魂从空中（地图出生点上方 soulStartHeight）落到地图；
///   2. 镜头同步演出：从近距离仰视玩家（近距 + 负 pitch），拉远到常规俯视（followDistance/pitch 基线）；
///   3. 演出期间屏蔽输入；完成后放行（LandingComplete）。
///
/// 生效范围：所有新局（主菜单"新游戏"/直接 Play 新局均经 Opening→Tutorial 阶段）。
/// 读档恢复阶段直接为 Waves/Choice，天然不触发（LandingComplete 默认 true，波门无感）。
///
/// 协作：
///   - WaveManager 首波前等待本组件 LandingComplete（与教学波门并联，60s 兜底）；
///   - TutorialController.OpeningCarrierRoutine 等 LandingComplete 后才刷开场载体（避免飞行附身打断降落）。
/// </summary>
public class OpeningLandingSequence : MonoBehaviour
{
    public static OpeningLandingSequence Instance { get; private set; }

    /// <summary>降落完成门（默认 true=未启动/未配置不阻塞；演出开始才关闭）。</summary>
    public static bool LandingComplete { get; private set; } = true;

    [Header("总开关")]
    [Tooltip("关闭则完全跳过开场降落演出（行为等价旧版）。")]
    public bool flowEnabled = true;

    [Header("降落")]
    [Tooltip("灵魂起始高度（相对地图出生点 Y；灵魂 XZ 保持出生点位置）。")]
    public float soulStartHeight = 20f;
    [Tooltip("降落时长（秒，透视全程）：灵魂从 soulStartHeight 落到地面；镜头在其间完成凝视+旋转拉远。")]
    [Min(0.1f)] public float descentDuration = 3f;
    [Tooltip("降落曲线：0=高空起点，1=地面终点（时间归一化）。")]
    public AnimationCurve descentCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("镜头")]
    [Tooltip("镜头起始直距（近，贴玩家）。")]
    [Min(1f)] public float cameraNearDistance = 3.5f;
    [Tooltip("镜头起始俯仰角（负=仰视，摄像机在玩家下方；结束回到 CameraDirector 基线的俯视）。-88=基本竖直向上仰望。")]
    public float cameraLookUpPitch = -88f;
    [Tooltip("镜头凝视时长（秒）：前段镜头保持竖直向上近距凝视（灵魂落向镜头），结束后才旋转拉远'躲开'。")]
    [Min(0f)] public float cameraHoldDuration = 0.8f;
    [Tooltip("镜头旋转拉远动画时长（秒，凝视结束后，透视下）。")]
    [Min(0.1f)] public float cameraAnimDuration = 2.2f;
    [Tooltip("落地后透视→正交切换的收尾缓动时长（秒）：匹配尺寸缓动回基线正交尺寸。")]
    [Min(0.1f)] public float lensTransitionDuration = 0.5f;

    bool flowStarted;
    SoulActor soul;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        // 退订 RunSession（DDOL 常驻）事件，防场景重载后悬空委托
        var session = RunSession.Instance;
        if (session != null) session.OnPhaseChanged -= OnPhaseChanged;
        // 兜底还原透视覆盖（演出中断时镜头别卡在透视态）
        if (CameraDirector.Instance != null) CameraDirector.Instance.EndPerspectiveLens();
        LandingComplete = true; // 场景卸载兜底：防静态残留阻塞下一局
    }

    void Start()
    {
        if (!flowEnabled) return;

        var session = RunSession.Instance;
        // 补查：WaveManager.AutoStartRoutine 的 Opening→Tutorial 可能早于本组件 Start（初始化序竞争）
        if (session != null && session.CurrentPhase == RunPhase.Tutorial)
            StartFlow();
        if (session != null)
            session.OnPhaseChanged += OnPhaseChanged;
    }

    void OnPhaseChanged(RunPhase phase)
    {
        if (phase == RunPhase.Tutorial) StartFlow();
    }

    void StartFlow()
    {
        if (flowStarted) return;
        flowStarted = true;
        StartCoroutine(LandingRoutine());
    }

    IEnumerator LandingRoutine()
    {
        LandingComplete = false;
        PlayerController.SetGameplayInputBlocked(true, "OpeningLanding");

        soul = FindFirstObjectByType<SoulActor>();
        var director = CameraDirector.Instance;

        if (soul == null || director == null)
        {
            Debug.LogWarning("[OpeningLanding] 灵魂或镜头未就绪，跳过降落演出（保底放行）。");
            PlayerController.SetGameplayInputBlocked(false, "OpeningLanding");
            LandingComplete = true;
            yield break;
        }

        float baseHover = soul.hoverHeight;
        float baseDist = director.FollowDistance;
        float basePitch = director.PitchAngle;
        float baseOrtho = director.OrthoSize; // 基线正交尺寸（切透视前采样，视觉匹配过渡的终点）

        // 切透视投影（等同 F4 调试相机手感）：正交下无纵深，降落+拉远+仰视→俯视不自然
        director.BeginPerspectiveLens();

        // 初始帧：灵魂抬到高空（XZ 保持出生点）；镜头瞬时跳位到近距仰视。
        // 关键：Cinemachine PositionDamping=1 会让镜头从基线位置"滑"到新位——视觉上镜头比灵魂还快地俯冲下落。
        // 临时 damping=0 让镜头一帧内直接贴位（镜头先不动，只有灵魂从高处落下）。
        float baseDamping = director.FollowDamping;
        director.FollowDamping = 0f;
        soul.hoverHeight = soulStartHeight;
        soul.EnforceHoverHeight();
        director.FollowDistance = cameraNearDistance;
        director.PitchAngle = cameraLookUpPitch;
        Debug.Log($"[OpeningLanding] 降落演出开始（透视投影）：灵魂 @{(soul != null ? soul.transform.position.ToString() : "?")}，镜头瞬时贴位 {cameraNearDistance}m / {cameraLookUpPitch}° → {baseDist}m / {basePitch}°。");

        yield return null; // 一帧：damping=0 让 Cinemachine 立即贴位，之后恢复阻尼开始插值
        director.FollowDamping = baseDamping;

        // ── 段1（透视全程）：灵魂落向镜头 → 镜头旋转拉远。前 cameraHoldDuration 秒镜头固定近距
        // 仰视凝视（灵魂从高空砸向镜头），之后镜头绕灵魂旋转（-88→45°）并拉远（3.5→18m）"躲开"。
        // 穿地校验（17:02 同参数组合验证）：镜头最低 Y≈6.8m，不穿地。 ──
        float elapsed = 0f;
        while (elapsed < descentDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / descentDuration);
            float k = EvaluateCurve(descentCurve, t);
            float camT = Mathf.Clamp01((elapsed - cameraHoldDuration) / Mathf.Max(0.01f, cameraAnimDuration));
            float camK = EvaluateCurve(descentCurve, camT);

            // 灵魂自由态 EnforceHoverHeight 每帧把 Y 贴到 hoverHeight → 直接插值 hoverHeight 即驱动降落，无拉扯
            soul.hoverHeight = Mathf.Lerp(soulStartHeight, baseHover, k);
            soul.EnforceHoverHeight();
            director.FollowDistance = Mathf.Lerp(cameraNearDistance, baseDist, camK);
            director.PitchAngle = Mathf.Lerp(cameraLookUpPitch, basePitch, camK);
            yield return null;
        }

        // 精确落位：灵魂落地、镜头已回到基线构图（18m/45°）
        soul.hoverHeight = baseHover;
        soul.EnforceHoverHeight();
        director.FollowDistance = baseDist;
        director.PitchAngle = basePitch;

        // ── 切换点：灵魂已落地后，透视→正交。此时镜头在远景基线构图，匹配值天然接近基线
        // （tan(fov/2)*dist ≈ 10.4 ≈ 基线 10），切换差异小。
        // 匹配：正交下距镜头 d 处画面半高 = orthoSize，透视下半高 = tan(fov/2)*d；
        // 取 orthoMatch = tan(fov/2) * 镜头到灵魂距离 → 切换瞬间灵魂画面大小连续（零闪跳）。
        // 同步设置（不 yield）：EndPerspectiveLens 已把主相机推回正交，此处改虚拟相机 Lens，
        // 本帧 Brain LateUpdate 会用 orthoMatch 覆盖主相机 → 无"先跳基线大小再弹回"的闪跳帧。 ──
        var mainCam = Camera.main;
        float distToSoul = mainCam != null ? (soul.transform.position - mainCam.transform.position).magnitude : baseDist;
        float fov = mainCam != null ? mainCam.fieldOfView : 60f;
        float orthoMatch = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * distToSoul;
        director.EndPerspectiveLens();
        director.OrthoSize = orthoMatch;
        Debug.Log($"[OpeningLanding] 落地后切换透视→正交：distToSoul={distToSoul:F1}m，orthoMatch={orthoMatch:F2} → 基线 {baseOrtho:F1}。");
        yield return null;

        // ── 收尾：匹配尺寸短缓动回基线正交尺寸（远景下匹配值≈基线，差异小，快速过渡）──
        float lensElapsed = 0f;
        while (lensElapsed < lensTransitionDuration)
        {
            lensElapsed += Time.deltaTime;
            float lt = Mathf.Clamp01(lensElapsed / lensTransitionDuration);
            float lk = EvaluateCurve(descentCurve, lt);
            director.OrthoSize = Mathf.Lerp(orthoMatch, baseOrtho, lk);
            yield return null;
        }
        director.OrthoSize = baseOrtho;

        PlayerController.SetGameplayInputBlocked(false, "OpeningLanding");
        LandingComplete = true;
        Debug.Log($"[OpeningLanding] 降落演出完成（{elapsed:F1}s + 收尾 {lensElapsed:F1}s），落地后切换透视→正交，波门放行。");
    }

    static float EvaluateCurve(AnimationCurve curve, float t)
        => curve != null && curve.length >= 2 ? curve.Evaluate(t) : t;
}
