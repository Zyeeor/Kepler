using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomPropertyDrawer(typeof(AiConfigIdAttribute))]
public class AiConfigIdDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var aiConfigProp = property.serializedObject.FindProperty("aiConfig");
        var aiConfig = aiConfigProp != null ? aiConfigProp.objectReferenceValue as MonsterAIConfig : null;
        if (aiConfig == null || aiConfig.entries == null || aiConfig.entries.Count == 0)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var validIds = aiConfig.entries
            .Where(e => e != null && !string.IsNullOrEmpty(e.id))
            .Select(e => e.id)
            .ToList();

        int index = validIds.IndexOf(property.stringValue);
        var display = validIds.ToList();
        int selIndex = index;
        if (index < 0)
        {
            string current = string.IsNullOrEmpty(property.stringValue) ? "<默认>" : property.stringValue + " (未命中)";
            display.Insert(0, current);
            selIndex = 0;
        }

        int picked = EditorGUI.Popup(position, label.text, selIndex, display.ToArray());
        if (picked == selIndex && index < 0) return; // 保持当前（可能无效的）值不变
        property.stringValue = display[picked];
    }
}
