using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [SfxIdName] 字段的自定义绘制器：string 字段（存储 SfxId 成员名）显示为下拉框。
/// 策划在能力预制体上配置 castAudioName/hitAudioName 时免背枚举名、免拼写错误；
/// 存储仍是 string（序列化零迁移，CombatAudioManager.Play(name) 解析路径不变）。
/// 值不在枚举列表（历史值/自定义）时原样显示并标红提示，不丢数据。
/// </summary>
[CustomPropertyDrawer(typeof(SfxIdNameAttribute))]
public class SfxIdNameDrawer : PropertyDrawer
{
    static string[] _options;
    static Dictionary<string, int> _nameToIndex;

    static void EnsureOptions()
    {
        if (_options != null) return;
        var names = Enum.GetNames(typeof(SfxId));
        var list = new List<string>(names.Length);
        foreach (var n in names)
            if (n != nameof(SfxId.None))   // None 是"静默"语义，由首选项表达
                list.Add(n);
        _options = new string[list.Count + 1];
        _nameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _options[0] = "（静默：不播放）";
        for (int i = 0; i < list.Count; i++)
        {
            _options[i + 1] = list[i];
            _nameToIndex[list[i]] = i + 1;
        }
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EnsureOptions();
        string current = property.stringValue;

        int index = 0;
        bool known = string.IsNullOrEmpty(current);
        if (!known)
        {
            if (_nameToIndex.TryGetValue(current.Trim(), out int idx))
            {
                index = idx;
                known = true;
            }
        }

        EditorGUI.BeginProperty(position, label, property);
        position = EditorGUI.PrefixLabel(position, label);

        if (!known)
        {
            // 未知值：显示红色提示 + 原字符串（保留数据，策划可改选）
            var warnRect = new Rect(position.x, position.y, 22, position.height);
            var oldColor = GUI.color;
            GUI.color = Color.red;
            EditorGUI.LabelField(warnRect, "⚠");
            GUI.color = oldColor;
            position.x += 24;
            position.width -= 24;
        }

        int newIndex = EditorGUI.Popup(position, index, _options);
        // 仅当选择实际变化时写回：未知历史值（显示"⚠原值"）在策划未改动选择时不被清空
        if (newIndex != index)
        {
            property.stringValue = newIndex == 0 ? "" : _options[newIndex];
        }
        EditorGUI.EndProperty();
    }
}
