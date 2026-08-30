using UnityEngine;

/// <summary>
/// UI 脉冲缩放：按设定频率在 minScale / maxScale 之间平滑放大缩小（sin 缓动，呼吸感）。
/// 挂到任意带 RectTransform 的 UI 物体即可。
/// </summary>
public class UIPulseScale : MonoBehaviour
{
    [Header("缩放范围")]
    [Tooltip("最小 localScale（z 通常保持 1，仅缩放 x/y）")]
    public Vector3 minScale = new Vector3(0.9f, 0.9f, 1f);
    [Tooltip("最大 localScale")]
    public Vector3 maxScale = new Vector3(1.1f, 1.1f, 1f);

    [Header("节奏")]
    [Min(0.01f)]
    [Tooltip("频率（Hz）：每秒完整放大缩小周期数。")]
    public float frequency = 1f;
    [Tooltip("随机初始相位，让多个 UI 不同步缩放。")]
    public bool randomizePhase = true;
    [Tooltip("使用 unscaled time（暂停菜单 / 慢动作时也持续）。")]
    public bool useUnscaledTime = true;

    [Header("运行时控制")]
    [Tooltip("启动即开始动画。")]
    public bool animateOnStart = true;

    bool animating;
    float phase;

    void Start()
    {
        phase = randomizePhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
        animating = animateOnStart;
    }

    void Update()
    {
        if (!animating) return;
        float t = useUnscaledTime ? Time.unscaledTime : Time.time;
        // sin 归一化到 0..1，在 min/max 之间平滑往返
        float wave = (Mathf.Sin(t * frequency * Mathf.PI * 2f + phase) + 1f) * 0.5f;
        transform.localScale = Vector3.LerpUnclamped(minScale, maxScale, wave);
    }

    /// <summary>开始 / 暂停动画。</summary>
    public void SetAnimating(bool value)
    {
        animating = value;
    }
}
