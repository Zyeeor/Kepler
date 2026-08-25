using UnityEditor;
using UnityEngine;

/// <summary>
/// WaveConfig 检查器绘制器：条件显示依赖 WaveManager 的整体 waveMode——
///   CountKill 数量波 → 显示 totalCount，隐藏 duration/maxSpawnCount；
///   Timed 时间波 → 显示 duration + maxSpawnCount，隐藏 totalCount。
/// weightedTable 始终显示。
/// </summary>
[CustomPropertyDrawer(typeof(WaveConfig))]
public class WaveConfigDrawer : PropertyDrawer
{
    /// <summary>整体模式从 WaveManager 宿主对象读取（不在 WaveConfig 内）。</summary>
    static bool IsTimed(SerializedProperty property)
    {
        var mode = property.serializedObject.FindProperty("waveMode");
        return mode != null && mode.enumValueIndex == (int)WaveMode.Timed;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        // 标题行（带折叠箭头，需与默认绘制保持一致）
        var titleRect = new Rect(line.x, line.y, line.width, line.height);
        property.isExpanded = EditorGUI.Foldout(titleRect, property.isExpanded, label, true);
        line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        if (property.isExpanded)
        {
            // weightedTable 始终显示
            var table = property.FindPropertyRelative("weightedTable");
            float tableHeight = EditorGUI.GetPropertyHeight(table, true);
            EditorGUI.PropertyField(line, table, true);
            line.y += tableHeight + EditorGUIUtility.standardVerticalSpacing;

            // 条件字段：整体模式 Timed → duration + maxSpawnCount；CountKill → totalCount
            if (IsTimed(property))
            {
                EditorGUI.PropertyField(line, property.FindPropertyRelative("duration"));
                line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.PropertyField(line, property.FindPropertyRelative("maxSpawnCount"));
            }
            else
            {
                EditorGUI.PropertyField(line, property.FindPropertyRelative("totalCount"));
            }
            line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // doublePick 恒显示（与模式无关）
            EditorGUI.PropertyField(line, property.FindPropertyRelative("doublePick"));
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;

        float h = EditorGUIUtility.singleLineHeight; // 标题行
        h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("weightedTable"), true)
             + EditorGUIUtility.standardVerticalSpacing; // weightedTable
        h += (IsTimed(property) ? 2 : 1) * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing); // duration+maxSpawnCount / totalCount
        h += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; // doublePick
        return h;
    }
}
