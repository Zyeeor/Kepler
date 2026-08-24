using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 火苗跳动特效（纯程序化，无需序列帧）。
///
/// 真实火苗的视觉本质 = 非均匀缩放（纵向窜动 + 横向挤压）+ 摇曳 + 亮度闪烁。
/// 本组件用多组不同频率的正弦波叠加实现，频率刻意取非整数倍避免机械循环感。
///
/// 职责边界（重要）：
///   * 本组件只写 localScale / localRotation / Image.color
///   * 不碰 anchoredPosition —— 位置由外部驱动（如 PlayerHealth 让火苗跟随血条末端）
///   两者操作不同属性，可安全共存。
///
/// pivot 约定：火苗应从底部燃起，pivot 建议 (0.5, 0)。
/// 通过 Editor 添加组件时 Reset() 会自动设好；手动改过的不覆盖。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class UIFlameFlicker : MonoBehaviour
{
    [Header("纵向窜动（火苗抽高/回落）")]
    [Tooltip("纵向拉伸幅度（0.2 = 最高时比基准高 20%）。")]
    [Range(0f, 0.6f)] public float verticalStretch = 0.18f;
    [Tooltip("纵向窜动速度。")]
    [Range(0.5f, 30f)] public float stretchSpeed = 9f;

    [Header("横向挤压（配合纵向形成体积感）")]
    [Tooltip("横向挤压幅度。与纵向反相 —— 抽高时收窄，回落时铺开。")]
    [Range(0f, 0.5f)] public float horizontalSquash = 0.11f;
    [Tooltip("横向挤压速度。刻意与纵向不同频，避免整体均匀缩放的\"呼吸球\"感。")]
    [Range(0.5f, 30f)] public float squashSpeed = 13f;

    [Header("摇曳（左右摆动）")]
    [Tooltip("最大摆动角度（度）。")]
    [Range(0f, 25f)] public float swayAngle = 6f;
    [Tooltip("摇曳速度。")]
    [Range(0.1f, 15f)] public float swaySpeed = 4.5f;

    [Header("亮度闪烁")]
    [Tooltip("要闪烁的 Image（留空自动取自身）。")]
    public Image targetImage;
    [Tooltip("亮度浮动幅度（0.25 = 明暗各 25%）。0 = 关闭闪烁。")]
    [Range(0f, 0.6f)] public float brightnessAmount = 0.2f;
    [Tooltip("闪烁速度。取较高频率模拟火焰的细碎明暗抖动。")]
    [Range(0.5f, 40f)] public float brightnessSpeed = 16f;

    [Header("时间基准")]
    [Tooltip("勾选后使用 unscaledTime —— 子弹时间/暂停时火苗照常跳动（血条特效通常需要）。")]
    public bool useUnscaledTime = true;
    [Tooltip("勾选后启动时随机相位，避免场景里多个火苗整齐同步跳动。")]
    public bool randomizePhase = true;

    private Vector3 baseScale = Vector3.one;
    private Color baseColor = Color.white;
    private float phase;
    private bool hasImage;

    void Awake()
    {
        CacheBase();
        phase = randomizePhase ? Random.Range(0f, 100f) : 0f;
    }

    void OnEnable()
    {
        // 复用/池化时重新取基准，避免拿到上次跳动中途的缩放值当基准而逐帧漂移。
        CacheBase();
    }

    void CacheBase()
    {
        // 只在接近未变形状态时更新基准，避免把跳动中的瞬时值固化为基准。
        baseScale = Vector3.one;
        if (targetImage == null) targetImage = GetComponent<Image>();
        hasImage = targetImage != null;
        if (hasImage)
        {
            Color c = targetImage.color;
            // 反推基准色：把当前色除以可能已施加的亮度系数不可靠，
            // 因此直接以"首次启用时的色"为基准（Inspector 里配的原色）。
            baseColor = c;
        }
    }

    void LateUpdate()
    {
        float t = (useUnscaledTime ? Time.unscaledTime : Time.time) + phase;

        // ── 纵向：双波叠加（主波 + 1.73 倍频副波），非整数倍频 → 无明显循环 ──
        float vy = Mathf.Sin(t * stretchSpeed) * 0.65f
                 + Mathf.Sin(t * stretchSpeed * 1.73f + 1.3f) * 0.35f;

        // ── 横向：与纵向反相（抽高时收窄），同样双波叠加 ──
        float vx = Mathf.Sin(t * squashSpeed + 2.1f) * 0.6f
                 + Mathf.Sin(t * squashSpeed * 1.41f) * 0.4f;

        float scaleY = 1f + vy * verticalStretch;
        float scaleX = 1f - vy * horizontalSquash * 0.6f   // 体积守恒：纵向抽高 → 横向自然收窄
                          + vx * horizontalSquash * 0.4f;  // 叠加独立横向抖动

        transform.localScale = new Vector3(
            baseScale.x * Mathf.Max(0.05f, scaleX),
            baseScale.y * Mathf.Max(0.05f, scaleY),
            baseScale.z);

        // ── 摇曳：绕 Z 轴摆动（pivot 在底部时看起来像火苗被风吹偏）──
        if (swayAngle > 0.01f)
        {
            float sway = Mathf.Sin(t * swaySpeed) * 0.7f
                       + Mathf.Sin(t * swaySpeed * 2.3f + 0.7f) * 0.3f;
            transform.localRotation = Quaternion.Euler(0f, 0f, sway * swayAngle);
        }

        // ── 亮度：高频细碎明暗抖动 ──
        if (hasImage && brightnessAmount > 0.001f)
        {
            float flick = Mathf.Sin(t * brightnessSpeed) * 0.5f
                        + Mathf.Sin(t * brightnessSpeed * 1.87f + 2.4f) * 0.3f
                        + Mathf.Sin(t * brightnessSpeed * 3.11f) * 0.2f;
            float mul = 1f + flick * brightnessAmount;
            targetImage.color = new Color(
                Mathf.Clamp01(baseColor.r * mul),
                Mathf.Clamp01(baseColor.g * mul),
                Mathf.Clamp01(baseColor.b * mul),
                baseColor.a);
        }
    }

    void OnDisable()
    {
        // 归位，避免禁用瞬间把变形状态留在 prefab / 下次启用时的初始帧上。
        transform.localScale = baseScale;
        transform.localRotation = Quaternion.identity;
        if (hasImage) targetImage.color = baseColor;
    }

#if UNITY_EDITOR
    void Reset()
    {
        // Editor 添加组件时自动配好底部 pivot（火苗从底部燃起）。
        var rt = (RectTransform)transform;
        if (Mathf.Approximately(rt.pivot.y, 0.5f) && Mathf.Approximately(rt.pivot.x, 0.5f))
            rt.pivot = new Vector2(0.5f, 0f);
        targetImage = GetComponent<Image>();
    }
#endif
}
