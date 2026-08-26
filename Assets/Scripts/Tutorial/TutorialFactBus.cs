using System;
using UnityEngine;

/// <summary>
/// 教学事实（Tutorial Fact）：游戏事件归一化后的声明式"事实"。
/// Step 的完成条件 = 事实集合（AND），由 TutorialController 判定；
/// 事实由 TutorialProbes（游戏事件 → 事实的转译层）报告，Debug 可注入。
/// </summary>
public enum TutorialFact
{
    // ---- M1 已实现 ----
    /// <summary>首次击杀怪物（怪物进入 Downed 尸体态）。</summary>
    KilledFirstMonster,
    /// <summary>场上存在可附身的尸体（含永久尸体）。状态性事实，Controller 实时查询。</summary>
    CorpseExists,
    /// <summary>首次对"击杀产生的尸体"附身成功（TUT-04；不含开场载体附身）。</summary>
    PossessedFirstBody,
    /// <summary>主动脱离附身（主动语义，由 OnPossessionEndedEx 细分）。</summary>
    ReleasedBody,

    // ---- M2 新增 ----
    /// <summary>开场载体附身完成（TutorialController.OpeningCarrierRoutine 报告；TUT-01 开始条件）。</summary>
    OpeningCarrierPossessed,
    /// <summary>玩家按过位移键（Space 闪避；TUT-03C 位移教学完成条件）。</summary>
    InputMobilityPressed,
    /// <summary>玩家按过普攻（左键；TUT-02）。</summary>
    InputBasicPressed,
    /// <summary>玩家按过技能键（Q；TUT-02）。</summary>
    InputSkillPressed,
    /// <summary>玩家附身到与上一次不同的 body（主动换身；TUT-05 完成条件）。</summary>
    SwitchedBody,

    // ---- M3 预留（依赖 Death Relay / Soul Shrine 玩法实现，契约表） ----
    /// <summary>死亡接力成功（M3：需战斗程序在 Relay 成功时报告本事实）。</summary>
    DeathRelaySucceeded,
    /// <summary>灵魂回神龛恢复身体（M3：需战斗程序在恢复完成时报告本事实）。</summary>
    SoulShrineRestored,

    // ---- TUT-01 移动完成条件（WASD 有效移动，TutorialController.Update 轮询边沿报告） ----
    /// <summary>玩家按过 WASD 产生有效移动（TUT-01 完成条件；契约"有效移动且 Aim 方向发生变化"的移动半支，Aim 半支暂缓）。</summary>
    InputMovementPressed,
}

/// <summary>
/// 教学事实总线：探针/战斗侧 Report → Controller 订阅判定。
/// 纯静态实现（跨场景存活），事实只做边沿广播，不缓存状态（状态由 ProfileStore 持久化）。
/// </summary>
public static class TutorialFactBus
{
    /// <summary>事实报告事件（TutorialController 订阅；同帧内可多次）。</summary>
    public static event Action<TutorialFact> OnFactReported;

    /// <summary>报告事实（正式路径：探针转译游戏事件后调用）。重复报告安全（订阅方幂等）。</summary>
    public static void Report(TutorialFact fact)
    {
        OnFactReported?.Invoke(fact);
    }

    /// <summary>Debug 注入入口（TutorialDebugPanel / Editor 菜单使用；与正式路径同一条总线）。</summary>
    public static void ReportDebug(TutorialFact fact) => Report(fact);

    /// <summary>清空订阅（场景切换兜底，防止重复订阅泄漏）。</summary>
    public static void ClearSubscribers() => OnFactReported = null;
}
