using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SfxBank 定制检查器：按类别（游戏事件 / UI / 战斗 / 技能）分区显示条目，
/// 策划配置时一眼看清"这是哪类音效、挂点在哪、配没配 clip"。
/// 分区依据：SfxBank.GetCategory(SfxId)（SfxId 枚举注释同步分组）。
/// </summary>
[CustomEditor(typeof(SfxBank))]
public class SfxBankEditor : UnityEditor.Editor
{
    static readonly SfxCategory[] SectionOrder = { SfxCategory.GameEvent, SfxCategory.UI, SfxCategory.Combat };

    static readonly Dictionary<SfxCategory, SfxId[]> SectionIds = new Dictionary<SfxCategory, SfxId[]>
    {
        {
            SfxCategory.GameEvent,
            new[]
            {
                SfxId.WaveStart, SfxId.WaveClear, SfxId.AllWavesComplete,
                SfxId.PossessionStart, SfxId.PossessionEnd, SfxId.PossessBodyDied,
                SfxId.CorpseWindow, SfxId.SoulEnter, SfxId.SoulDeath,
                SfxId.BulletTimeStart, SfxId.BulletTimeEnd, SfxId.FinalBegin,
                SfxId.FinalClear, SfxId.FinalPhaseChange, SfxId.ShrineProximity,
                SfxId.ShrineProvide, SfxId.VictoryEpilogueEnter,
                SfxId.VictoryEpilogueFirstTextReveal, SfxId.VictoryEpilogueNameInputReveal,
                SfxId.VictoryEpilogueNameConfirm, SfxId.VictoryEpilogueFinalReveal,
                SfxId.VictoryEpilogueFinalTitleReveal, SfxId.VictoryEpilogueFinalNameReveal,
                SfxId.VictoryEpilogueFinalCoronationReveal, SfxId.VictoryEpilogueExitBlack,
            }
        },
        {
            SfxCategory.UI,
            new[]
            {
                SfxId.UiClick, SfxId.CardOpen, SfxId.CardSelect, SfxId.CardReroll,
                SfxId.HallOfFameOpen, SfxId.HallOfFameClose, SfxId.CardArchiveOpen,
                SfxId.CardArchiveClose, SfxId.BuildExpand, SfxId.BuildCollapse,
            }
        },
        {
            SfxCategory.Combat,
            new[]
            {
                SfxId.BodyHit, SfxId.PlayerHurt, SfxId.EnemyFatal, SfxId.CorpseAvailable,
                SfxId.TargetLock, SfxId.MovementLoop, SfxId.EliteSpawn, SfxId.Hazard,
            }
        },
    };

    static readonly Dictionary<SfxCategory, string> SectionTitles = new Dictionary<SfxCategory, string>
    {
        { SfxCategory.GameEvent, "游戏事件音效（波次/附身/结算…）" },
        { SfxCategory.UI, "UI 界面音效（点击/选卡）" },
        { SfxCategory.Combat, "战斗音效（受击/击杀/移动…）" },
    };

    static readonly Dictionary<SfxCategory, string> SectionHints = new Dictionary<SfxCategory, string>
    {
        { SfxCategory.GameEvent, "挂点已接线（AudioEventBinder），只需拖 clip；留空 = 静默。" },
        { SfxCategory.UI, "走独立 UI 通道；UiClick 由通用点击音驱动，Card* 由选卡弹窗驱动。\n" +
                           "HallOfFame/CardArchive 由局外面板 Show/Hide 驱动；BuildExpand/Collapse 由局内构筑展开/收起驱动。" },
        { SfxCategory.Combat, "战斗负责人直调；占位条目拖 clip 即生效。" },
    };

    readonly Dictionary<SfxCategory, bool> _foldouts = new Dictionary<SfxCategory, bool>
    {
        { SfxCategory.GameEvent, true }, { SfxCategory.UI, true },
        { SfxCategory.Combat, true },
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var bank = (SfxBank)target;
        var entriesProp = serializedObject.FindProperty("entries");

        EditorGUILayout.HelpBox(
            "按类别分区配置。新增音效：SfxId 枚举尾部加成员 → 这里对应分区添加条目 → 拖 clip。" +
            "\n留空 clip 的条目 = 静默（正常设计，正式音频到位后拖入即可）。",
            MessageType.Info);

        // 按类别收集条目（保持资产原顺序稳定）
        var byCategory = new Dictionary<SfxCategory, List<int>>();
        var duplicateIds = new HashSet<SfxId>();
        var seenIds = new HashSet<SfxId>();
        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            var idProp = entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("id");
            var id = (SfxId)idProp.intValue;   // 显式编号枚举：必须用 intValue（enumValueIndex 是索引，与值不同）
            if (id != SfxId.None && !seenIds.Add(id)) duplicateIds.Add(id);
            var cat = SfxBank.GetCategory(id);
            if (id == SfxId.None) continue;
            if (!byCategory.ContainsKey(cat)) byCategory[cat] = new List<int>();
            byCategory[cat].Add(i);
        }

        if (duplicateIds.Count > 0)
        {
            EditorGUILayout.HelpBox(
                "检测到重复音效 ID：" + string.Join(", ", new List<SfxId>(duplicateIds)) +
                "。运行时只读取同 ID 的第一条，请删除重复条目后再配置。",
                MessageType.Warning);
        }

        foreach (var cat in SectionOrder)
        {
            byCategory.TryGetValue(cat, out var list);
            int count = list != null ? list.Count : 0;
            if (!_foldouts.ContainsKey(cat)) _foldouts[cat] = true;
            _foldouts[cat] = EditorGUILayout.Foldout(_foldouts[cat], $"{SectionTitles[cat]}  ({count})", true);
            if (!_foldouts[cat]) continue;

            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox(SectionHints[cat], MessageType.None);
            if (list != null)
            {
                int removeIndex = -1;
                for (int n = 0; n < list.Count; n++)
                {
                    var e = entriesProp.GetArrayElementAtIndex(list[n]);
                    if (DrawEntry(e, list[n])) removeIndex = list[n];
                }
                if (removeIndex >= 0)
                    entriesProp.DeleteArrayElementAtIndex(removeIndex);
            }

            // 按固定 SfxId 提供缺失条目按钮，避免策划通过修改已有条目的 id 误造重复槽位。
            if (SectionIds.TryGetValue(cat, out var expectedIds))
            {
                foreach (var expectedId in expectedIds)
                {
                    if (FindFirstEntryIndex(entriesProp, expectedId) >= 0) continue;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(expectedId.ToString(), GUILayout.Width(170));
                    if (GUILayout.Button("＋ 添加此槽位"))
                        AddEntry(entriesProp, expectedId);
                    EditorGUILayout.EndHorizontal();
                }
            }

            if (GUILayout.Button($"＋ 在「{SectionTitles[cat]}」区新增自定义条目"))
                AddEntry(entriesProp, SfxId.None);
            EditorGUI.indentLevel--;
        }

        // 分类区可能在本轮 GUI 中删除条目，不能继续使用前面缓存的 uncategorized 索引。
        // 重新按当前 SerializedProperty 反向遍历，避免删除分类条目后发生数组越界。
        int currentUncategorizedCount = 0;
        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            if ((SfxId)entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("id").intValue == SfxId.None)
                currentUncategorizedCount++;
        }
        if (currentUncategorizedCount > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.Foldout(true, $"未分类（id=None，{currentUncategorizedCount} 条）", true);
            EditorGUI.indentLevel++;
            int removeIndex = -1;
            for (int i = entriesProp.arraySize - 1; i >= 0; i--)
            {
                var entry = entriesProp.GetArrayElementAtIndex(i);
                if ((SfxId)entry.FindPropertyRelative("id").intValue != SfxId.None) continue;
                if (DrawEntry(entry, i))
                {
                    removeIndex = i;
                    break;
                }
            }
            if (removeIndex >= 0)
                entriesProp.DeleteArrayElementAtIndex(removeIndex);
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }

    static int FindFirstEntryIndex(SerializedProperty entriesProp, SfxId id)
    {
        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            var entry = entriesProp.GetArrayElementAtIndex(i);
            if ((SfxId)entry.FindPropertyRelative("id").intValue == id)
                return i;
        }
        return -1;
    }

    static void AddEntry(SerializedProperty entriesProp, SfxId id)
    {
        entriesProp.arraySize++;
        var entry = entriesProp.GetArrayElementAtIndex(entriesProp.arraySize - 1);
        entry.FindPropertyRelative("id").intValue = (int)id;
        entry.FindPropertyRelative("volumeScale").floatValue = 1f;
        entry.FindPropertyRelative("minInterval").floatValue = 0f;
        entry.FindPropertyRelative("pitch").floatValue = 1f;
        entry.FindPropertyRelative("channel").enumValueIndex = 0;
        entry.FindPropertyRelative("prefer3D").boolValue = true;
    }

    /// <summary>绘制单条目：一行 = id 下拉 + clip 槽 + 删除；展开显示详细参数。</summary>
    bool DrawEntry(SerializedProperty e, int index)
    {
        var idProp = e.FindPropertyRelative("id");
        var clipProp = e.FindPropertyRelative("clip");
        bool remove = false;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        // id 下拉：按类别着色，让"哪类"一眼可辨
        var cat = SfxBank.GetCategory((SfxId)idProp.intValue);
        var oldColor = GUI.color;
        GUI.color = CategoryColor(cat);
        EditorGUILayout.PropertyField(idProp, GUIContent.none, GUILayout.Width(170));
        GUI.color = oldColor;
        EditorGUILayout.PropertyField(clipProp, GUIContent.none);
        if (GUILayout.Button("✕", GUILayout.Width(24)))
            remove = true;
        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel++;
        var vol = e.FindPropertyRelative("volumeScale");
        var iv = e.FindPropertyRelative("minInterval");
        var pitch = e.FindPropertyRelative("pitch");
        var ch = e.FindPropertyRelative("channel");
        var p3d = e.FindPropertyRelative("prefer3D");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(vol, new GUIContent("音量"));
        EditorGUILayout.PropertyField(pitch, new GUIContent("音高"));
        EditorGUILayout.PropertyField(iv, new GUIContent("间隔(s)"));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(ch, new GUIContent("通道"));
        EditorGUILayout.PropertyField(p3d, new GUIContent("3D 定位"));
        EditorGUILayout.EndHorizontal();
        EditorGUI.indentLevel--;

        EditorGUILayout.EndVertical();
        return remove;
    }

    static Color CategoryColor(SfxCategory cat)
    {
        switch (cat)
        {
            case SfxCategory.GameEvent: return new Color(0.7f, 1f, 0.7f);  // 绿
            case SfxCategory.UI: return new Color(0.75f, 0.85f, 1f);       // 蓝
            case SfxCategory.Combat: return new Color(1f, 0.8f, 0.7f);     // 橙红
            default: return Color.white;
        }
    }
}
