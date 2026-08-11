using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PandaPostProcessSwitcher))]
internal sealed class PandaPostProcessSwitcherEditor : Editor
{
    private SerializedProperty automaticallyFindEffectsProperty;
    private SerializedProperty effectsProperty;
    private SerializedProperty activeEffectIndexProperty;

    private void OnEnable()
    {
        automaticallyFindEffectsProperty = serializedObject.FindProperty("automaticallyFindEffects");
        effectsProperty = serializedObject.FindProperty("effects");
        activeEffectIndexProperty = serializedObject.FindProperty("activeEffectIndex");
    }

    public override void OnInspectorGUI()
    {
        PandaPostProcessSwitcher switcher = (PandaPostProcessSwitcher)target;
        serializedObject.Update();

        EditorGUILayout.LabelField("Panda 后期材质切换器", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "选择一个 Panda Post Process 作为当前效果，其他效果会自动关闭。",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(automaticallyFindEffectsProperty, new GUIContent("自动查找同物体效果"));
        if (!automaticallyFindEffectsProperty.boolValue)
        {
            EditorGUILayout.PropertyField(effectsProperty, new GUIContent("后期效果列表"), true);
        }

        bool configurationChanged = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();

        if (configurationChanged)
        {
            RecordControlledObjects(switcher, "修改后期效果列表");
            if (switcher.AutomaticallyFindEffects)
            {
                switcher.RefreshEffects();
            }

            switcher.ApplyCurrentSelection();
            EditorUtility.SetDirty(switcher);
        }

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("刷新效果列表"))
            {
                RecordControlledObjects(switcher, "刷新后期效果列表");
                switcher.RefreshEffects();
                switcher.ApplyCurrentSelection();
                EditorUtility.SetDirty(switcher);
            }

            if (GUILayout.Button("同步当前启用项"))
            {
                RecordControlledObjects(switcher, "同步当前后期效果");
                switcher.UseCurrentlyEnabledEffect();
                EditorUtility.SetDirty(switcher);
            }
        }

        DrawEffectSelector(switcher);
    }

    private void DrawEffectSelector(PandaPostProcessSwitcher switcher)
    {
        IReadOnlyList<PandaPostProcess> effects = switcher.Effects;
        if (effects.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "没有找到 Panda Post Process。请把切换器与后期脚本放在同一个 GameObject 上，然后刷新列表。",
                MessageType.Warning);
            return;
        }

        GUIContent[] options = new GUIContent[effects.Count + 1];
        options[0] = new GUIContent("关闭全部后期材质");
        for (int i = 0; i < effects.Count; i++)
        {
            options[i + 1] = new GUIContent($"{i + 1}. {GetEffectName(effects[i])}");
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("当前效果", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        int popupIndex = EditorGUILayout.Popup(
            new GUIContent("启用后期材质"),
            switcher.ActiveEffectIndex + 1,
            options);
        if (EditorGUI.EndChangeCheck())
        {
            SetSelection(switcher, popupIndex - 1, "切换后期材质");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("上一个", GUILayout.Height(26f)))
            {
                RecordControlledObjects(switcher, "切换到上一个后期材质");
                switcher.SelectPreviousEffect();
                EditorUtility.SetDirty(switcher);
            }

            if (GUILayout.Button("下一个", GUILayout.Height(26f)))
            {
                RecordControlledObjects(switcher, "切换到下一个后期材质");
                switcher.SelectNextEffect();
                EditorUtility.SetDirty(switcher);
            }

            if (GUILayout.Button("全部关闭", GUILayout.Height(26f)))
            {
                SetSelection(switcher, -1, "关闭全部后期材质");
            }
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("快捷选择", EditorStyles.boldLabel);

        for (int i = 0; i < effects.Count; i++)
        {
            PandaPostProcess effect = effects[i];
            bool isSelected = i == switcher.ActiveEffectIndex;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                using (new EditorGUI.DisabledScope(isSelected || effect == null))
                {
                    if (GUILayout.Button(isSelected ? "已启用" : "启用", GUILayout.Width(58f)))
                    {
                        SetSelection(switcher, i, "切换后期材质");
                    }
                }

                EditorGUILayout.LabelField($"{i + 1}. {GetEffectName(effect)}");

                using (new EditorGUI.DisabledScope(effect == null))
                {
                    if (GUILayout.Button("定位", GUILayout.Width(44f)))
                    {
                        Selection.activeObject = effect;
                        EditorGUIUtility.PingObject(effect);
                    }
                }
            }
        }

        if (activeEffectIndexProperty.intValue != switcher.ActiveEffectIndex)
        {
            serializedObject.Update();
            activeEffectIndexProperty.intValue = switcher.ActiveEffectIndex;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetSelection(
        PandaPostProcessSwitcher switcher,
        int index,
        string undoName)
    {
        RecordControlledObjects(switcher, undoName);
        switcher.SetActiveEffect(index);
        EditorUtility.SetDirty(switcher);

        foreach (PandaPostProcess effect in switcher.Effects)
        {
            if (effect != null)
            {
                EditorUtility.SetDirty(effect);
            }
        }
    }

    private static void RecordControlledObjects(PandaPostProcessSwitcher switcher, string undoName)
    {
        List<Object> objects = new List<Object> { switcher };
        foreach (PandaPostProcess effect in switcher.Effects)
        {
            if (effect != null)
            {
                objects.Add(effect);
            }
        }

        Undo.RecordObjects(objects.ToArray(), undoName);
    }

    private static string GetEffectName(PandaPostProcess effect)
    {
        if (effect == null)
        {
            return "缺失引用";
        }

        return effect.PostProcessMat != null
            ? effect.PostProcessMat.name
            : $"{effect.GetType().Name}（未指定材质）";
    }
}
