using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 技能烧血闪光（付出 HP 代价的瞬间，整条血条铺开一层高亮火焰后立即燃尽）。
///
/// 与 HealthBarBurnParticles 的分工（两者可共存、互不干扰）：
///   * HealthBarBurnParticles —— 持续态：只在血条断口处小火苗常燃，表示附身耐久被动流逝；
///   * 本组件 —— 瞬时态：沿整条血条一次性喷发再迅速熄灭，表示主动烧血的代价。
///
/// 为什么同样不能把 ParticleSystem 直接挂进血条：
///   UICanvas 是 Screen Space - Overlay，Overlay Canvas 在所有相机之后直接绘制到屏幕；
///   而 ParticleSystemRenderer 是普通世界空间 Renderer —— 既不参与 Canvas 批次，
///   位置又落在屏幕像素坐标处的世界空间，实际结果是看不到。
///   因此这里关掉它的 Renderer，改由本组件每帧把粒子烘进 CanvasRenderer 的 UI 网格。
///
/// 触发源：MonsterActor.AbilityHpCostPaid（HP 代价实际扣除后抛出）。
///   刻意不监听 Slider 掉值 —— 那样会把受击掉血也当成烧血点着。
///
/// 职责边界（重要）：
///   本组件只做三件事 —— 沿血条铺开发射、烘录粒子网格、按代价大小缩放喷发量。
///   火焰形态（寿命 / 速度 / 颜色 / 大小曲线）不在这里写死，全部在 ParticleSystem 上配置。
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasRenderer))]
public class HealthBarSkillBurnFlash : MaskableGraphic
{
    [Header("Source")]
    [Tooltip("提供粒子模拟的 ParticleSystem（留空自动取子物体上的第一个）。其 Renderer 会被强制关闭。")]
    public ParticleSystem particles;
    [Tooltip("粒子贴图（建议 Art folder/UI/HealthBar_Flame.png）。留空则退化为白色方块。")]
    public Texture particleTexture;

    [Header("Placement")]
    [Tooltip("铺开范围参照的血条 Slider（留空自动取 PlayerHealth.healthSlider）。")]
    public Slider healthBar;
    [Tooltip("勾选后只点燃已填充的血量段；取消则铺满整条血条（含已空的槽位）。")]
    public bool limitToFilledPart = false;
    [Tooltip("纵向散布占血条高度的比例。1 = 铺满条高，>1 会溢出条外。")]
    [Range(0f, 2f)] public float verticalSpread = 0.8f;

    [Header("Burst")]
    [Tooltip("满强度时沿血条一次喷发的粒子数。")]
    [Range(4, 512)] public int burstParticles = 90;
    [Tooltip("单次代价达到 Body 最大生命的该比例时取满强度。")]
    [Range(0.002f, 0.5f)] public float costForFullStrength = 0.05f;
    [Tooltip("最小强度系数，保证微量代价（如普攻）也有可读反馈。")]
    [Range(0f, 1f)] public float minStrength = 0.4f;
    [Tooltip("两次喷发的最小间隔（秒）。激光类每秒付费的技能靠它避免刷爆 UI 网格。")]
    [Range(0f, 1f)] public float minInterval = 0.08f;

    [Header("Budget")]
    [Tooltip("单帧最多烘录的粒子数（每个粒子 4 顶点），防止 UI 网格膨胀。")]
    [Range(16, 2048)] public int maxBakedParticles = 512;

    private ParticleSystem.Particle[] buffer;
    private readonly Vector3[] fillCorners = new Vector3[4];
    private int lastBakedCount;
    private float lastBurstTime = -999f;

    public override Texture mainTexture
    {
        get { return particleTexture != null ? particleTexture : s_WhiteTexture; }
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        ResolveRefs();
        if (particles != null)
        {
            // 世界空间绘制必须关掉，否则粒子会真的出现在屏幕像素坐标那一片世界空间里。
            var psr = particles.GetComponent<ParticleSystemRenderer>();
            if (psr != null) psr.enabled = false;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        MonsterActor.AbilityHpCostPaid += OnAbilityHpCostPaid;
        EnsurePlaying();
    }

    protected override void OnDisable()
    {
        MonsterActor.AbilityHpCostPaid -= OnAbilityHpCostPaid;
        if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        lastBakedCount = 0;
        base.OnDisable();
    }

    void LateUpdate()
    {
        ResolveRefs();

        // 空闲帧（无存活粒子且上一帧也没有）不触发重建，避免常驻的 UI 网格重算。
        int alive = particles != null ? particles.particleCount : 0;
        if (alive > 0 || lastBakedCount > 0) SetVerticesDirty();
    }

    void ResolveRefs()
    {
        if (particles != null && healthBar != null) return;
        if (particles == null) particles = GetComponentInChildren<ParticleSystem>(true);
        if (healthBar == null)
        {
            var playerHealth = PlayerHealth.Instance;
            if (playerHealth != null) healthBar = playerHealth.healthSlider;
        }
    }

    /// <summary>
    /// 手动 Emit 出的粒子只有在系统处于播放中才会被推进模拟，因此发射前必须确认它在播放。
    ///
    /// 不能只在 OnEnable 里 Play 一次：particles 可能是 OnEnable 之后才被 Inspector 赋值
    /// 或由 ResolveRefs 补齐的，那时 Play 会被跳过，之后所有喷发都是空的。
    /// 发射速率与 Burst 应在 ParticleSystem 上配成 0，喷发完全由本组件触发。
    /// </summary>
    void EnsurePlaying()
    {
        if (particles == null) return;
        if (!particles.isPlaying) particles.Play(true);
    }

    /// <summary>只表现当前血条正在显示的那具 Body —— 其它怪的扣血与这条血条无关。</summary>
    void OnAbilityHpCostPaid(MonsterActor body, float cost)
    {
        if (body == null || cost <= 0f) return;
        var possession = PossessionManager.Instance;
        if (possession == null || possession.CurrentBody != body) return;

        // unscaledTime：子弹时间下技能反馈仍需按真实时间节流，与 UIShake 的时间基准一致。
        float now = Time.unscaledTime;
        if (now - lastBurstTime < minInterval) return;
        lastBurstTime = now;

        float ratio = body.maxHealth > 0f ? cost / body.maxHealth : 1f;
        float strength = costForFullStrength > 0f ? Mathf.Clamp01(ratio / costForFullStrength) : 1f;
        EmitAlongBar(Mathf.Lerp(minStrength, 1f, strength));
    }

    /// <summary>
    /// 沿血条铺开一次喷发。
    ///
    /// 取世界角点而非本地 rect：血条被 UIShake 抖动、被父级非均匀缩放时都能自动跟上，
    /// 与 HealthBarBurnParticles 的定位口径保持一致。
    /// </summary>
    void EmitAlongBar(float strength)
    {
        if (particles == null || healthBar == null || healthBar.fillRect == null) return;
        EnsurePlaying();

        healthBar.fillRect.GetWorldCorners(fillCorners);   // 0=左下 1=左上 2=右上 3=右下
        Vector3 leftMid = (fillCorners[0] + fillCorners[1]) * 0.5f;
        Vector3 rightMid = (fillCorners[2] + fillCorners[3]) * 0.5f;
        Vector3 heightVector = fillCorners[1] - fillCorners[0];

        Vector3 spanStart = leftMid;
        Vector3 spanEnd = rightMid;
        if (limitToFilledPart)
        {
            // 本项目血条 Fill 是 Image.type = Filled，fillRect 几何恒定不变，
            // 断口只体现在 fillAmount 上，因此必须按 fillAmount 在左右边界间插值。
            var fillImage = healthBar.fillRect.GetComponent<Image>();
            float fill = fillImage != null && fillImage.type == Image.Type.Filled ? fillImage.fillAmount : 1f;
            bool fromRight = fillImage != null
                && fillImage.fillMethod == Image.FillMethod.Horizontal
                && fillImage.fillOrigin == (int)Image.OriginHorizontal.Right;
            if (fromRight) spanStart = Vector3.Lerp(rightMid, leftMid, fill);
            else spanEnd = Vector3.Lerp(leftMid, rightMid, fill);
        }

        int count = Mathf.Clamp(Mathf.RoundToInt(burstParticles * strength), 1, maxBakedParticles);
        bool localSpace = particles.main.simulationSpace == ParticleSystemSimulationSpace.Local;
        Transform psTransform = particles.transform;
        var emit = new ParticleSystem.EmitParams();

        for (int i = 0; i < count; i++)
        {
            // 均匀分层 + 层内抖动：纯随机会出现明显的疏密团块，分层后才像"整条被同时点燃"。
            float t = (i + Random.value) / count;
            Vector3 world = Vector3.Lerp(spanStart, spanEnd, t)
                          + heightVector * ((Random.value - 0.5f) * verticalSpread);
            // Local 模拟时 EmitParams.position 是 ParticleSystem 的本地坐标，World 模拟时是世界坐标。
            emit.position = localSpace ? psTransform.InverseTransformPoint(world) : world;
            particles.Emit(emit, 1);
        }

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        lastBakedCount = 0;
        if (particles == null || particles.particleCount <= 0) return;

        int capacity = Mathf.Min(particles.main.maxParticles, maxBakedParticles);
        if (capacity <= 0) return;
        if (buffer == null || buffer.Length < capacity) buffer = new ParticleSystem.Particle[capacity];

        int count = particles.GetParticles(buffer, capacity);
        Transform psTransform = particles.transform;
        bool localSpace = particles.main.simulationSpace == ParticleSystemSimulationSpace.Local;
        Color32 tint = color;

        for (int i = 0; i < count; i++)
        {
            Vector3 world = localSpace ? psTransform.TransformPoint(buffer[i].position) : buffer[i].position;
            Vector3 center = rectTransform.InverseTransformPoint(world);

            Vector3 size = buffer[i].GetCurrentSize3D(particles);
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            if (halfWidth <= 0f || halfHeight <= 0f) continue;

            Color32 vertexColor = Multiply(buffer[i].GetCurrentColor(particles), tint);
            float radians = -buffer[i].rotation * Mathf.Deg2Rad;   // 粒子旋转为顺时针，UI 为逆时针
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);

            int firstVert = vh.currentVertCount;
            AddCorner(vh, center, -halfWidth, -halfHeight, cos, sin, vertexColor, 0f, 0f);
            AddCorner(vh, center, -halfWidth, halfHeight, cos, sin, vertexColor, 0f, 1f);
            AddCorner(vh, center, halfWidth, halfHeight, cos, sin, vertexColor, 1f, 1f);
            AddCorner(vh, center, halfWidth, -halfHeight, cos, sin, vertexColor, 1f, 0f);
            vh.AddTriangle(firstVert, firstVert + 1, firstVert + 2);
            vh.AddTriangle(firstVert + 2, firstVert + 3, firstVert);
        }

        lastBakedCount = count;
    }

    static void AddCorner(VertexHelper vh, Vector3 center, float x, float y, float cos, float sin, Color32 c, float u, float v)
    {
        vh.AddVert(new Vector3(center.x + x * cos - y * sin, center.y + x * sin + y * cos, 0f), c, new Vector2(u, v));
    }

    static Color32 Multiply(Color32 a, Color32 b)
    {
        return new Color32(
            (byte)(a.r * b.r / 255),
            (byte)(a.g * b.g / 255),
            (byte)(a.b * b.b / 255),
            (byte)(a.a * b.a / 255));
    }
}
