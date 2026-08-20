#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

/// <summary>
/// Play-mode 波次流程调试面板：
///   - F5：清场跳波（视为当前波所有怪已击杀 → 选卡 → 下一波）
///   - 屏幕左下角显示 Run 阶段链（RunSession.CurrentPhase）、当前波次、在场怪数
/// 正式流程（GameManager.IsFormalFlow）下整体屏蔽（与 MonsterPossessionCheat 同款约定）。
/// </summary>
public class WaveFlowDebug : MonoBehaviour
{
    [Header("Input")]
    public bool enableDebug = true;
    [Tooltip("清场跳波按键。")]
    public KeyCode skipWaveKey = KeyCode.F5;

    [Header("Display")]
    public bool showPanel = true;

    void Update()
    {
        if (!enableDebug || GameManager.IsFormalFlow) return;
        if (Input.GetKeyDown(skipWaveKey))
        {
            if (WaveManager.Instance != null) WaveManager.Instance.DebugSkipWave();
            else Debug.LogWarning("[WaveFlowDebug] WaveManager.Instance 为空，跳波失败。");
        }
    }

    void OnGUI()
    {
        if (!enableDebug || !showPanel || GameManager.IsFormalFlow) return;

        var session = RunSession.Instance;
        var wave = WaveManager.Instance;

        string phase = session != null ? session.CurrentPhase.ToString() : "(无会话)";
        string waveInfo = wave != null
            ? $"Wave {wave.CurrentWaveIndex} | 在场 {wave.EnemiesAlive} | 活动 {wave.IsWaveActive}"
            : "WaveManager 缺失";

        float w = 300f, h = 70f;
        GUI.Box(new Rect(10f, Screen.height - h - 10f, w, h), "Wave Flow Debug");
        GUI.Label(new Rect(20f, Screen.height - h + 8f, w - 20f, 18f), $"阶段 {phase}");
        GUI.Label(new Rect(20f, Screen.height - h + 26f, w - 20f, 18f), waveInfo);
        GUI.Label(new Rect(20f, Screen.height - h + 44f, w - 20f, 18f), "F5 = 清场跳波（视为击杀当前波全部怪）");
    }
}
#endif
