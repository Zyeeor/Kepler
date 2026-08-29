using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// MonsterSkillIconConfig 定制检查器：按七罪分区展示怪物三槽，底部单独展示玩家两槽。
/// 配置路径：找怪物罪类型 → 找 HUD 槽位 → 拖 Sprite。
/// </summary>
[CustomEditor(typeof(MonsterSkillIconConfig))]
public class MonsterSkillIconConfigEditor : UnityEditor.Editor
{
    static readonly SinType[] SinOrder =
    {
        SinType.Pride,
        SinType.Lust,
        SinType.Wrath,
        SinType.Greed,
        SinType.Gluttony,
        SinType.Envy,
        SinType.Sloth,
    };

    static readonly MonsterSkillIconConfig.MonsterSlot[] MonsterSlotOrder =
    {
        MonsterSkillIconConfig.MonsterSlot.BasicAttack,
        MonsterSkillIconConfig.MonsterSlot.Skill,
        MonsterSkillIconConfig.MonsterSlot.Mobility,

    };

    static readonly MonsterSkillIconConfig.PlayerSlot[] PlayerSlotOrder =
    {
        MonsterSkillIconConfig.PlayerSlot.BasicAttack,
        MonsterSkillIconConfig.PlayerSlot.Possess,
    };

    static readonly Dictionary<SinType, string> SinNames = new Dictionary<SinType, string>
    {
        { SinType.Pride, "Pride 傲慢" },
        { SinType.Lust, "Lust 色欲" },
        { SinType.Wrath, "Wrath 暴怒" },
        { SinType.Greed, "Greed 贪婪" },
        { SinType.Gluttony, "Gluttony 暴食" },
        { SinType.Envy, "Envy 嫉妒" },
        { SinType.Sloth, "Sloth 怠惰" },
    };

    static readonly Dictionary<MonsterSkillIconConfig.MonsterSlot, string> MonsterSlotNames =
        new Dictionary<MonsterSkillIconConfig.MonsterSlot, string>
        {
            { MonsterSkillIconConfig.MonsterSlot.BasicAttack, "普攻" },
            { MonsterSkillIconConfig.MonsterSlot.Skill, "技能" },
            { MonsterSkillIconConfig.MonsterSlot.Mobility, "位移" },

        };

    static readonly Dictionary<MonsterSkillIconConfig.PlayerSlot, string> PlayerSlotNames =
        new Dictionary<MonsterSkillIconConfig.PlayerSlot, string>
        {
            { MonsterSkillIconConfig.PlayerSlot.BasicAttack, "灵魂普攻" },
            { MonsterSkillIconConfig.PlayerSlot.Possess, "附身" },
        };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var monsterEntries = serializedObject.FindProperty("monsterEntries");
        var identityEntries = serializedObject.FindProperty("monsterIdentityEntries");
        var playerEntries = serializedObject.FindProperty("playerEntries");
        var monsterMap = BuildMonsterMap(monsterEntries);
        var identityMap = BuildIdentityMap(identityEntries);
        var playerMap = BuildPlayerMap(playerEntries);
        var usedMonster = new HashSet<int>();
        var usedIdentity = new HashSet<int>();
        var usedPlayer = new HashSet<int>();


        EditorGUILayout.HelpBox(
            "Ability HUD 图标配置。怪物附身态显示三槽：普攻（左键）/ 技能（右键）/ 位移（空格）；每个槽位可独立配置 Sprite 与颜色。\n" +

            "留空会保留场景中当前默认图片；配置 Sprite 后运行时按当前怪物罪类型自动替换。",

            MessageType.Info);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("怪物技能图标", EditorStyles.boldLabel);
        foreach (var sin in SinOrder)
        {
            EditorGUILayout.Space(2);
            bool folded = EditorGUILayout.Foldout(true, SinNames[sin], true);
            if (!folded) continue;

            EditorGUI.indentLevel++;
            int identityIndex;
            if (identityMap.TryGetValue(sin, out identityIndex) && !usedIdentity.Contains(identityIndex))
            {
                usedIdentity.Add(identityIndex);
                var identity = identityEntries.GetArrayElementAtIndex(identityIndex);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("身份", GUILayout.Width(52));
                EditorGUILayout.PropertyField(identity.FindPropertyRelative("icon"), GUIContent.none);
                EditorGUILayout.PropertyField(identity.FindPropertyRelative("iconColor"), GUIContent.none, GUILayout.Width(72));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(identity.FindPropertyRelative("description"), new GUIContent("描述"));
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("身份", GUILayout.Width(52));
                if (GUILayout.Button("＋ 添加条目"))
                {
                    identityEntries.arraySize++;
                    var identity = identityEntries.GetArrayElementAtIndex(identityEntries.arraySize - 1);
                    identity.FindPropertyRelative("sin").intValue = (int)sin;
                }
                EditorGUILayout.EndHorizontal();
            }

            foreach (var slot in MonsterSlotOrder)

            {
                int index;
                var key = (sin, slot);
                if (monsterMap.TryGetValue(key, out index) && !usedMonster.Contains(index))
                {
                    usedMonster.Add(index);
                    var entry = monsterEntries.GetArrayElementAtIndex(index);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(MonsterSlotNames[slot], GUILayout.Width(52));
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("icon"), GUIContent.none);
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("iconColor"), GUIContent.none, GUILayout.Width(72));
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("description"), new GUIContent("描述"));

                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(MonsterSlotNames[slot], GUILayout.Width(52));
                    if (GUILayout.Button("＋ 添加条目"))
                    {
                        monsterEntries.arraySize++;
                        var entry = monsterEntries.GetArrayElementAtIndex(monsterEntries.arraySize - 1);
                        entry.FindPropertyRelative("sin").intValue = (int)sin;
                        entry.FindPropertyRelative("slot").intValue = (int)slot;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUI.indentLevel--;
        }

        DrawStrayMonsterEntries(monsterEntries, usedMonster);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("玩家技能图标", EditorStyles.boldLabel);
        foreach (var slot in PlayerSlotOrder)
        {
            int index;
            if (playerMap.TryGetValue(slot, out index) && !usedPlayer.Contains(index))
            {
                usedPlayer.Add(index);
                var entry = playerEntries.GetArrayElementAtIndex(index);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(PlayerSlotNames[slot], GUILayout.Width(72));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("icon"), GUIContent.none);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("description"), new GUIContent("描述"));
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(PlayerSlotNames[slot], GUILayout.Width(72));
                if (GUILayout.Button("＋ 添加条目"))
                {
                    playerEntries.arraySize++;
                    var entry = playerEntries.GetArrayElementAtIndex(playerEntries.arraySize - 1);
                    entry.FindPropertyRelative("slot").intValue = (int)slot;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        DrawStrayPlayerEntries(playerEntries, usedPlayer);
        serializedObject.ApplyModifiedProperties();
    }

    static Dictionary<(SinType, MonsterSkillIconConfig.MonsterSlot), int> BuildMonsterMap(SerializedProperty entries)
    {
        var map = new Dictionary<(SinType, MonsterSkillIconConfig.MonsterSlot), int>();
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            var key = ((SinType)entry.FindPropertyRelative("sin").intValue,
                (MonsterSkillIconConfig.MonsterSlot)entry.FindPropertyRelative("slot").intValue);
            if (key.Item1 != SinType.None && !map.ContainsKey(key)) map[key] = i;
        }
        return map;
    }

    static Dictionary<SinType, int> BuildIdentityMap(SerializedProperty entries)
    {
        var map = new Dictionary<SinType, int>();
        for (int i = 0; i < entries.arraySize; i++)
        {
            var sin = (SinType)entries.GetArrayElementAtIndex(i).FindPropertyRelative("sin").intValue;
            if (sin != SinType.None && !map.ContainsKey(sin)) map[sin] = i;
        }
        return map;
    }

    static Dictionary<MonsterSkillIconConfig.PlayerSlot, int> BuildPlayerMap(SerializedProperty entries)

    {
        var map = new Dictionary<MonsterSkillIconConfig.PlayerSlot, int>();
        for (int i = 0; i < entries.arraySize; i++)
        {
            var slot = (MonsterSkillIconConfig.PlayerSlot)entries.GetArrayElementAtIndex(i)
                .FindPropertyRelative("slot").intValue;
            if (!map.ContainsKey(slot)) map[slot] = i;
        }
        return map;
    }

    static void DrawStrayMonsterEntries(SerializedProperty entries, HashSet<int> used)
    {
        var stray = new List<int>();
        for (int i = 0; i < entries.arraySize; i++)
            if (!used.Contains(i)) stray.Add(i);
        if (stray.Count == 0) return;

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField($"未识别怪物条目（重复或 None，{stray.Count} 条）", EditorStyles.boldLabel);
        int removeIndex = -1;
        foreach (var index in stray)
        {
            var entry = entries.GetArrayElementAtIndex(index);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("sin"), GUIContent.none, GUILayout.Width(110));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("slot"), GUIContent.none, GUILayout.Width(110));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("icon"), GUIContent.none);
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("iconColor"), GUIContent.none, GUILayout.Width(72));
            if (GUILayout.Button("✕", GUILayout.Width(22))) removeIndex = index;
            EditorGUILayout.EndHorizontal();
        }

        if (removeIndex >= 0) entries.DeleteArrayElementAtIndex(removeIndex);
    }

    static void DrawStrayPlayerEntries(SerializedProperty entries, HashSet<int> used)
    {
        var stray = new List<int>();
        for (int i = 0; i < entries.arraySize; i++)
            if (!used.Contains(i)) stray.Add(i);
        if (stray.Count == 0) return;

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField($"未识别玩家条目（重复，{stray.Count} 条）", EditorStyles.boldLabel);
        int removeIndex = -1;
        foreach (var index in stray)
        {
            var entry = entries.GetArrayElementAtIndex(index);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("slot"), GUIContent.none, GUILayout.Width(110));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("icon"), GUIContent.none);
            if (GUILayout.Button("✕", GUILayout.Width(22))) removeIndex = index;
            EditorGUILayout.EndHorizontal();
        }
        if (removeIndex >= 0) entries.DeleteArrayElementAtIndex(removeIndex);
    }
}
