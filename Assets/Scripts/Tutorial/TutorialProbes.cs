using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 教学探针集合：游戏事件 → TutorialFact 的转译层（唯一被允许订阅游戏事件的 Tutorial 组件）。
/// 零侵入原则：不改战斗代码签名，只订阅既有事件 + 低频轮询注册表边沿。
/// 由 TutorialController 挂载并 StartProbing/StopProbing。
/// </summary>
public class TutorialProbes : MonoBehaviour
{
    // ---- 轮询探针状态 ----
    readonly HashSet<Enemy> wasAlive = new HashSet<Enemy>();   // 上一帧存活的怪（击杀边沿检测）
    const float PollInterval = 0.25f;
    float nextPollTime;
    bool killEdgeDetected;
    bool possessedFirstReported;   // 首次附身事实只报一次

    PossessionManager pm;
    bool probing;

    /// <summary>
    /// 原始附身观察事件（每次附身提交都转发，含开场载体附身）。
    /// 供 TutorialController 订阅 TUT-MONSTER 怪介绍微教学——本组件对 PossessionManager 的绑定带每帧重试兜底，
    /// 比 TutorialController 直接订阅 PossessionManager 可靠（零"晚创建漏订"时序风险）。
    /// </summary>
    public event System.Action<MonsterActor> OnPossessionObserved;

    /// <summary>开始探针：订阅事件 + 开启轮询（幂等）。</summary>
    public void StartProbing()
    {
        if (probing) return;
        probing = true;

        pm = PossessionManager.Instance;
        if (pm != null)
        {
            pm.OnPossessionStarted += OnPossessionStarted;
            pm.OnPossessionEndedEx += OnPossessionEndedEx;
            // OnBodyDiedWhilePossessing 订阅留给 M3（死亡接力 TUT-06 检测点）
        }
        else
        {
            // PossessionManager 晚于本组件 Awake 的场景：Start 重试一次
            Invoke(nameof(TryLateBind), 0.5f);
        }

        // 初始化存活快照（已 Downed 的怪不算"首次击杀"）
        RebuildAliveSnapshot();
        nextPollTime = Time.time + PollInterval;
    }

    /// <summary>停止探针：退订事件 + 停止轮询（幂等）。</summary>
    public void StopProbing()
    {
        if (!probing) return;
        probing = false;

        if (pm != null)
        {
            pm.OnPossessionStarted -= OnPossessionStarted;
            pm.OnPossessionEndedEx -= OnPossessionEndedEx;
        }
        CancelInvoke(nameof(TryLateBind));
        wasAlive.Clear();
    }

    void TryLateBind()
    {
        // PossessionManager 晚于本组件 Awake 的场景（不同场景对象初始化序）：每次轮询时重试绑定
        if (pm != null) return;
        pm = PossessionManager.Instance;
        if (pm != null && probing)
        {
            pm.OnPossessionStarted += OnPossessionStarted;
            pm.OnPossessionEndedEx += OnPossessionEndedEx;
        }
    }

    void Update()
    {
        if (!probing) return;
        TryLateBind(); // 每次 tick 重试（成本=一次静态字段读；绑定成功后不再进入）
        if (Time.time < nextPollTime) return;
        nextPollTime = Time.time + PollInterval;

        PollKillEdge();
    }

    // ---- 探针 1：首次击杀（KilledFirstMonster） ----
    // EnemyRegistry 只读遍历 + isDowned 边沿检测（无战斗侧事件可订阅时的最小侵入方案）。
    // 已知边界：怪在两次轮询间（0.25s）出生即被 AOE 秒杀会漏报（下只怪击杀时补报）。
    // 不能用"发现 downed 即报"兜底：教学/刷体产出的永久尸体（SpawnAsPermanentCorpse）也是 downed，会误报。
    void PollKillEdge()
    {
        if (killEdgeDetected) return; // 事实只报一次

        var enemies = EnemyRegistry.All;
        var nowAlive = new HashSet<Enemy>();
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e == null) continue;
            if (!e.isDowned)
            {
                nowAlive.Add(e);
            }
            else if (wasAlive.Contains(e))
            {
                // 上一轮存活、本轮 Downed → 击杀边沿（首次击杀事实）
                killEdgeDetected = true;
                TutorialFactBus.Report(TutorialFact.KilledFirstMonster);
            }
        }
        // 快照滚动为当前存活集（下轮以此为基准）
        wasAlive.Clear();
        foreach (var a in nowAlive) wasAlive.Add(a);
    }

    void RebuildAliveSnapshot()
    {
        wasAlive.Clear();
        var enemies = EnemyRegistry.All;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e != null && !e.isDowned) wasAlive.Add(e);
        }
    }

    // ---- 探针 2：场上尸体存在（CorpseExists） ----
    // 注意：本事实是"状态性"事实，不在此轮询报告（避免每 0.25s 刷屏）；
    // 由 TutorialController 经 TutorialProbes.QueryCorpseExists() 在 Step 激活/轮询时实时查询。
    /// <summary>查询当前场上是否存在可附身尸体（含永久尸体；Controller 使用）。</summary>
    public static bool QueryCorpseExists()
    {
        var enemies = EnemyRegistry.All;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e == null) continue;
            if (e.isDowned && !e.isPossessed) return true;
        }
        return false;
    }

    // ---- 探针 3：附身语义细分（开场 / 首次 / 换身） ----
    MonsterActor lastPossessedBody;

    void OnPossessionStarted(MonsterActor body)
    {
        if (body == null) return;

        // 开场载体附身：由 TutorialController.OpeningCarrierRoutine 标记 pending（时间戳防残留）。
        // 开场附身同样算"首次附身"：补报 PossessedFirstBody，否则 TUT-02（完成条件=首次附身）
        // 在 autoPossessOpeningCarrier=true 的开场自动附身路径下永不完成、队列卡死。
        if (TutorialController.OpeningCarrierPendingUntil > Time.unscaledTime)
        {
            TutorialController.OpeningCarrierPendingUntil = 0f;   // 消费标记
            lastPossessedBody = body;   // 换身判定基线
            TutorialFactBus.Report(TutorialFact.OpeningCarrierPossessed);
            if (!possessedFirstReported)
            {
                possessedFirstReported = true;
                TutorialFactBus.Report(TutorialFact.PossessedFirstBody);
            }
            OnPossessionObserved?.Invoke(body);
            return;
        }

        // 主动换身：附身到与上一次不同的 body（含死亡被迫后附身新体，v1 语义宽松）
        if (lastPossessedBody != null && body != lastPossessedBody)
        {
            TutorialFactBus.Report(TutorialFact.SwitchedBody);
        }

        // 首次对尸体的附身（开场载体之外；击杀产生尸体必然先于非开场附身）
        if (!possessedFirstReported)
        {
            possessedFirstReported = true;
            TutorialFactBus.Report(TutorialFact.PossessedFirstBody);
        }

        lastPossessedBody = body;
        OnPossessionObserved?.Invoke(body);
    }

    // ---- 探针 4：主动脱离（ReleasedBody） ----
    // 用 OnPossessionEndedEx 区分原因：死亡被迫脱离 / 系统重置不报 ReleasedBody。
    void OnPossessionEndedEx(PossessionManager.PossessionEndReason reason)
    {
        if (reason == PossessionManager.PossessionEndReason.VoluntaryRelease)
            TutorialFactBus.Report(TutorialFact.ReleasedBody);
    }

    void OnDestroy()
    {
        StopProbing();
    }
}
