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
        var uncategorized = new List<int>();
        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            var idProp = entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("id");
            var id = (SfxId)idProp.intValue;   // 显式编号枚举：必须用 intValue（enumValueIndex 是索引，与值不同）
            var cat = SfxBank.GetCategory(id);
            if (id == SfxId.None) { uncategorized.Add(i); continue; }
            if (!byCategory.ContainsKey(cat)) byCategory[cat] = new List<int>();
            byCategory[cat].Add(i);
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
            // 本区新增条目按钮：默认 id=None，选择后即归入对应区（id 决定分区，不强制指定）
            if (GUILayout.Button($"＋ 在「{SectionTitles[cat]}」区新增条目"))
            {
                entriesProp.arraySize++;
                var e = entriesProp.GetArrayElementAtIndex(entriesProp.arraySize - 1);
                e.FindPropertyRelative("id").intValue = (int)SfxId.None;
                e.FindPropertyRelative("volumeScale").floatValue = 1f;
                e.FindPropertyRelative("pitch").floatValue = 1f;
                e.FindPropertyRelative("channel").enumValueIndex = 0;
                e.FindPropertyRelative("prefer3D").boolValue = true;
            }
            EditorGUI.indentLevel--;
        }

        if (uncategorized.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.Foldout(true, $"未分类（id=None，{uncategorized.Count} 条）", true);
            EditorGUI.indentLevel++;
            int removeIndex = -1;
            foreach (var i in uncategorized)
                if (DrawEntry(entriesProp.GetArrayElementAtIndex(i), i))
                    removeIndex = i;
            if (removeIndex >= 0)
                entriesProp.DeleteArrayElementAtIndex(removeIndex);
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
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
