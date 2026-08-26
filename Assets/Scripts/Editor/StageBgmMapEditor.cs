using UnityEditor;
using UnityEngine;

/// <summary>
/// StageBgmMap 定制检查器：补上当前缺失的策划友好 UI。
///   - 逐波 BGM（waveTiers）：每行 = 波次号 + slot（action/clip/淡入淡出）+ 删除，可一键添加；
///   - 阶段槽位（combat/choice/final/result/fail/soul/elite）：每槽一行 action 三态。
/// 仲裁规则（终态 > 覆盖槽 > 阶段槽 > 场景曲）在 BgmController 单点实现，本编辑器只负责配置。
/// </summary>
[CustomEditor(typeof(StageBgmMap))]
public class StageBgmMapEditor : UnityEditor.Editor
{
    static readonly string[] PhaseKeys =
    {
        "combat", "choice", "final", "result", "fail", "soul", "elite",
    };

    static readonly string[] PhaseLabels =
    {
        "combat 统一波次曲", "choice 选卡", "final Final", "result 结算胜",
        "fail 结算败", "soul 灵魂态", "elite 精英",
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var map = (StageBgmMap)target;
        var waveTiersProp = serializedObject.FindProperty("waveTiers");

        EditorGUILayout.HelpBox(
            "阶段 BGM 映射。仲裁：终态(结算/失败) > 覆盖槽(soul/elite) > 阶段槽(含逐波) > 场景曲。\n" +
            "每槽 action 三态：Inherit 保持当前 / Play 播放 clip（clip 空回退 Inherit）/ Stop 淡出停止。\n" +
            "逐波 BGM：某波配了条目 → 进波按 action 处理；未配 → 保持当前曲。列表空 = 所有波共用 combat 槽。",
            MessageType.Info);

        // ── 逐波 BGM（waveTiers）──
        EditorGUILayout.LabelField("逐波 BGM（waveTiers）", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("按波次配不同战斗曲。第 N 波匹配到条目则切曲；未匹配保持当前曲。", MessageType.None);

        int removeWave = -1;
        for (int i = 0; i < waveTiersProp.arraySize; i++)
        {
            var tier = waveTiersProp.GetArrayElementAtIndex(i);
            var waveNumProp = tier.FindPropertyRelative("waveNumber");
            var slotProp = tier.FindPropertyRelative("slot");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(waveNumProp, GUIContent.none, GUILayout.Width(48));
            DrawSlot(slotProp);
            if (GUILayout.Button("✕", GUILayout.Width(22)))
                removeWave = i;
            EditorGUILayout.EndHorizontal();
        }
        if (removeWave >= 0)
            waveTiersProp.DeleteArrayElementAtIndex(removeWave);
        if (GUILayout.Button("＋ 添加波次"))
        {
            waveTiersProp.arraySize++;
            var tier = waveTiersProp.GetArrayElementAtIndex(waveTiersProp.arraySize - 1);
            tier.FindPropertyRelative("waveNumber").intValue = 1;
        }

        EditorGUILayout.Space(8);

        // ── 阶段槽位 ──
        EditorGUILayout.LabelField("阶段槽位（RunPhase → BGM）", EditorStyles.boldLabel);
        for (int i = 0; i < PhaseKeys.Length; i++)
        {
            var slotProp = serializedObject.FindProperty(PhaseKeys[i]);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(PhaseLabels[i], GUILayout.Width(130));
            DrawSlot(slotProp);
            EditorGUILayout.EndHorizontal();
        }

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>绘制一个 BGM 槽位：action 三态 + clip + 淡入淡出覆盖。</summary>
    void DrawSlot(SerializedProperty slotProp)
    {
        var actionProp = slotProp.FindPropertyRelative("action");
        var clipProp = slotProp.FindPropertyRelative("clip");
        var fadeProp = slotProp.FindPropertyRelative("fadeOverride");
        EditorGUILayout.PropertyField(actionProp, GUIContent.none, GUILayout.Width(80));
        EditorGUILayout.PropertyField(clipProp, GUIContent.none);
        EditorGUILayout.PropertyField(fadeProp, new GUIContent("淡入淡出"), GUILayout.Width(80));
    }
}
