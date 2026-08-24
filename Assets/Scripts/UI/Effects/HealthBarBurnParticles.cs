using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 血条右端燃烧特效（附身状态下 Body 耐久自动流逝时燃烧）。
///
/// 为什么不能把 ParticleSystem 直接挂进血条：
///   UICanvas 是 Screen Space - Overlay，Overlay Canvas 在所有相机之后直接绘制到屏幕；
///   而 ParticleSystemRenderer 是普通世界空间 Renderer —— 既不参与 Canvas 批次，
///   位置又落在屏幕像素坐标（如 x=1900）处，实际结果是看不到。
///
/// 因此这里的做法：
///   * 仍然用真正的 ParticleSystem 做模拟（发射、寿命、颜色/大小曲线全在粒子模块上，美术可直接调）；
///   * 关掉它的 ParticleSystemRenderer，改由本组件每帧把粒子烘进 CanvasRenderer 的 UI 网格；
///   * 于是特效严格遵守 UI 层级、遮挡与 CanvasScaler 缩放，且与 Overlay Canvas 兼容。
///
/// 职责边界（重要）：
///   本组件只做三件事 —— 烘录粒子网格、跟随血条右端、按附身流逝状态开关发射。
///   火焰形态本身（速率 / 寿命 / 颜色 / 大小曲线）不在这里写死，全部在 ParticleSystem 上配置。
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasRenderer))]
public class HealthBarBurnParticles : MaskableGraphic
{
    [Header("Source")]
    [Tooltip("提供粒子模拟的 ParticleSystem（留空自动取子物体上的第一个）。其 Renderer 会被强制关闭。")]
    public ParticleSystem particles;
    [Tooltip("粒子贴图（建议 Art folder/UI/HealthBar_Flame.png）。留空则退化为白色方块。")]
    public Texture particleTexture;

    [Header("Placement")]
    [Tooltip("跟随的血条 Slider（留空自动取 PlayerHealth.healthSlider）。火苗跟随其填充断口。")]
    public Slider healthBar;
    [Tooltip("相对填充断口中点的偏移，单位为本物体父级的 UI 单位。")]
    public Vector2 offset = new Vector2(6f, 0f);

    [Header("Budget")]
    [Tooltip("单帧最多烘录的粒子数（每个粒子 4 顶点），防止 UI 网格膨胀。")]
    [Range(16, 1024)] public int maxBakedParticles = 256;

    private ParticleSystem.Particle[] buffer;
    private readonly Vector3[] fillCorners = new Vector3[4];
    private bool burning;
    private int lastBakedCount;

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
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        burning = false;
    }

    void LateUpdate()
    {
        ResolveRefs();
        FollowFillEdge();
        UpdateEmission();

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
    /// 贴到血条填充断口（血量 80% ⇒ 火苗在 80% 处）。
    ///
    /// 本项目血条的 Fill 是 Image.type = Filled，fillRect 的几何恒定不变 ——
    /// 断口只体现在 fillAmount 上，所以不能拿 fillRect 的世界右边界当断口，
    /// 必须用 fillRect 的左右边界按 fillAmount 插值。
    /// Sliced/Simple 型 Fill（靠 anchorMax 收缩）时 fillAmount 为 1，插值退化为右边界，同样正确。
    /// </summary>
    void FollowFillEdge()
    {
        if (healthBar == null || healthBar.fillRect == null) return;
        var parent = rectTransform.parent as RectTransform;
        if (parent == null) return;

        healthBar.fillRect.GetWorldCorners(fillCorners);   // 0=左下 1=左上 2=右上 3=右下
        Vector3 leftMid = (fillCorners[0] + fillCorners[1]) * 0.5f;
        Vector3 rightMid = (fillCorners[2] + fillCorners[3]) * 0.5f;

        var fillImage = healthBar.fillRect.GetComponent<Image>();
        float ratio = fillImage != null && fillImage.type == Image.Type.Filled
            ? fillImage.fillAmount
            : 1f;
        // fillOrigin 为 Right 时填充自右向左推进，断口在另一侧。
        bool fromRight = fillImage != null
            && fillImage.fillMethod == Image.FillMethod.Horizontal
            && fillImage.fillOrigin == (int)Image.OriginHorizontal.Right;

        Vector3 edge = fromRight
            ? Vector3.Lerp(rightMid, leftMid, ratio)
            : Vector3.Lerp(leftMid, rightMid, ratio);

        Vector3 local = parent.InverseTransformPoint(edge);
        rectTransform.localPosition = new Vector3(local.x + offset.x, local.y + offset.y, 0f);
    }

    /// <summary>只在「附身中 Body 耐久正在流逝」时发射；流逝停止后已存在的粒子自然燃尽。</summary>
    void UpdateEmission()
    {
        var possession = PossessionManager.Instance;
        bool shouldBurn = possession != null && possession.IsBodyDecaying;
        if (shouldBurn == burning) return;
        burning = shouldBurn;
        if (particles == null) return;
        if (shouldBurn) particles.Play(true);
        else particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
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
