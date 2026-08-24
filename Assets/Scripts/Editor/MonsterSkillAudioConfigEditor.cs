using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// MonsterSkillAudioConfig 定制检查器：按七罪分区（每个怪一个折叠区），
/// 每区固定三行：位移 / 普攻 / 技能，每行 = [clip][音量][音高]。
/// 策划配置路径："找怪 → 找技能类别 → 拖音源"，一目了然。
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

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var cfg = (MonsterSkillAudioConfig)target;
        var entriesProp = serializedObject.FindProperty("entries");

        EditorGUILayout.HelpBox(
            "怪物技能施放音效：每个怪三种技能（位移/普攻/技能）各配一条音源。\n" +
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
            bool folded = EditorGUILayout.Foldout(true, SinNames[sin], true);
            if (!folded) continue;
            EditorGUI.indentLevel++;
            foreach (var kind in KindOrder)
            {
                var key = (sin, kind);
                int idx;
                if (map.TryGetValue(key, out idx) && !used.Contains(idx))
                {
                    used.Add(idx);
                    var e = entriesProp.GetArrayElementAtIndex(idx);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(KindNames[kind], GUILayout.Width(40));
                    EditorGUILayout.PropertyField(e.FindPropertyRelative("clip"), GUIContent.none);
                    EditorGUILayout.PropertyField(e.FindPropertyRelative("volumeScale"), new GUIContent("音量"), GUILayout.Width(90));
                    EditorGUILayout.PropertyField(e.FindPropertyRelative("pitch"), new GUIContent("音高"), GUILayout.Width(90));
                    if (GUILayout.Button("✕", GUILayout.Width(22)))
                        removeIndex = idx;
                    EditorGUILayout.EndHorizontal();
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
                        e.FindPropertyRelative("volumeScale").floatValue = 1f;
                        e.FindPropertyRelative("pitch").floatValue = 1f;
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
                EditorGUILayout.PropertyField(e.FindPropertyRelative("clip"), GUIContent.none);
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
}
