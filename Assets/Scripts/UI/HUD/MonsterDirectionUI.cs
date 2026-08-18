using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物指引脉冲（找不到怪时的方向引导）：
/// 屏幕中连续 noMonsterVisibleSeconds 秒没有出现任何可交互怪物（且地图上仍有活怪）时，
/// 从玩家/附身怪脚底朝目标怪物发出一道脉冲：一条光带沿蛇形路径从脚底推进到怪物脚底，
/// 停留片刻后从发出端向终点逐段消散，冷却后循环发出下一道。
///
/// 目标锁定：锁定目标后，只要它未死亡/未被附身/未被回收，且屏幕中未出现别的怪物，
/// 就持续朝同一只怪发脉冲——不做"最近怪"重算，避免两只怪分居两侧时引导来回跳转。
///
/// 美术资源：引导线 Prefab（linePrefab，Root 挂 LineRenderer + 拖尾同款 Material 资产，
/// 子对象 PulseHead 挂 ParticleSystem 脉冲头光尘——与项目 VFX 规范一致：Root + 子粒子层），
/// 所有样式参数直接在本组件检查器设置（无需额外配置资产）。
///
/// 脉冲锚点：followPlayerAfterFire=false（默认）时，脉冲发出瞬间锁定起点（玩家当时脚下）
/// 与终点（怪物当时位置），推进/停留/消散全程不再跟随；true 时起点实时跟随玩家（终点仍锁定）。
///
/// 数据源：MonsterSpawner.CollectAliveMonsters（在场活怪：未附身、未倒地）。
/// </summary>
public class MonsterDirectionUI : MonoBehaviour
{
    [Header("引导线资源")]
    [Tooltip("引导线 Prefab 资产（Assets/Prefabs/VFX/MonsterGuideLine.prefab）：Root 挂 LineRenderer + 拖尾同款材质，子对象 PulseHead 为脉冲头光尘粒子；为空时运行时动态创建。")]
    public GameObject linePrefab;

    [Header("触发条件")]
    [Tooltip("屏幕中连续无怪物可见的秒数超过该值后发出第一道引导脉冲（秒）。")]
    [Min(0.5f)] public float noMonsterVisibleSeconds = 5f;

    [Header("脉冲节奏")]
    [Tooltip("脉冲从脚底推进到怪物脚底的耗时（秒），值越大推进越从容。")]
    [Min(0.05f)] public float pulseTravelTime = 0.5f;
    [Tooltip("脉冲到达怪物后停留的时间（秒），用于看清方向。")]
    [Min(0f)] public float pulseHoldTime = 0.5f;
    [Tooltip("消散时长（秒）：脉冲到达后从发出端（玩家脚下）朝终点（怪物）方向逐段熄灭。")]
    [Min(0.05f)] public float pulseFadeTime = 1f;
    [Tooltip("两道脉冲之间的冷却间隔（秒），值越大引导节奏越舒缓。")]
    [Min(0f)] public float pulseCooldown = 5f;

    [Header("脉冲行为")]
    [Tooltip("脉冲发出后起点是否跟随玩家：false = 锁定发出瞬间的起点（玩家当时脚下），推进/停留/消散全程不跟随；true = 起点实时跟随玩家位置（终点始终锁定发出时的怪物位置）。")]
    public bool followPlayerAfterFire = false;

    [Header("光效")]
    [Tooltip("引导线颜色（HDR：RGB >1 更亮，与拖尾 _Color0 同语义）。")]
    public Color guideColor = new Color(3.8f, 4.05f, 4.6f, 0.85f);
    [Tooltip("整体亮度乘数：1 = 玩家拖尾原亮度。")]
    [Range(0.1f, 3f)] public float brightness = 0.75f;
    [Tooltip("若隐若现：呼吸明暗幅度（0 = 恒定亮度，越大呼吸越明显）。")]
    [Range(0f, 1f)] public float breatheAmount = 0.35f;
    [Tooltip("呼吸频率（每秒明暗周期数）。")]
    [Min(0.1f)] public float breatheSpeed = 2f;

    [Header("宽度（世界单位）")]
    [Tooltip("宽度曲线（彗星式引导脉冲）：根部细融于地 → 中段饱满 → 头部锐利收尖，×widthMultiplier。运行时以此处为准（覆盖 Prefab 中的宽度）。")]
    public AnimationCurve widthCurve = new AnimationCurve(
        new Keyframe(0.025f, 0.0526f),
        new Keyframe(0.5f, 0.55f),
        new Keyframe(0.88f, 0.06f),
        new Keyframe(1f, 0f));
    [Tooltip("宽度倍率。运行时以此处为准（覆盖 Prefab 中的宽度）。")]
    [Min(0.01f)] public float widthMultiplier = 3f;
    [Tooltip("线贴地高度（米）：0 = 脚底。")]
    public float heightOffset = 0.05f;

    [Header("蛇形波动")]
    [Tooltip("波动幅度（米）：弯曲程度，越大越明显。")]
    [Min(0f)] public float waveAmplitude = 0.2f;
    [Tooltip("波动波数：完整波周期个数，越大越密。")]
    [Min(0.5f)] public float waveCount = 3f;
    [Tooltip("波动相位滚动速度：波形扭动快慢。")]
    public float waveSpeed = 1f;

    // 实现细节常量（无检查器配置价值）
    const float kCollectInterval = 0.25f; // 活怪收集刷新间隔（秒）
    const float kViewportMargin = 0.03f;  // 视口边缘容差：怪物进入该范围内即视为"已出现"（防边缘闪烁）
    const int kSegments = 28;             // 蛇形路径分段数（越大越平滑）

    /// <summary>是否正在显示引导（脉冲推进/停留/消散中）。</summary>
    public bool IsShowing { get; private set; }
    /// <summary>当前锁定的引导目标（无锁定为 null）。</summary>
    public MonsterActor LockedTarget { get; private set; }

    LineRenderer line;
    Material runtimeMat;
    ParticleSystem[] pulseParticles;   // 脉冲头光尘（Prefab 子粒子系统，项目 VFX 规范：Root + 子粒子层）
    Camera mainCamera;
    Transform player;
    readonly List<MonsterActor> aliveMonsters = new List<MonsterActor>();
    readonly Vector3[] pathPoints = new Vector3[64];
    float nextCollectTime;
    float noMonsterTimer;
    float wavePhase;
    float pulseTime;
    Vector3 pulseOrigin;       // 当前脉冲发出瞬间锁定的起点（玩家当时脚下）
    Vector3 pulseTargetPos;    // 当前脉冲发出瞬间锁定的终点（怪物当时位置）
    bool anchorsReady;         // 脉冲锚点是否已采样（首道与每道新脉冲开始时采样）

    void Awake()
    {
        BuildLine();
        if (line != null) line.gameObject.SetActive(false);
    }

    // 检查器修改（含 Play 模式）时实时同步宽度到运行中的引导线，
    // 避免"调了宽度看不到变化"（BuildLine 仅在 Awake 执行一次）
    void OnValidate()
    {
        if (line != null)
        {
            line.widthMultiplier = widthMultiplier;
            line.widthCurve = widthCurve;
        }
    }

    void Update()
    {
        var spawner = MonsterSpawner.Instance;
        if (spawner == null)
        {
            Hide();
            return;
        }

        // 低频收集在场活怪
        if (Time.time >= nextCollectTime)
        {
            spawner.CollectAliveMonsters(aliveMonsters);
            nextCollectTime = Time.time + kCollectInterval;
        }

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Hide();
            return;
        }
        if (player == null && PlayerController.Instance != null)
            player = PlayerController.Instance.transform;

        // 起点：附身怪物时用被附身怪物脚底（玩家灵魂已脱离原身体），否则用玩家位置
        Vector3 origin;
        var pm = PossessionManager.Instance;
        if (pm != null && pm.CurrentBody != null)
            origin = pm.CurrentBody.transform.position;
        else if (player != null)
            origin = player.position;
        else
            origin = mainCamera.transform.position;

        // 锁定目标有效性：死亡 / 被附身 / 被回收 → 解锁（下次立即换锁最近的）
        if (LockedTarget != null && !IsTargetValid(LockedTarget))
        {
            LockedTarget = null;
            anchorsReady = false;
            pulseTime = 0f;
        }

        // 1) 屏幕中是否有怪物可见
        bool anyInView = false;
        MonsterActor nearest = null;
        float nearestSqr = float.MaxValue;
        for (int i = 0; i < aliveMonsters.Count; i++)
        {
            var m = aliveMonsters[i];
            if (m == null) continue;
            Vector3 vp = mainCamera.WorldToViewportPoint(m.transform.position);
            if (vp.z > 0f && vp.x >= kViewportMargin && vp.x <= 1f - kViewportMargin
                && vp.y >= kViewportMargin && vp.y <= 1f - kViewportMargin)
            {
                anyInView = true;
                break;
            }
            if (LockedTarget != null) continue; // 已锁定：不再重算最近怪（防来回跳转）
            float sqr = (m.transform.position - origin).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = m;
            }
        }

        // 2) 有怪可见 → 隐藏并重置计时、解锁
        if (anyInView)
        {
            noMonsterTimer = 0f;
            LockedTarget = null;
            anchorsReady = false;
            Hide();
            return;
        }

        // 3) 地图上无活怪 → 隐藏并重置计时、解锁
        if (aliveMonsters.Count == 0)
        {
            noMonsterTimer = 0f;
            LockedTarget = null;
            anchorsReady = false;
            Hide();
            return;
        }

        // 4) 无锁定：累计"无怪可见"时间，超时后锁定最近的怪并立即发第一道脉冲
        if (LockedTarget == null)
        {
            noMonsterTimer += Time.deltaTime;
            if (noMonsterTimer >= noMonsterVisibleSeconds && nearest != null)
            {
                LockedTarget = nearest;
                pulseTime = 0f;
                Debug.Log($"[MonsterDirectionUI] 锁定引导目标：{nearest.gameObject.name}@{nearest.transform.position}");
            }
            else
            {
                Hide();
                return;
            }
        }

        // 5) 已锁定目标：脉冲循环（推进 → 停留 → 消散 → 冷却 → 循环）
        float oldPulseTime = pulseTime;
        pulseTime += Time.deltaTime;
        float cycle = pulseTravelTime + pulseHoldTime + pulseFadeTime + pulseCooldown;
        if (pulseTime >= cycle) pulseTime -= cycle;

        // 脉冲锚点采样：首道（anchorsReady=false）或每道新脉冲开始时（回绕：old > new）
        // 锁定"发出瞬间"的起点（玩家当时脚下）与终点（怪物当时位置）。
        // followPlayerAfterFire=true 时起点每帧实时更新（跟随玩家），终点始终锁定。
        if (!anchorsReady || oldPulseTime > pulseTime)
        {
            pulseOrigin = origin;
            pulseTargetPos = LockedTarget.transform.position;
            anchorsReady = true;
        }
        else if (followPlayerAfterFire)
        {
            pulseOrigin = origin; // 起点实时跟随玩家/附身怪
        }

        if (pulseTime < pulseTravelTime)
        {
            // 推进阶段：光带从发出时脚底向发出时怪物位置推进（锚点已锁定）
            float progress = Mathf.Clamp01(pulseTime / pulseTravelTime);
            ShowPulse(0f, progress);
        }
        else if (pulseTime < pulseTravelTime + pulseHoldTime)
        {
            // 到达后停留：全亮（仍用锚点位置，不跟随怪物）
            ShowPulse(0f, 1f);
        }
        else if (pulseTime < pulseTravelTime + pulseHoldTime + pulseFadeTime)
        {
            // 消散阶段：从发出端（玩家端 t=0）朝终点（怪物端 t=1）方向逐段熄灭——
            // 起点端先消失，残余光带缩短向怪物端收拢，最后整条熄灭。
            float fade = Mathf.Clamp01((pulseTime - pulseTravelTime - pulseHoldTime) / pulseFadeTime);
            ShowPulse(fade, 1f);
        }
        else
        {
            // 冷却：隐藏，等下一道
            Hide();
        }
    }

    bool IsTargetValid(MonsterActor m)
    {
        return m != null && m.gameObject.activeInHierarchy && !m.isDowned && !m.isPossessed;
    }

    /// <summary>
    /// 显示脉冲的一段：蛇形路径从锁定的发出位置（pulseOrigin）到锁定的目标位置（pulseTargetPos），
    /// 只渲染 [startT, endT] 区间（路径参数 t ∈ [0,1]，0=玩家端，1=怪物端）。
    /// 推进阶段 endT=progress（头部收尖）；停留 endT=1 全亮；消散阶段 startT=fade
    /// （起点端先熄灭，残余向怪物端收拢）——实现"从发出端朝终点消散"。
    /// 锚点在脉冲发出瞬间采样，推进/停留/消散全程不跟随玩家/怪物移动。
    /// 若隐若现：呼吸正弦调制透明度（breatheAmount 控制明暗幅度）。
    /// </summary>
    void ShowPulse(float startT, float endT)
    {
        if (line == null) return;
        startT = Mathf.Clamp01(startT);
        endT = Mathf.Clamp01(endT);
        if (endT <= startT)
        {
            Hide();
            return;
        }
        IsShowing = true;
        line.gameObject.SetActive(true);

        // 能量抵达感：停留/消散阶段全亮（endT==1 时 advance 最大）；叠加柔和呼吸
        if (runtimeMat != null)
        {
            float advance = 0.55f + 0.45f * endT;
            float breathe = 1f;
            if (breatheAmount > 0f)
                breathe = 1f + Mathf.Sin(Time.time * breatheSpeed * 2f * Mathf.PI) * breatheAmount * 0.5f;
            runtimeMat.SetColor("_Color0", guideColor * brightness * advance * breathe);
        }

        Vector3 a = pulseOrigin + Vector3.up * heightOffset;
        Vector3 b = pulseTargetPos + Vector3.up * heightOffset;

        // 蛇形波动路径（全量生成，之后按 [startT, endT] 截取）
        int n = kSegments;
        Vector3 dir = b - a;
        float len = dir.magnitude;
        if (len < 0.01f)
        {
            dir = Vector3.forward;
            len = 1f;
        }
        Vector3 flat = dir;
        flat.y = 0f;
        if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
        flat.Normalize();
        Vector3 normal = Vector3.Cross(flat, Vector3.up).normalized; // 水平面内法线

        wavePhase += Time.deltaTime * waveSpeed;

        for (int i = 0; i <= n; i++)
        {
            float t = (float)i / n;
            Vector3 basePos = Vector3.Lerp(a, b, t);
            float envelope = Mathf.Sin(Mathf.PI * t); // 两端 0、中间最大
            float wave = Mathf.Sin(t * waveCount * 2f * Mathf.PI + wavePhase);
            pathPoints[i] = basePos + normal * (wave * envelope * waveAmplitude);
        }

        // 截取 [startT, endT] 区间：头部（endT 处）由宽度曲线自然收尖，
        // 起点端（startT 处）同样收尖 → 消散时从玩家端平滑熄灭
        int segCount = Mathf.Max(1, n);
        int first = Mathf.FloorToInt(startT * segCount);
        int last = Mathf.CeilToInt(endT * segCount);
        int visibleCount = Mathf.Max(2, last - first + 1);
        line.positionCount = visibleCount;
        for (int i = 0; i < visibleCount; i++)
            line.SetPosition(i, pathPoints[first + i]);

        // 脉冲头粒子：推进/停留阶段发射于脉冲尖端（每帧跟随推进位置），
        // 消散阶段停止发射（已发射光尘自然飘散，与引导线"从发出端消散"同步）。
        if (pulseParticles != null && pulseParticles.Length > 0)
        {
            if (startT > 0f)
            {
                StopPulseVfx();
            }
            else
            {
                Vector3 headPos = GetPulseHeadPosition(pathPoints, n, endT);
                for (int i = 0; i < pulseParticles.Length; i++)
                {
                    if (pulseParticles[i] != null) pulseParticles[i].transform.position = headPos;
                }
                PlayPulseVfx();
            }
        }
    }

    void Hide()
    {
        if (IsShowing)
        {
            IsShowing = false;
            if (line != null) line.gameObject.SetActive(false);
        }
        // 隐藏时清空脉冲头粒子，防循环粒子残留（项目 VFX 规范同款处理）
        if (pulseParticles != null) StopPulseVfx(true);
    }

    /// <summary>
    /// 构建引导线：优先 Instantiate 美术 Prefab 资产（渲染参数如对齐/阴影等在 Prefab 中配置，
    /// 美术可直接编辑资产）；为空时回退运行时动态创建。
    /// 材质取 Prefab 自带，运行时克隆实例应用动态参数（不影响资产与玩家拖尾）。
    /// </summary>
    void BuildLine()
    {
        GameObject go;
        if (linePrefab != null)
        {
            go = Instantiate(linePrefab, transform, false);
            go.name = "MonsterGuideLine";
            line = go.GetComponent<LineRenderer>();
        }
        else
        {
            go = new GameObject("MonsterGuideLine");
            go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.TransformZ; // 世界单位宽度（拖尾同款；View 是像素语义，会失真）
        }

        // 动态参数来自检查器（其余渲染参数由美术在 Prefab 中配置）
        line.widthMultiplier = widthMultiplier;
        line.widthCurve = widthCurve;

        // 材质模板：Prefab 自带 → URP 粒子 Unlit 兜底（仅动态创建路径无材质时）
        Material template = line.sharedMaterial;
        if (template != null)
        {
            runtimeMat = new Material(template);
            // ASE 主色属性 _Color0（HDR 语义）
            runtimeMat.SetColor("_Color0", guideColor * brightness);
            // 覆盖常见颜色属性，确保克隆实例不被其它属性带偏
            runtimeMat.SetColor("_BaseColor", Color.white);
            runtimeMat.SetColor("_Color", Color.white);
            line.sharedMaterial = runtimeMat;
        }
        else
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader != null)
            {
                runtimeMat = new Material(shader);
                runtimeMat.SetColor("_BaseColor", guideColor * brightness);
                line.sharedMaterial = runtimeMat;
            }
            else
            {
                Debug.LogWarning("[MonsterDirectionUI] 未配置引导线材质且找不到兜底 shader，引导线不可见。");
            }
        }

        // 脉冲头粒子：Prefab 子粒子系统（项目 VFX 规范：Root + 子粒子层），
        // 渲染材质复用同一克隆实例，颜色/呼吸与引导线同步。
        pulseParticles = go.GetComponentsInChildren<ParticleSystem>(true);
        if (runtimeMat != null)
        {
            foreach (var ps in pulseParticles)
            {
                if (ps == null) continue;
                var psr = ps.GetComponent<ParticleSystemRenderer>();
                if (psr != null) psr.sharedMaterial = runtimeMat;
            }
        }
    }

    /// <summary>
    /// 播放脉冲头粒子（对齐项目 VFX 规范：EnemyAbility.PlayVfx 遍历子粒子系统 Play(true)）。
    /// </summary>
    void PlayPulseVfx()
    {
        for (int i = 0; i < pulseParticles.Length; i++)
        {
            var ps = pulseParticles[i];
            if (ps != null && !ps.isPlaying) ps.Play(true);
        }
    }

    /// <summary>
    /// 停止脉冲头粒子发射：默认保留已发射粒子自然消散（与引导线"从发出端消散"同步）；
    /// clear=true 立即清空（隐藏时不留残留，防循环粒子残留——项目 StopVfxLooping 同目的）。
    /// </summary>
    void StopPulseVfx(bool clear = false)
    {
        for (int i = 0; i < pulseParticles.Length; i++)
        {
            var ps = pulseParticles[i];
            if (ps == null) continue;
            if (clear) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            else if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    /// <summary>路径参数 t 处的世界位置（pathPoints 线性插值）。</summary>
    Vector3 GetPulseHeadPosition(Vector3[] pts, int n, float endT)
    {
        float f = Mathf.Clamp01(endT) * n;
        int i0 = Mathf.FloorToInt(f);
        int i1 = Mathf.Min(i0 + 1, n);
        return Vector3.Lerp(pts[i0], pts[i1], f - i0);
    }

    void OnDestroy()
    {
        if (runtimeMat != null) Destroy(runtimeMat);
    }
}
