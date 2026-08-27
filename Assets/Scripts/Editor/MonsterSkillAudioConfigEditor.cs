using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// MonsterSkillAudioConfig 定制检查器：按七罪分区（每个怪一个折叠区），
/// 每个（sin, kind）行支持：
///   - 随机多音源：候选 clip 列表 + 选取规则（纯随机 / 不连续重复）+ 音量/音高；
///   - 敌我分轨：splitSides 开关，开 = 敌方（AI）/ 附身（玩家控制）各一组独立音源。
/// 条目数据（sin, kind）锁定由本编辑器生成；资产里游离/重复条目会落到"未识别条目"区供清理。
/// </summary>
[CustomEditor(typeof(MonsterSkillAudioConfig))]
public class MonsterSkillAudioConfigEditor : UnityEditor.Editor
{
    static readonly SinType[] SinOrder =
    {
        SinType.Pride, SinType.Lust, SinType.Wrath, SinType.Greed,
        SinType.Gluttony, SinType.Envy, SinType.Sloth,
    };

    static readonly EnemyAbility.AbilityType[] KindOrder =
    {
        EnemyAbility.AbilityType.Mobility,
        EnemyAbility.AbilityType.BasicAttack,
        EnemyAbility.AbilityType.Skill,
    };

    static readonly Dictionary<SinType, string> SinNames = new Dictionary<SinType, string>
    {
        { SinType.Pride, "Pride 傲慢" }, { SinType.Lust, "Lust 色欲" },
        { SinType.Wrath, "Wrath 暴怒" }, { SinType.Greed, "Greed 贪婪" },
        { SinType.Gluttony, "Gluttony 暴食" }, { SinType.Envy, "Envy 嫉妒" },
        { SinType.Sloth, "Sloth 怠惰" },
    };

    static readonly Dictionary<EnemyAbility.AbilityType, string> KindNames = new Dictionary<EnemyAbility.AbilityType, string>
    {
        { EnemyAbility.AbilityType.Mobility, "位移" },
        { EnemyAbility.AbilityType.BasicAttack, "普攻" },
        { EnemyAbility.AbilityType.Skill, "技能" },
    };

    readonly Dictionary<(SinType, EnemyAbility.AbilityType), bool> _foldouts =
        new Dictionary<(SinType, EnemyAbility.AbilityType), bool>();
    readonly Dictionary<SinType, bool> _droneFoldouts = new Dictionary<SinType, bool>();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var cfg = (MonsterSkillAudioConfig)target;
        var entriesProp = serializedObject.FindProperty("entries");
        var droneEntriesProp = serializedObject.FindProperty("droneEntries");

        EditorGUILayout.HelpBox(
            "怪物技能施放音效：每个怪三种技能（位移/普攻/技能）各配一组候选音源。\n" +
            "· 选取规则：纯随机 / 不连续重复（按条目去重，重复放同音可加权）/ 蓄力分档（按蓄力程度二选一，用于蓄力类普攻）；\n" +
            "· 敌我分轨：打开后敌方（AI）与附身（玩家控制）各配一组独立音源/音量/音高；\n" +
            "· 空间化：每组音源可选 2D（恒定音量）/ 3D（随距离衰减），默认敌方 3D、附身 2D；\n" +
            "· 循环音效：勾选后为持续技能（按住持续施放，如嫉妒激光）的循环音，由调用方 Start/Stop 控制启停；\n" +
            "· 无人机：召唤物（如怠惰木灵）的攻击音，逻辑与技能条目相同（敌我分轨/空间化/随机多音源）。\n" +
            "留空 = 静默（正常设计）。能力预制体上的 castAudioName 字段非空时优先（单能力覆盖）。",
            MessageType.Info);

        // 索引现有条目
        var map = new Dictionary<(SinType, EnemyAbility.AbilityType), int>();
        var used = new HashSet<int>();
        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            var e = entriesProp.GetArrayElementAtIndex(i);
            var sin = (SinType)e.FindPropertyRelative("sin").intValue;
            var kind = (EnemyAbility.AbilityType)e.FindPropertyRelative("kind").intValue;
            var key = (sin, kind);
            if (sin != SinType.None && !map.ContainsKey(key))
                map[key] = i;
        }

        // 索引无人机条目（sin → index）
        var droneMap = new Dictionary<SinType, int>();
        var droneUsed = new HashSet<int>();
        for (int i = 0; i < droneEntriesProp.arraySize; i++)
        {
            var e = droneEntriesProp.GetArrayElementAtIndex(i);
            var sin = (SinType)e.FindPropertyRelative("sin").intValue;
            if (sin != SinType.None && !droneMap.ContainsKey(sin))
                droneMap[sin] = i;
        }

        int removeIndex = -1;
        int droneRemoveIndex = -1;
        foreach (var sin in SinOrder)
        {
            EditorGUILayout.Space(2);
            bool sinFolded = EditorGUILayout.Foldout(true, SinNames[sin], true);
            if (!sinFolded) continue;
            EditorGUI.indentLevel++;
            foreach (var kind in KindOrder)
            {
                var key = (sin, kind);
                int idx;
                if (map.TryGetValue(key, out idx) && !used.Contains(idx))
                {
                    used.Add(idx);
                    var e = entriesProp.GetArrayElementAtIndex(idx);
                    bool folded = _foldouts.TryGetValue(key, out bool f) ? f : false;

                    bool split = e.FindPropertyRelative("splitSides").boolValue;
                    int enemyCount = CountClips(e.FindPropertyRelative("enemy"));
                    int possessedCount = CountClips(e.FindPropertyRelative("possessed"));
                    string summary = enemyCount + (split ? $"+{possessedCount}" : "") + " 音";
                    folded = EditorGUILayout.Foldout(folded, $"{KindNames[kind]}  [{summary}]", true);
                    _foldouts[key] = folded;

                    if (folded)
                    {
                        EditorGUI.indentLevel++;
                        var splitProp = e.FindPropertyRelative("splitSides");
                        EditorGUILayout.PropertyField(splitProp, new GUIContent("敌我分轨"));
                        DrawClipSet(e.FindPropertyRelative("enemy"), splitProp.boolValue ? "敌方（AI）" : "音源组");
                        if (splitProp.boolValue)
                            DrawClipSet(e.FindPropertyRelative("possessed"), "附身（玩家控制）");
                        if (kind == EnemyAbility.AbilityType.Mobility)
                            DrawClipSet(e.FindPropertyRelative("returnSet"), "回归音（换位/第二段，可选，留空回退去程）");
                        if (GUILayout.Button("删除此条目"))
                            removeIndex = idx;
                        EditorGUI.indentLevel--;
                    }
                }
                else
                {
                    // 缺失条目：提供一键创建（sin/kind 锁定）
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(KindNames[kind], GUILayout.Width(40));
                    if (GUILayout.Button("＋ 添加条目"))
                    {
                        entriesProp.arraySize++;
                        var e = entriesProp.GetArrayElementAtIndex(entriesProp.arraySize - 1);
                        e.FindPropertyRelative("sin").intValue = (int)sin;
                        e.FindPropertyRelative("kind").intValue = (int)kind;
                        // 显式初始化音源组字段（Unity 新增数组元素可能跳过字段初始化器，音高等 float 会落 0 导致静音）
                        InitClipSetDefaults(e.FindPropertyRelative("enemy"), MonsterSkillAudioConfig.SpatialMode.Positional3D);
                        InitClipSetDefaults(e.FindPropertyRelative("possessed"), MonsterSkillAudioConfig.SpatialMode.Flat2D);
                        InitClipSetDefaults(e.FindPropertyRelative("returnSet"), MonsterSkillAudioConfig.SpatialMode.Positional3D);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            // ── 无人机（召唤物攻击音）条目：按召唤者 sin 查表，逻辑与技能条目同构 ──
            int droneIdx;
            if (droneMap.TryGetValue(sin, out droneIdx) && !droneUsed.Contains(droneIdx))
            {
                droneUsed.Add(droneIdx);
                var de = droneEntriesProp.GetArrayElementAtIndex(droneIdx);
                bool dFolded = _droneFoldouts.TryGetValue(sin, out bool df) ? df : false;

                bool dSplit = de.FindPropertyRelative("splitSides").boolValue;
                int dEnemyCount = CountClips(de.FindPropertyRelative("enemy"));
                int dPossessedCount = CountClips(de.FindPropertyRelative("possessed"));
                string dSummary = dEnemyCount + (dSplit ? $"+{dPossessedCount}" : "") + " 音";
                dFolded = EditorGUILayout.Foldout(dFolded, $"无人机  [{dSummary}]", true);
                _droneFoldouts[sin] = dFolded;

                if (dFolded)
                {
                    EditorGUI.indentLevel++;
                    var dSplitProp = de.FindPropertyRelative("splitSides");
                    EditorGUILayout.PropertyField(dSplitProp, new GUIContent("敌我分轨"));
                    DrawClipSet(de.FindPropertyRelative("enemy"), dSplitProp.boolValue ? "敌方（AI）" : "音源组");
                    if (dSplitProp.boolValue)
                        DrawClipSet(de.FindPropertyRelative("possessed"), "附身（玩家控制）");
                    if (GUILayout.Button("删除此条目"))
                        droneRemoveIndex = droneIdx;
                    EditorGUI.indentLevel--;
                }
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("无人机", GUILayout.Width(40));
                if (GUILayout.Button("＋ 添加无人机条目"))
                {
                    droneEntriesProp.arraySize++;
                    var de = droneEntriesProp.GetArrayElementAtIndex(droneEntriesProp.arraySize - 1);
                    de.FindPropertyRelative("sin").intValue = (int)sin;
                    // 显式初始化音源组字段（同普通条目：避免新增元素 float 字段落 0 导致音高=0 静音）
                    InitClipSetDefaults(de.FindPropertyRelative("enemy"), MonsterSkillAudioConfig.SpatialMode.Positional3D);
                    InitClipSetDefaults(de.FindPropertyRelative("possessed"), MonsterSkillAudioConfig.SpatialMode.Flat2D);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        }

        if (removeIndex >= 0)
            entriesProp.DeleteArrayElementAtIndex(removeIndex);
        if (droneRemoveIndex >= 0)
            droneEntriesProp.DeleteArrayElementAtIndex(droneRemoveIndex);

        // 未识别条目（游离/重复/sin=None）兜底区
        int stray = 0;
        for (int i = 0; i < entriesProp.arraySize; i++)
            if (!used.Contains(i)) stray++;
        if (stray > 0)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.Foldout(true, $"未识别条目（重复/sin=None，{stray} 条）", true);
            EditorGUI.indentLevel++;
            int strayRemove = -1;
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                if (used.Contains(i)) continue;
                var e = entriesProp.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(e.FindPropertyRelative("sin"), GUIContent.none, GUILayout.Width(110));
                EditorGUILayout.PropertyField(e.FindPropertyRelative("kind"), GUIContent.none, GUILayout.Width(110));
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                    strayRemove = i;
                EditorGUILayout.EndHorizontal();
            }
            if (strayRemove >= 0)
                entriesProp.DeleteArrayElementAtIndex(strayRemove);
            EditorGUI.indentLevel--;
        }

        // 未识别无人机条目兜底区
        int droneStray = 0;
        for (int i = 0; i < droneEntriesProp.arraySize; i++)
            if (!droneUsed.Contains(i)) droneStray++;
        if (droneStray > 0)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.Foldout(true, $"未识别无人机条目（重复/sin=None，{droneStray} 条）", true);
            EditorGUI.indentLevel++;
            int droneStrayRemove = -1;
            for (int i = 0; i < droneEntriesProp.arraySize; i++)
            {
                if (droneUsed.Contains(i)) continue;
                var e = droneEntriesProp.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(e.FindPropertyRelative("sin"), GUIContent.none, GUILayout.Width(110));
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                    droneStrayRemove = i;
                EditorGUILayout.EndHorizontal();
            }
            if (droneStrayRemove >= 0)
                droneEntriesProp.DeleteArrayElementAtIndex(droneStrayRemove);
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>绘制一个音源组：选取规则 + 空间化 + 音量（滑轨+% 数值框）+ 音高（滑轨+数值框）+ 候选 clip 列表。</summary>
    void DrawClipSet(SerializedProperty clipSetProp, string label)
    {
        var clipsProp = clipSetProp.FindPropertyRelative("clips");
        var pickModeProp = clipSetProp.FindPropertyRelative("pickMode");
        var volumeProp = clipSetProp.FindPropertyRelative("volumeScale");
        var pitchProp = clipSetProp.FindPropertyRelative("pitch");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

        // 选取规则独占一行（保证下拉宽度充足）
        EditorGUILayout.PropertyField(pickModeProp, new GUIContent("选取"));

        // 蓄力分档：阈值滑轨（仅 ChargeTiered 生效）。用独立作用域避免和音量行的 pct/newPct 冲突。
        var pickMode = (MonsterSkillAudioConfig.ClipPickMode)pickModeProp.intValue;
        if (pickMode == MonsterSkillAudioConfig.ClipPickMode.ChargeTiered)
        {
            var thresholdProp = clipSetProp.FindPropertyRelative("heavyCastThreshold");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("分档阈值");
            EditorGUILayout.Slider(thresholdProp, 0f, 1f, GUIContent.none, GUILayout.MinWidth(160));
            int thrPct = Mathf.RoundToInt(thresholdProp.floatValue * 100f);
            int newThrPct = EditorGUILayout.IntField(thrPct, GUILayout.Width(60));
            if (newThrPct != thrPct)
                thresholdProp.floatValue = Mathf.Clamp(newThrPct, 0, 100) / 100f;
            EditorGUILayout.LabelField("%", GUILayout.Width(16));
            EditorGUILayout.EndHorizontal();
        }

        // 空间化：2D（恒定音量）/ 3D（随距离衰减）
        var spatialProp = clipSetProp.FindPropertyRelative("spatialMode");
        EditorGUILayout.PropertyField(spatialProp, new GUIContent("空间化"));

        // 循环音效：true = 持续技能（按住持续施放，如嫉妒激光）由调用方 Start/Stop 控制
        var loopProp = clipSetProp.FindPropertyRelative("loop");
        EditorGUILayout.PropertyField(loopProp, new GUIContent("循环音效"));

        // 音量：滑轨（占主宽度） + 百分比数值框（0~100%，100% = 满音量）+ "%" 后缀
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("音量");
        EditorGUILayout.Slider(volumeProp, 0f, 1f, GUIContent.none, GUILayout.MinWidth(140));
        int pct = Mathf.RoundToInt(volumeProp.floatValue * 100f);
        int newPct = EditorGUILayout.IntField(pct, GUILayout.Width(60));
        if (newPct != pct)
            volumeProp.floatValue = Mathf.Clamp(newPct, 0, 100) / 100f;
        EditorGUILayout.LabelField("%", GUILayout.Width(16));
        EditorGUILayout.EndHorizontal();

        // 音高：滑轨 + 数值框（0.50~1.50，标准音高倍数，不加 % 避免误解）
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("音高");
        EditorGUILayout.Slider(pitchProp, 0.5f, 1.5f, GUIContent.none, GUILayout.MinWidth(160));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(clipsProp, new GUIContent(
            pickMode == MonsterSkillAudioConfig.ClipPickMode.ChargeTiered
                ? "候选音源（[0]=低蓄力，[1]=高蓄力）"
                : "候选音源"), true);
        EditorGUILayout.EndVertical();
    }

    /// <summary>统计音源组内有效 clip 数（非 null）。</summary>
    static int CountClips(SerializedProperty clipSetProp)
    {
        var clipsProp = clipSetProp.FindPropertyRelative("clips");
        int n = 0;
        for (int i = 0; i < clipsProp.arraySize; i++)
            if (clipsProp.GetArrayElementAtIndex(i).objectReferenceValue != null) n++;
        return n;
    }

    /// <summary>
    /// 显式初始化一个音源组的字段默认值。Unity 通过 arraySize++ 新增数组元素时可能跳过
    /// 字段初始化器（float 落 0、enum 落 0），导致新条目音高=0 静音、音量为 0。
    /// 与 ClipSet / DroneEntry 的字段默认值保持一致：选取=NoRepeat、音量=1、音高=1、阈值=0.5。
    /// </summary>
    static void InitClipSetDefaults(SerializedProperty clipSetProp, MonsterSkillAudioConfig.SpatialMode defaultSpatial)
    {
        clipSetProp.FindPropertyRelative("pickMode").intValue = (int)MonsterSkillAudioConfig.ClipPickMode.NoRepeat;
        clipSetProp.FindPropertyRelative("volumeScale").floatValue = 1f;
        clipSetProp.FindPropertyRelative("pitch").floatValue = 1f;
        clipSetProp.FindPropertyRelative("spatialMode").intValue = (int)defaultSpatial;
        clipSetProp.FindPropertyRelative("heavyCastThreshold").floatValue = 0.5f;
        clipSetProp.FindPropertyRelative("loop").boolValue = false;
    }
}
