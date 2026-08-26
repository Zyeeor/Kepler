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

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var cfg = (MonsterSkillAudioConfig)target;
        var entriesProp = serializedObject.FindProperty("entries");

        EditorGUILayout.HelpBox(
            "怪物技能施放音效：每个怪三种技能（位移/普攻/技能）各配一组候选音源。\n" +
            "· 随机多音源：候选列表按「选取」规则随机取一条（不连续重复 = 按条目去重，重复放同音可加权）；\n" +
            "· 敌我分轨：打开后敌方（AI）与附身（玩家控制）各配一组独立音源/音量/音高；\n" +
            "· 空间化：每组音源可选 2D（恒定音量）/ 3D（随距离衰减），默认敌方 3D、附身 2D。\n" +
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

        int removeIndex = -1;
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
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUI.indentLevel--;
        }

        if (removeIndex >= 0)
            entriesProp.DeleteArrayElementAtIndex(removeIndex);

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

        // 空间化：2D（恒定音量）/ 3D（随距离衰减）
        var spatialProp = clipSetProp.FindPropertyRelative("spatialMode");
        EditorGUILayout.PropertyField(spatialProp, new GUIContent("空间化"));

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

        EditorGUILayout.PropertyField(clipsProp, new GUIContent("候选音源"), true);
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
}
