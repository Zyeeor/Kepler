using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 方向引导脉冲：
/// Monsters 模式保留原有逻辑——玩家处于附身态时，屏幕连续 noMonsterVisibleSeconds 秒没有活怪可见，
/// 从当前控制身体朝屏幕外最近活怪发出引导；自由灵魂态时该模式完全停用。
///
/// Shrines 模式是自由灵魂态资源引导：屏幕中没有可附身躯体时，若场上仍有躯体则引导到最近躯体，
/// 若场上没有可附身躯体则引导到最近有效神龛。两种模式由场景中的两个组件分工，但通过状态门控互斥。
///
/// 美术资源：两种模式都复用同一个引导线 Prefab（Root 挂 LineRenderer + 拖尾同款 Material，
/// 子对象 PulseHead 挂 ParticleSystem 脉冲头光尘），所有样式参数直接在本组件检查器设置。
/// </summary>
public class MonsterDirectionUI : MonoBehaviour
{
    public enum GuideTargetMode
    {
        Monsters = 0,
        Shrines = 1,
        Elites = 2,
    }

    [Header("引导目标")]
    [Tooltip("Monsters = 非灵魂态时，视野内没有活怪则引导到最近活怪；Shrines = 灵魂态时，视野内没有可附身躯体则优先引导躯体，没有躯体才引导神龛；Elites = 精英生成后若在视野外则引导（每只只引导一次，引导期间普通怪引导让路）。")]
    public GuideTargetMode guideMode = GuideTargetMode.Monsters;

    [Header("引导线资源")]
    [Tooltip("引导线 Prefab 资产（Assets/Prefabs/VFX/MonsterGuideLine.prefab）：Root 挂 LineRenderer + 拖尾同款材质，子对象 PulseHead 为脉冲头光尘粒子；为空时运行时动态创建。")]
    public GameObject linePrefab;

    [Header("触发条件")]
    [Tooltip("怪物模式：视野内连续无活怪的秒数；神龛模式：灵魂态且视野内连续无可附身躯体的秒数。")]
    [Min(0.5f)] public float noMonsterVisibleSeconds = 5f;
    [Tooltip("仅神龛模式使用：灵魂态且视野内没有可附身躯体后，持续达到该秒数才开始引导；场上有躯体时引导躯体，否则引导有效神龛。")]
    [Min(0.5f)] public float noPossessableMonsterSeconds = 5f;

    [Header("脉冲节奏")]
    [Tooltip("脉冲从脚底推进到怪物脚底的耗时（秒），值越大推进越从容。")]
    [Min(0.05f)] public float pulseTravelTime = 0.5f;
    [Tooltip("脉冲到达怪物后停留的时间（秒），用于看清方向。")]
    [Min(0f)] public float pulseHoldTime = 0.5f;
    [Tooltip("消散时长（秒）：脉冲到达后从发出端（玩家脚下）朝终点（怪物）方向逐段熄灭。")]
    [Min(0.05f)] public float pulseFadeTime = 1f;
    [Tooltip("两道脉冲之间的冷却间隔（秒），值越大引导节奏越舒缓。")]
    [Min(0f)] public float pulseCooldown = 5f;

    [Header("常驻引导")]
    [Tooltip("true：锁定目标在视野外时引导线持续显示（不进入脉冲冷却），目标进入视野后按 pulseFadeTime 淡出；false：保留脉冲循环（推进→停留→消散→冷却）。")]
    public bool persistentGuide = true;

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
    /// <summary>当前锁定的怪物引导目标（无锁定为 null）。</summary>
    public MonsterActor LockedTarget { get; private set; }
    /// <summary>当前锁定的神龛引导目标（神龛模式，无锁定为 null）。</summary>
    public PossessionBodyProvider LockedShrine => lockedShrine;

    LineRenderer line;
    Material runtimeMat;
    ParticleSystem[] pulseParticles;   // 脉冲头光尘（Prefab 子粒子系统，项目 VFX 规范：Root + 子粒子层）
    Camera mainCamera;
    Transform player;
    readonly List<MonsterActor> aliveMonsters = new List<MonsterActor>();
    readonly List<MonsterActor> possessableBodies = new List<MonsterActor>();
    readonly List<PossessionBodyProvider> activeShrines = new List<PossessionBodyProvider>();
    readonly Vector3[] pathPoints = new Vector3[64];
    SoulActor soulActor;
    MonsterActor lockedBody;
    PossessionBodyProvider lockedShrine;
    float nextCollectTime;
    float nextShrineCollectTime;
    float noMonsterTimer;
    float noPossessableMonsterTimer;
    float wavePhase;
    float pulseTime;
    float guideFadeOut;         // 常驻引导淡出进度 0→1（0=全亮，1=全隐）
    Vector3 pulseOrigin;       // 当前脉冲发出瞬间锁定的起点（玩家当时脚下）
    Vector3 pulseTargetPos;    // 当前脉冲发出瞬间锁定的终点（怪物当时位置）
    bool anchorsReady;         // 脉冲锚点是否已采样（首道与每道新脉冲开始时采样）
    readonly HashSet<MonsterActor> pendingElites = new HashSet<MonsterActor>();   // 已生成但尚未引导过的精英
    readonly HashSet<MonsterActor> guidedElites = new HashSet<MonsterActor>();    // 已引导过的精英（每只只引导一次）
    bool eliteSubscribed;

    void Awake()
    {
        BuildLine();
        if (line != null) line.gameObject.SetActive(false);
        if (guideMode == GuideTargetMode.Elites)
            SubscribeEliteSpawn();
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
        wavePhase += Time.deltaTime * waveSpeed;   // 波动相位每帧推进一次（普通/精英两条引导线共用）

        if (guideMode == GuideTargetMode.Shrines)
        {
            UpdateShrineGuide();
            return;
        }
        if (guideMode == GuideTargetMode.Elites)
        {
            UpdateEliteGuide();
            return;
        }

        UpdateMonsterGuide();
    }

    void UpdateMonsterGuide()
    {
        // 自由灵魂态由 Shrine 模式负责引导躯体/神龛，怪物模式让路，确保两套引导互斥。
        if (IsFreeSoulState())
        {
            ResetMonsterGuide();
            return;
        }

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

        // 2) 有怪可见 → 隐藏并重置计时、解锁。
        // 常驻引导下仅在"未锁定"时据此停止引导；已锁定交由下方按"锁定目标是否进视野"处理。
        if (anyInView && (!persistentGuide || LockedTarget == null))
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

        // 5) 已锁定目标：显示（常驻/脉冲由 UpdateGuideDisplay 统一处理）
        UpdateGuideDisplay(origin, LockedTarget.transform.position, null);
    }

    void UpdateShrineGuide()
    {
        // 自由灵魂态只处理“躯体 → 神龛”资源引导；附身/过渡态由怪物模式独立处理。
        if (!IsFreeSoulState())
        {
            ResetShrineGuide();
            return;
        }

        if (Time.time >= nextShrineCollectTime)
        {
            var spawner = MonsterSpawner.Instance;
            if (spawner != null)
                spawner.CollectPossessableMonsters(possessableBodies);
            else
                CollectPossessableMonstersFallback();
            PossessionBodyProvider.CollectActiveProviders(activeShrines);
            nextShrineCollectTime = Time.time + kCollectInterval;
        }

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null)
        {
            ResetShrineGuide();
            return;
        }

        // 触发条件改为“视野内没有可附身躯体”，不再要求场上完全没有躯体。
        // 常驻引导下仅在"未锁定"时据此停止引导；已锁定交由下方按"锁定目标是否进视野"处理。
        for (int i = 0; i < possessableBodies.Count; i++)
        {
            if (IsPossessableBodyVisible(possessableBodies[i])
                && (!persistentGuide || (lockedBody == null && lockedShrine == null)))
            {
                ResetShrineGuide();
                return;
            }
        }

        if (lockedBody != null && !IsPossessableBodyValid(lockedBody))
        {
            lockedBody = null;
            anchorsReady = false;
            pulseTime = 0f;
        }
        if (lockedShrine != null && !lockedShrine.IsValidForGuide)
        {
            lockedShrine = null;
            anchorsReady = false;
            pulseTime = 0f;
        }

        // 场上存在可附身躯体时，躯体优先级高于神龛；若躯体后来消失，再切换到神龛。
        if (possessableBodies.Count > 0)
        {
            if (lockedShrine != null)
            {
                lockedShrine = null;
                anchorsReady = false;
                pulseTime = 0f;
            }

            if (lockedBody == null)
            {
                noPossessableMonsterTimer += Time.deltaTime;
                if (noPossessableMonsterTimer < Mathf.Max(0.01f, noPossessableMonsterSeconds))
                {
                    Hide();
                    return;
                }

                lockedBody = FindNearestPossessableBody();
                if (lockedBody == null)
                {
                    Hide();
                    return;
                }
                pulseTime = 0f;
                anchorsReady = false;
                Debug.Log($"[SoulDirectionUI] 锁定躯体引导目标：{lockedBody.name}@{lockedBody.transform.position}", lockedBody);
            }
        }
        else
        {
            if (lockedBody != null)
            {
                lockedBody = null;
                anchorsReady = false;
                pulseTime = 0f;
            }

            if (lockedShrine == null)
            {
                PossessionBodyProvider nearest = FindNearestValidShrine();
                if (nearest == null)
                {
                    noPossessableMonsterTimer = 0f;
                    Hide();
                    return;
                }

                noPossessableMonsterTimer += Time.deltaTime;
                if (noPossessableMonsterTimer < Mathf.Max(0.01f, noPossessableMonsterSeconds))
                {
                    Hide();
                    return;
                }

                lockedShrine = nearest;
                pulseTime = 0f;
                anchorsReady = false;
                Debug.Log($"[SoulDirectionUI] 锁定神龛引导目标：{nearest.name}@{nearest.transform.position}", nearest);
            }
        }

        Vector3 origin = GetGuideOrigin();

        // 已锁定目标：显示（常驻/脉冲统一处理；淡出完成后清空躯体/神龛锁定）
        Vector3 targetPos = lockedBody != null
            ? lockedBody.transform.position
            : lockedShrine.GuideAnchorPosition;
        UpdateGuideDisplay(origin, targetPos, () =>
        {
            lockedBody = null;
            lockedShrine = null;
        });
    }

    /// <summary>
    /// 已锁定目标（活怪/躯体/神龛/精英）的引导显示：
    /// persistentGuide=true 走常驻（目标视野外持续全亮，进视野按 pulseFadeTime 整体淡出后回调）；
    /// false 走脉冲循环。origin=引导起点，targetPos=引导终点，onFadeComplete=常驻淡出完成回调
    /// （在 LockedTarget 清空前调用，供 Shrines 清躯体/神龛锁定、Elites 标记已引导）。
    /// </summary>
    void UpdateGuideDisplay(Vector3 origin, Vector3 targetPos, System.Action onFadeComplete)
    {
        if (persistentGuide)
        {
            pulseOrigin = origin;
            pulseTargetPos = targetPos;
            anchorsReady = true;

            if (IsInViewport(targetPos))
            {
                guideFadeOut += Time.deltaTime;
                float fade = Mathf.Clamp01(guideFadeOut / Mathf.Max(0.05f, pulseFadeTime));
                ShowPulse(0f, 1f, fade);
                if (guideFadeOut >= Mathf.Max(0.05f, pulseFadeTime))
                {
                    guideFadeOut = 0f;
                    noMonsterTimer = 0f;
                    noPossessableMonsterTimer = 0f;
                    anchorsReady = false;
                    onFadeComplete?.Invoke();
                    LockedTarget = null;
                    Hide();
                }
            }
            else
            {
                guideFadeOut = 0f;
                ShowPulse(0f, 1f);
            }
        }
        else
        {
            float oldPulseTime = pulseTime;
            pulseTime += Time.deltaTime;
            float cycle = pulseTravelTime + pulseHoldTime + pulseFadeTime + pulseCooldown;
            if (pulseTime >= cycle) pulseTime -= cycle;

            if (!anchorsReady || oldPulseTime > pulseTime)
            {
                pulseOrigin = origin;
                pulseTargetPos = targetPos;
                anchorsReady = true;
            }
            else if (followPlayerAfterFire)
            {
                pulseOrigin = origin;
            }

            if (pulseTime < pulseTravelTime)
            {
                ShowPulse(0f, Mathf.Clamp01(pulseTime / pulseTravelTime));
            }
            else if (pulseTime < pulseTravelTime + pulseHoldTime)
            {
                ShowPulse(0f, 1f);
            }
            else if (pulseTime < pulseTravelTime + pulseHoldTime + pulseFadeTime)
            {
                float fade = Mathf.Clamp01((pulseTime - pulseTravelTime - pulseHoldTime) / pulseFadeTime);
                ShowPulse(fade, 1f);
            }
            else
            {
                Hide();
            }
        }
    }

    // ── 精英引导（guideMode = Elites）──

    void SubscribeEliteSpawn()
    {
        if (eliteSubscribed) return;
        var director = EliteBuildDirector.Instance;
        if (director == null) return;
        director.OnEliteSpawned += HandleEliteSpawned;
        eliteSubscribed = true;
    }

    void UnsubscribeEliteSpawn()
    {
        if (!eliteSubscribed) return;
        var director = EliteBuildDirector.Instance;
        if (director != null) director.OnEliteSpawned -= HandleEliteSpawned;
        eliteSubscribed = false;
    }

    void HandleEliteSpawned(MonsterActor elite)
    {
        if (elite == null) return;
        if (guidedElites.Contains(elite)) return;
        pendingElites.Add(elite);
    }

    /// <summary>
    /// 精英引导：精英生成（EliteBuildDirector.OnEliteSpawned）后，若其在视野外则引导；
    /// 每只精英只引导一次。独立于怪物/神龛引导，不参与灵魂态/附身态互斥（可与其它引导线同时显示）。
    /// </summary>
    void UpdateEliteGuide()
    {
        if (!eliteSubscribed) SubscribeEliteSpawn();

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null)
        {
            ResetEliteGuide();
            return;
        }
        if (player == null && PlayerController.Instance != null)
            player = PlayerController.Instance.transform;

        // 清理失效的待引导精英（死亡/被附身/销毁）
        pendingElites.RemoveWhere(m => !IsTargetValid(m));

        // 已锁定精英失效 → 标记已引导并解锁
        if (LockedTarget != null && !IsTargetValid(LockedTarget))
        {
            guidedElites.Add(LockedTarget);
            LockedTarget = null;
            anchorsReady = false;
            pulseTime = 0f;
        }

        // 未锁定：从待引导精英里挑一个视野外的引导
        if (LockedTarget == null)
        {
            MonsterActor candidate = null;
            foreach (var m in pendingElites)
            {
                if (IsInViewport(m.transform.position)) continue; // 视野内：玩家看得到，不引导
                candidate = m;
                break;
            }

            if (candidate != null)
            {
                pendingElites.Remove(candidate);
                LockedTarget = candidate;
                pulseTime = 0f;
                anchorsReady = false;
                Debug.Log($"[MonsterDirectionUI] 锁定精英引导目标：{candidate.gameObject.name}@{candidate.transform.position}");
            }
            else
            {
                Hide();
                return;
            }
        }

        // 已锁定：起点同怪物引导（附身怪脚底 / 玩家位置）
        Vector3 origin;
        var pm = PossessionManager.Instance;
        if (pm != null && pm.CurrentBody != null)
            origin = pm.CurrentBody.transform.position;
        else if (player != null)
            origin = player.position;
        else
            origin = mainCamera.transform.position;

        UpdateGuideDisplay(origin, LockedTarget.transform.position, () =>
        {
            if (LockedTarget != null) guidedElites.Add(LockedTarget);
        });
    }

    void ResetEliteGuide()
    {
        LockedTarget = null;
        anchorsReady = false;
        pulseTime = 0f;
        Hide();
    }

    bool IsFreeSoulState()
    {
        if (soulActor == null) soulActor = FindObjectOfType<SoulActor>();
        if (soulActor == null || !soulActor.gameObject.activeInHierarchy
            || soulActor.IsSuppressed || soulActor.IsInPossessionFlight)
            return false;

        PossessionManager manager = PossessionManager.Instance;
        if (manager != null && (manager.CurrentBody != null
            || manager.State != PossessionManager.SwitchState.Idle))
            return false;
        return true;
    }

    void ResetMonsterGuide()
    {
        noMonsterTimer = 0f;
        LockedTarget = null;
        anchorsReady = false;
        pulseTime = 0f;
        Hide();
    }

    void CollectPossessableMonstersFallback()
    {
        possessableBodies.Clear();
        MonsterActor[] monsters = FindObjectsOfType<MonsterActor>(true);
        for (int i = 0; i < monsters.Length; i++)
        {
            MonsterActor monster = monsters[i];
            if (IsPossessableBodyValid(monster))
                possessableBodies.Add(monster);
        }
    }

    bool IsPossessableBodyValid(MonsterActor body)
    {
        return body != null && body.gameObject.activeInHierarchy && body.CanBePossessed;
    }

    bool IsPossessableBodyVisible(MonsterActor body)
    {
        if (!IsPossessableBodyValid(body) || mainCamera == null) return false;
        Vector3 vp = mainCamera.WorldToViewportPoint(body.transform.position);
        return vp.z > 0f && vp.x >= kViewportMargin && vp.x <= 1f - kViewportMargin
            && vp.y >= kViewportMargin && vp.y <= 1f - kViewportMargin;
    }

    /// <summary>世界坐标是否落入当前镜头视口（带边缘容差，防边缘闪烁）。</summary>
    bool IsInViewport(Vector3 worldPos)
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return false;
        Vector3 vp = mainCamera.WorldToViewportPoint(worldPos);
        return vp.z > 0f && vp.x >= kViewportMargin && vp.x <= 1f - kViewportMargin
            && vp.y >= kViewportMargin && vp.y <= 1f - kViewportMargin;
    }

    MonsterActor FindNearestPossessableBody()
    {
        Vector3 origin = GetGuideOrigin();
        MonsterActor nearest = null;
        float nearestSqr = float.MaxValue;
        for (int i = 0; i < possessableBodies.Count; i++)
        {
            MonsterActor body = possessableBodies[i];
            if (!IsPossessableBodyValid(body)) continue;
            float sqr = (body.transform.position - origin).sqrMagnitude;
            if (sqr >= nearestSqr) continue;
            nearestSqr = sqr;
            nearest = body;
        }
        return nearest;
    }


    PossessionBodyProvider FindNearestValidShrine()
    {
        Vector3 origin = GetGuideOrigin();
        PossessionBodyProvider nearest = null;
        float nearestSqr = float.MaxValue;
        for (int i = 0; i < activeShrines.Count; i++)
        {
            PossessionBodyProvider shrine = activeShrines[i];
            if (shrine == null || !shrine.IsValidForGuide) continue;
            float sqr = (shrine.transform.position - origin).sqrMagnitude;
            if (sqr >= nearestSqr) continue;
            nearestSqr = sqr;
            nearest = shrine;
        }
        return nearest;
    }

    Vector3 GetGuideOrigin()
    {
        if (player == null && PlayerController.Instance != null)
            player = PlayerController.Instance.transform;
        if (player != null) return player.position;
        if (soulActor != null) return soulActor.transform.position;
        if (mainCamera == null) mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.transform.position : transform.position;
    }

    void ResetShrineGuide()
    {
        noPossessableMonsterTimer = 0f;
        lockedBody = null;
        lockedShrine = null;
        anchorsReady = false;
        pulseTime = 0f;
        Hide();
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
    void ShowPulse(float startT, float endT, float overallFade = 0f)
    {
        if (line == null) return;
        if (RenderPulse(line, runtimeMat, pulseParticles, guideColor, pulseOrigin, pulseTargetPos, startT, endT, overallFade))
        {
            IsShowing = true;
        }
        else
        {
            IsShowing = false;
            line.gameObject.SetActive(false);
        }
    }

    /// <summary>渲染一条引导脉冲：蛇形路径 + [startT,endT] 截取 + 脉冲头粒子。返回是否可见。</summary>
    bool RenderPulse(LineRenderer lr, Material mat, ParticleSystem[] particles, Color baseColor,
        Vector3 a, Vector3 b, float startT, float endT, float overallFade = 0f)
    {
        startT = Mathf.Clamp01(startT);
        endT = Mathf.Clamp01(endT);
        if (endT <= startT) return false;

        lr.gameObject.SetActive(true);

        // 能量抵达感：停留/消散阶段全亮（endT==1 时 advance 最大）；叠加柔和呼吸
        if (mat != null)
        {
            float advance = 0.55f + 0.45f * endT;
            float breathe = 1f;
            if (breatheAmount > 0f)
                breathe = 1f + Mathf.Sin(Time.time * breatheSpeed * 2f * Mathf.PI) * breatheAmount * 0.5f;
            float fadeMul = 1f - Mathf.Clamp01(overallFade);
            runtimeMat.SetColor("_Color0", guideColor * brightness * advance * breathe * fadeMul);
        }

        Vector3 a2 = a + Vector3.up * heightOffset;
        Vector3 b2 = b + Vector3.up * heightOffset;

        // 蛇形波动路径（全量生成，之后按 [startT, endT] 截取）
        int n = kSegments;
        Vector3 dir = b2 - a2;
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

        for (int i = 0; i <= n; i++)
        {
            float t = (float)i / n;
            Vector3 basePos = Vector3.Lerp(a2, b2, t);
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
        lr.positionCount = visibleCount;
        for (int i = 0; i < visibleCount; i++)
            lr.SetPosition(i, pathPoints[first + i]);

        // 脉冲头粒子：推进/停留阶段发射于脉冲尖端（每帧跟随推进位置），
        // 消散阶段停止发射（已发射光尘自然飘散，与引导线"从发出端消散"同步）。
        if (particles != null && particles.Length > 0)
        {
            if (startT > 0f)
            {
                StopPulseVfx(particles);
            }
            else
            {
                Vector3 headPos = GetPulseHeadPosition(pathPoints, n, endT);
                for (int i = 0; i < particles.Length; i++)
                {
                    if (particles[i] != null) particles[i].transform.position = headPos;
                }
                PlayPulseVfx(particles);
            }
        }
        return true;
    }

    void Hide()
    {
        if (IsShowing)
        {
            IsShowing = false;
            if (line != null) line.gameObject.SetActive(false);
        }
        // 隐藏时清空脉冲头粒子，防循环粒子残留（项目 VFX 规范同款处理）
        if (pulseParticles != null) StopPulseVfx(pulseParticles, true);
    }

    /// <summary>
    /// 构建引导线：优先 Instantiate 美术 Prefab 资产（渲染参数如对齐/阴影等在 Prefab 中配置，
    /// 美术可直接编辑资产）；为空时回退运行时动态创建。
    /// 材质取 Prefab 自带，运行时克隆实例应用动态参数（不影响资产与玩家拖尾）。
    /// </summary>
    void BuildLine()
    {
        line = BuildGuideLine(linePrefab,
            guideMode == GuideTargetMode.Shrines ? "ShrineGuideLine" : "MonsterGuideLine",
            guideColor, out runtimeMat, out pulseParticles);
        if (line != null) line.gameObject.SetActive(false);
    }

    /// <summary>构建一条引导线（Prefab 或运行时兜底），返回 LineRenderer 并回填材质与脉冲头粒子。</summary>
    LineRenderer BuildGuideLine(GameObject prefab, string name, Color color,
        out Material mat, out ParticleSystem[] particles)
    {
        GameObject go;
        LineRenderer lr;
        if (prefab != null)
        {
            go = Instantiate(prefab, transform, false);
            go.name = name;
            lr = go.GetComponent<LineRenderer>();
        }
        else
        {
            go = new GameObject(name);
            go.transform.SetParent(transform, false);
            lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.TransformZ; // 世界单位宽度（拖尾同款；View 是像素语义，会失真）
        }

        // 动态参数来自检查器（其余渲染参数由美术在 Prefab 中配置）
        lr.widthMultiplier = widthMultiplier;
        lr.widthCurve = widthCurve;

        mat = null;
        Material template = lr.sharedMaterial;
        if (template != null)
        {
            mat = new Material(template);
            // ASE 主色属性 _Color0（HDR 语义）
            mat.SetColor("_Color0", color * brightness);
            // 覆盖常见颜色属性，确保克隆实例不被其它属性带偏
            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_Color", Color.white);
            lr.sharedMaterial = mat;
        }
        else
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader != null)
            {
                mat = new Material(shader);
                mat.SetColor("_BaseColor", color * brightness);
                lr.sharedMaterial = mat;
            }
            else
            {
                Debug.LogWarning("[MonsterDirectionUI] 未配置引导线材质且找不到兜底 shader，引导线不可见。");
            }
        }

        // 脉冲头粒子：Prefab 子粒子系统（项目 VFX 规范：Root + 子粒子层），
        // 渲染材质复用同一克隆实例，颜色/呼吸与引导线同步。
        particles = go.GetComponentsInChildren<ParticleSystem>(true);
        if (mat != null)
        {
            foreach (var ps in particles)
            {
                if (ps == null) continue;
                var psr = ps.GetComponent<ParticleSystemRenderer>();
                if (psr != null) psr.sharedMaterial = mat;
            }
        }
        return lr;
    }

    /// <summary>
    /// 播放脉冲头粒子（对齐项目 VFX 规范：EnemyAbility.PlayVfx 遍历子粒子系统 Play(true)）。
    /// </summary>
    void PlayPulseVfx(ParticleSystem[] particles)
    {
        for (int i = 0; i < particles.Length; i++)
        {
            var ps = particles[i];
            if (ps != null && !ps.isPlaying) ps.Play(true);
        }
    }

    /// <summary>
    /// 停止脉冲头粒子发射：默认保留已发射粒子自然消散（与引导线"从发出端消散"同步）；
    /// clear=true 立即清空（隐藏时不留残留，防循环粒子残留——项目 StopVfxLooping 同目的）。
    /// </summary>
    void StopPulseVfx(ParticleSystem[] particles, bool clear = false)
    {
        for (int i = 0; i < particles.Length; i++)
        {
            var ps = particles[i];
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
        UnsubscribeEliteSpawn();
        if (runtimeMat != null) Destroy(runtimeMat);
    }
}
