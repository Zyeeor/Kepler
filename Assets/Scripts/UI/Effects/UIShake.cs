using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 震动（血条掉血时整体抖动）。
///
/// 数据源自包含：直接监听挂载对象上的 Slider 掉值，因此 Soul 池扣血、附身 Body 扣血、
/// 附身耐久流逝三条路径都无需额外接线 —— 它们最终都写同一个 Slider。
///
/// 职责边界（重要）：
///   * 本组件只写自身的 anchoredPosition / localRotation
///   * 不碰子物体，不碰 Slider 数值
///   血条右端的燃烧特效读取 fillRect 的世界角点定位，因此会自动跟随本震动，无需额外处理。
///
/// 为什么用 Update 而不是 LateUpdate：
///   HealthBarBurnParticles 在 LateUpdate 读血条世界角点定位火苗。
///   震动写在 Update 可保证「先震动、后取位」，火苗不会比血条晚一帧。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class UIShake : MonoBehaviour
{
    [Header("Trigger")]
    [Tooltip("监听掉值的 Slider（留空自动取自身）。数值下降即触发震动。")]
    public Slider watchedSlider;
    [Tooltip("触发所需的最小跌幅（占 Slider 满值的比例），用于过滤附身流逝的逐帧微量扣血。0 = 任何跌幅都震。")]
    [Range(0f, 0.2f)] public float minDropRatio = 0.004f;
    [Tooltip("按跌幅缩放强度：跌幅达到满值的该比例时取满强度。")]
    [Range(0.01f, 1f)] public float dropForFullStrength = 0.15f;

    [Header("Shake")]
    [Tooltip("最大位移幅度（UI 单位）。")]
    [Range(0f, 60f)] public float amplitude = 14f;
    [Tooltip("单次震动持续时间（秒）。")]
    [Range(0.02f, 2f)] public float duration = 0.22f;
    [Tooltip("抖动频率。偏高更急躁，偏低更沉重。")]
    [Range(1f, 60f)] public float frequency = 26f;
    [Tooltip("纵向幅度相对横向的比例。血条通常横向抖更好读。")]
    [Range(0f, 1f)] public float verticalRatio = 0.45f;
    [Tooltip("勾选后按自身 localScale 反向补偿，使屏幕上的实际位移等于 amplitude。非均匀缩放的 UI（如被拉长的血条）需要勾上，否则横纵抖动比例会失真。")]
    public bool compensateScale = true;
    [Tooltip("附带的最大旋转抖动（度）。默认 0 = 纯平移。注意：宽条状 UI 上即使 1~2 度也会让远端大幅上下扫动。")]
    [Range(0f, 10f)] public float rotationAmount = 0f;
    [Tooltip("旋转围绕 RectTransform 的几何中心，而非 pivot。pivot 不在中心时（如左上角）避免出现「以一端为轴翘起」。")]
    public bool rotateAroundCenter = true;

    [Header("Time")]
    [Tooltip("勾选后使用 unscaledTime —— 子弹时间 / 暂停时震动照常（血条反馈通常需要）。")]
    public bool useUnscaledTime = true;

    private RectTransform rect;
    private Vector2 basePosition;
    private Quaternion baseRotation;
    private float timeLeft;
    private float strength;
    private float seed;
    private float noiseTime;
    private float lastValue;
    private bool hasBaseline;

    /// <summary>
    /// 单帧最大推进时间。防止加载 / 编译 / 断点造成的巨大 deltaTime 一帧吞掉整段震动。
    /// </summary>
    const float MaxFrameDelta = 0.05f;

    void Awake()
    {
        rect = (RectTransform)transform;
        if (watchedSlider == null) watchedSlider = GetComponent<Slider>();
        seed = Random.Range(0f, 100f);
        CacheBase();
    }

    void OnEnable()
    {
        // 复用/重开时重取基准，避免把上次震动中途的偏移固化成基准而逐次漂移。
        CacheBase();
        timeLeft = 0f;
        hasBaseline = false;
    }

    void CacheBase()
    {
        if (rect == null) rect = (RectTransform)transform;
        basePosition = rect.anchoredPosition;
        baseRotation = rect.localRotation;
    }

    void Update()
    {
        PollSlider();
        ApplyShake();
    }

    /// <summary>Slider 掉值即震；回血与满值重置不震。</summary>
    void PollSlider()
    {
        if (watchedSlider == null) return;

        float value = watchedSlider.value;
        float span = watchedSlider.maxValue - watchedSlider.minValue;
        if (!hasBaseline)
        {
            lastValue = value;
            hasBaseline = true;
            return;
        }

        float drop = lastValue - value;
        lastValue = value;
        if (drop <= 0f || span <= 0f) return;

        float dropRatio = drop / span;
        if (dropRatio < minDropRatio) return;

        Shake(Mathf.Clamp01(dropRatio / dropForFullStrength));
    }

    /// <summary>主动触发一次震动。force 为 0~1 的强度系数。</summary>
    public void Shake(float force = 1f)
    {
        force = Mathf.Clamp01(force);
        // 取较大值而非覆盖：连续受击时不会被后来的轻微一击削弱当前震动。
        strength = Mathf.Max(strength, force);
        timeLeft = duration;
        // 每次重新起震都重置噪声相位，避免长时间累积后落进 Perlin 的高数值区精度塌陷。
        noiseTime = seed;
    }

    void ApplyShake()
    {
        if (timeLeft <= 0f) return;

        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        // 卡帧保护：巨大 delta 只按上限推进，否则一帧就把整段震动吃掉，玩家什么也看不到。
        delta = Mathf.Min(delta, MaxFrameDelta);

        timeLeft -= delta;
        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            strength = 0f;
            rect.anchoredPosition = basePosition;
            rect.localRotation = baseRotation;
            return;
        }

        // 线性衰减包络：起手最猛、尾部收干净，避免残留微抖。
        float envelope = (timeLeft / duration) * strength;

        // 用自增的局部计时器而非 Time.unscaledTime * frequency：
        // 后者在游戏跑久后会涨到上万，三角函数在该量级浮点精度塌陷 ⇒ 抖动逐渐冻结。
        // 局部计时器每次起震归零，恒定落在低数值区。
        noiseTime += delta * frequency;

        // 正弦叠加而非 PerlinNoise：Perlin 实际输出集中在 0.2~0.8（remap 后仅 ±0.6），
        // 且整数格点恒为 0.5 ⇒ 峰值远达不到 amplitude。正弦能满幅摆动，抖动力度可控。
        // 频率取非整数倍，避免出现规律的机械往复。
        float nx = Mathf.Sin(noiseTime) * 0.7f + Mathf.Sin(noiseTime * 2.37f + 1.7f) * 0.3f;
        float ny = Mathf.Sin(noiseTime * 1.61f + 2.4f) * 0.7f + Mathf.Sin(noiseTime * 3.11f) * 0.3f;

        Vector2 shakeOffset = new Vector2(
            nx * amplitude * envelope,
            ny * amplitude * verticalRatio * envelope);

        // anchoredPosition 处于自身 localScale 之内，缩放非 1 时屏幕位移会被放大 / 压缩。
        // 本项目血条 localScale 为 (2.09, 0.52)，不补偿会让横向抖动被放大 2 倍、纵向被压掉一半。
        if (compensateScale)
        {
            Vector3 scale = rect.localScale;
            if (Mathf.Abs(scale.x) > 0.0001f) shakeOffset.x /= scale.x;
            if (Mathf.Abs(scale.y) > 0.0001f) shakeOffset.y /= scale.y;
        }

        if (rotationAmount > 0.001f)
        {
            float nr = Mathf.Sin(noiseTime * 0.83f + 0.9f);
            float angle = nr * rotationAmount * envelope;
            rect.localRotation = baseRotation * Quaternion.Euler(0f, 0f, angle);

            // 旋转绕 pivot 发生。pivot 不在中心时（本项目血条 pivot 为左上角），
            // 转 1~2 度就会让远端上下扫过十几个单位，看起来是「以一端为轴翘起」而非平移。
            // 这里补一段反向位移，把旋转的支点搬到几何中心。
            if (rotateAroundCenter) shakeOffset += CenterPivotCompensation(angle);
        }
        else
        {
            rect.localRotation = baseRotation;
        }

        rect.anchoredPosition = basePosition + shakeOffset;
    }

    /// <summary>
    /// 把绕 pivot 的旋转等效成绕几何中心的旋转所需的补偿位移。
    /// 原理：绕中心旋转 = 绕 pivot 旋转 + 位移(中心旋转后的落点 → 中心原位)。
    /// </summary>
    Vector2 CenterPivotCompensation(float angleDegrees)
    {
        // pivot 指向几何中心的向量（本地未旋转坐标系，含 rect 尺寸）
        Vector2 pivotToCenter = new Vector2(
            (0.5f - rect.pivot.x) * rect.rect.width,
            (0.5f - rect.pivot.y) * rect.rect.height);
        if (pivotToCenter.sqrMagnitude < 0.0001f) return Vector2.zero;

        Vector2 rotated = Quaternion.Euler(0f, 0f, angleDegrees) * pivotToCenter;
        return pivotToCenter - rotated;
    }

    void OnDisable()
    {
        if (rect == null) return;
        rect.anchoredPosition = basePosition;
        rect.localRotation = baseRotation;
        timeLeft = 0f;
        strength = 0f;
    }
}
