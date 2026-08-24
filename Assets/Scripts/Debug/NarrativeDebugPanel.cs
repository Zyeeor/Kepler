using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 叙事调试面板（F12 切换）：契约 §10 全部能力无代码可达。
/// - 强制设置 A0-A4（Access.ForceSet）
/// - 强制触发指定 Cue（Scheduler.ForceTriggerCue）
/// - 模拟事件计数（NarrativeEventBus.Report 自定义事件 + 内置事件下拉）
/// - 查看决策原因（GetRecentDecisions 环形缓冲）
/// - 状态总览（当前 Access / 高压门 / 等待队列 / 当前播放）
/// 惯例：enableDebug Inspector 开关 + GameManager.IsFormalFlow 屏蔽 + EnsureOnGameManager 挂 GameManager。
/// </summary>
public class NarrativeDebugPanel : MonoBehaviour
{
    [Tooltip("面板总开关（Inspector 可配）。")]
    public bool enableDebug = true;
    [Tooltip("是否显示面板（F12 切换）。")]
    public bool showPanel = false;
    [Tooltip("切换快捷键。")]
    public KeyCode toggleKey = KeyCode.F12;

    string cueIdInput = "";
    string customEventInput = "";
    int forceAccessChoice = 0;
    Vector2 decisionScroll;
    bool showPressureDetail;

    public static NarrativeDebugPanel EnsureOnGameManager()
    {
        var gm = GameManager.Instance;
        if (gm == null) return null;
        var existing = gm.GetComponent<NarrativeDebugPanel>();
        return existing != null ? existing : gm.gameObject.AddComponent<NarrativeDebugPanel>();
    }

    void Update()
    {
        if (!enableDebug || GameManager.IsFormalFlow) return;
        if (Input.GetKeyDown(toggleKey)) showPanel = !showPanel;
    }

    void OnGUI()
    {
        if (!showPanel || !enableDebug || !Application.isPlaying || GameManager.IsFormalFlow) return;

        float w = 460f, h = 620f;
        float x = 12f, y = 12f;
        GUI.Box(new Rect(x, y, w, h), "叙事调试面板（F12）");

        const float lineH = 22f, pad = 8f;
        float ty = y + lineH + 6f;
        var sched = NarrativeScheduler.Instance;

        // ── 1. Access 强制设置 ──
        GUI.Label(new Rect(x + pad, ty, 440f, lineH), $"当前 Access：{(sched != null ? sched.Access.Current.ToString() : "?")}");
        ty += lineH;
        string[] accessNames = { "A0", "A1", "A2", "A3", "A4" };
        forceAccessChoice = GUI.SelectionGrid(new Rect(x + pad, ty, 440f, lineH), forceAccessChoice, accessNames, 5);
        ty += lineH;
        if (GUI.Button(new Rect(x + pad, ty, 440f, lineH), "ForceSet 选中 Access"))
            sched?.Access.ForceSet((NarrativeAccess)forceAccessChoice);
        ty += lineH + 6f;

        // ── 2. 强制触发 Cue ──
        GUI.Label(new Rect(x + pad, ty, 440f, lineH), "强制触发 Cue（cueId）：");
        ty += lineH;
        cueIdInput = GUI.TextField(new Rect(x + pad, ty, 320f, lineH), cueIdInput);
        if (GUI.Button(new Rect(x + 332f, ty, 112f, lineH), "ForceTrigger"))
            sched?.ForceTriggerCue(cueIdInput.Trim());
        ty += lineH + 6f;

        // ── 3. 模拟事件 ──
        GUI.Label(new Rect(x + pad, ty, 440f, lineH), "模拟事件（内置下拉 / 自定义 id）：");
        ty += lineH;
        if (GUI.Button(new Rect(x + pad, ty, 150f, lineH), "WaveStarted"))
            Simulate(NarrativeTriggerEvent.WaveStarted, null);
        if (GUI.Button(new Rect(x + 158f, ty, 150f, lineH), "CardOffered"))
            Simulate(NarrativeTriggerEvent.CardOffered, null);
        if (GUI.Button(new Rect(x + 316f, ty, 150f, lineH), "EliteSpawned"))
            Simulate(NarrativeTriggerEvent.EliteSpawned, null);
        ty += lineH;
        if (GUI.Button(new Rect(x + pad, ty, 150f, lineH), "PossessionStarted"))
            Simulate(NarrativeTriggerEvent.PossessionStarted, null);
        if (GUI.Button(new Rect(x + 158f, ty, 150f, lineH), "RunWon"))
            Simulate(NarrativeTriggerEvent.RunWon, null);
        if (GUI.Button(new Rect(x + 316f, ty, 150f, lineH), "FinalPhase"))
            Simulate(NarrativeTriggerEvent.RunPhaseChanged, "Final");
        ty += lineH;
        customEventInput = GUI.TextField(new Rect(x + pad, ty, 320f, lineH), customEventInput);
        if (GUI.Button(new Rect(x + 332f, ty, 112f, lineH), "Report"))
            NarrativeEventBus.Report(customEventInput.Trim());
        ty += lineH + 6f;

        // ── 4. 高压门状态 ──
        showPressureDetail = GUI.Toggle(new Rect(x + pad, ty, 200f, lineH), showPressureDetail, "高压门命中（详情）");
        if (GUI.Button(new Rect(x + 208f, ty, 100f, lineH), "重置"))
            sched?.ResetForNewRun();
        if (GUI.Button(new Rect(x + 316f, ty, 128f, lineH), "ResetProfile"))
            NarrativeProfileStore.ResetProfile();
        ty += lineH;
        if (showPressureDetail && sched != null)
        {
            GUI.Label(new Rect(x + pad, ty, 440f, lineH), $"IsUnderPressure={sched.IsUnderPressure()}");
            ty += lineH;
        }
        ty += 4f;

        // ── 5. 决策原因环形缓冲 ──
        GUI.Label(new Rect(x + pad, ty, 440f, lineH), "决策原因（最近 64 条）：");
        ty += lineH;
        if (sched != null)
        {
            var decisions = sched.GetRecentDecisions();
            float listH = 200f;
            decisionScroll = GUI.BeginScrollView(new Rect(x + pad, ty, 440f, listH), decisionScroll,
                new Rect(0f, 0f, 420f, decisions.Count * 18f));
            for (int i = 0; i < decisions.Count; i++)
                GUI.Label(new Rect(0f, i * 18f, 420f, 18f), decisions[i].ToString());
            GUI.EndScrollView();
        }
    }

    void Simulate(NarrativeTriggerEvent evt, string qualifier)
    {
        NarrativeEventBus.Report(evt, qualifier); // 计数+转发（与真实事件同口）
    }
}
