using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Custom drawer for <see cref="HitFeedbackParams"/>.
/// The free-text "Post Process Effect Name" becomes a dropdown that lists ONLY the effects
/// actually wired onto a <see cref="PandaPostProcessSwitcher"/> in the currently open scene(s),
/// shown with a human-readable display name. "所见即可用" — if it's in the list, it can play.
/// The value stored in the property is STILL the material name, because
/// <c>CombatEffectManager.NamesMatch</c> matches by material / GameObject name at runtime.
/// A "自定义…" fallback keeps manual entry available for GameObject-name based setups
/// (or when editing a prefab while the target scene is not open).
/// </summary>
[CustomPropertyDrawer(typeof(HitFeedbackParams))]
public sealed class HitFeedbackParamsDrawer : PropertyDrawer
{
    private const string CustomOption = "自定义…";
    private const string NoneOption = "(未选择)";

    /// <summary>
    /// Material name -> designer-facing effect display name.
    /// Only lists the effects currently wired into combat scenes. Add a new row here when art
    /// wires a new PandaPostProcess onto the scene's GlobalVolume / PandaPostProcessSwitcher.
    /// Keep this in sync with docs/战斗表现管理-策划配置说明.md.
    /// Wired materials not listed here fall back to showing their raw material name.
    /// </summary>
    private static readonly Dictionary<string, string> EffectDisplayNames = new Dictionary<string, string>
    {
        { "WhiteFlash", "白闪—爆发/受击" },
        { "BlackFlash", "黑闪—重击/顿感" },
        { "RadialBlur", "径向模糊—冲刺/爆炸" },
    };

    private static string[] _cachedNames;
    private static double _lastRefreshTime;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        property.isExpanded = EditorGUI.Foldout(
            new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
            property.isExpanded, label, true);

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        SerializedProperty child = property.Copy();
        SerializedProperty end = property.GetEndProperty();
        bool enterChildren = true;

        while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
        {
            enterChildren = false;

            float height = EditorGUI.GetPropertyHeight(child, true);
            Rect rect = new Rect(position.x, y, position.width, height);

            if (child.name == "postProcessEffectName")
                DrawEffectNamePopup(rect, child);
            else
                EditorGUI.PropertyField(rect, child, true);

            y += height + EditorGUIUtility.standardVerticalSpacing;
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    private static void DrawEffectNamePopup(Rect rect, SerializedProperty nameProperty)
    {
        // materialNames[i] is the stored value; displayLabels[i] is what the designer sees.
        string[] materialNames = GetPandaMaterialNames();

        // Build parallel lists: [(未选择), <effects...>, 自定义…]
        List<string> storedValues = new List<string>(materialNames.Length + 2) { string.Empty };
        List<string> displayLabels = new List<string>(materialNames.Length + 2) { NoneOption };
        for (int i = 0; i < materialNames.Length; i++)
        {
            storedValues.Add(materialNames[i]);
            displayLabels.Add(GetDisplayLabel(materialNames[i]));
        }
        storedValues.Add(CustomOption);
        displayLabels.Add(CustomOption);

        string current = nameProperty.stringValue;
        bool isKnown = !string.IsNullOrEmpty(current) && System.Array.IndexOf(materialNames, current) >= 0;
        bool isEmpty = string.IsNullOrEmpty(current);

        // A per-property flag so "自定义…" stays selected even while its text is empty.
        string customKey = "HitFeedbackParams.custom." + nameProperty.propertyPath;
        bool customSticky = SessionState.GetBool(customKey, false);

        int customIndex = storedValues.Count - 1;
        int selectedIndex;
        if (isKnown) selectedIndex = storedValues.IndexOf(current);
        else if (!isEmpty || customSticky) selectedIndex = customIndex; // non-material text or sticky custom
        else selectedIndex = 0;                                         // (未选择)

        var label = new GUIContent(
            "命中后处理效果",
            "仅列出当前打开场景里已接线（挂在 PandaPostProcessSwitcher 上）的效果。" +
            "列表为空说明当前未打开含后期的场景，可打开对应场景，或选“自定义…”手填材质/物体名。");

        // Hint the designer when nothing is wired in the open scene(s).
        if (materialNames.Length == 0)
            displayLabels[0] = "(无接线效果·可选自定义)";

        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUI.Popup(rect, label, selectedIndex, ToGuiContents(displayLabels));
        if (EditorGUI.EndChangeCheck())
        {
            if (newIndex == 0)
            {
                nameProperty.stringValue = string.Empty;          // (未选择)
                SessionState.SetBool(customKey, false);
            }
            else if (newIndex == customIndex)
            {
                SessionState.SetBool(customKey, true);            // switch to custom; keep existing text
            }
            else
            {
                nameProperty.stringValue = storedValues[newIndex]; // store the MATERIAL NAME, not the label
                SessionState.SetBool(customKey, false);
            }
        }

        bool showCustomField = selectedIndex == customIndex || newIndex == customIndex;
        if (showCustomField)
        {
            Rect customRect = new Rect(rect.x, rect.y + rect.height + EditorGUIUtility.standardVerticalSpacing,
                rect.width, EditorGUIUtility.singleLineHeight);
            nameProperty.stringValue = EditorGUI.TextField(customRect, "  自定义名称(材质/物体名)", nameProperty.stringValue);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        float total = line; // foldout line
        SerializedProperty child = property.Copy();
        SerializedProperty end = property.GetEndProperty();
        bool enterChildren = true;

        while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
        {
            enterChildren = false;
            total += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;

            if (child.name == "postProcessEffectName")
            {
                string current = child.stringValue;
                bool isKnown = !string.IsNullOrEmpty(current)
                    && System.Array.IndexOf(GetPandaMaterialNames(), current) >= 0;
                bool isEmpty = string.IsNullOrEmpty(current);
                bool customSticky = SessionState.GetBool(
                    "HitFeedbackParams.custom." + child.propertyPath, false);
                if ((!isKnown && !isEmpty) || customSticky)
                    total += line; // extra custom text field
            }
        }

        return total;
    }

    /// <summary>Designer-facing label for a material. Falls back to the raw name when unmapped.</summary>
    private static string GetDisplayLabel(string materialName)
    {
        if (!string.IsNullOrEmpty(materialName) &&
            EffectDisplayNames.TryGetValue(materialName, out string display))
            return display;
        return materialName; // unmapped material: show its own name so it is never hidden
    }

    private static GUIContent[] ToGuiContents(List<string> values)
    {
        var result = new GUIContent[values.Count];
        for (int i = 0; i < values.Count; i++)
            result[i] = new GUIContent(values[i]);
        return result;
    }

    /// <summary>
    /// Collects the effect names actually wired onto every <see cref="PandaPostProcessSwitcher"/>
    /// in the currently open scene(s). Prefers material name (what CombatEffectManager matches first
    /// via material), then falls back to the GameObject name. Cached briefly to avoid re-scanning
    /// the scene on every repaint.
    /// </summary>
    private static string[] GetPandaMaterialNames()
    {
        double now = EditorApplication.timeSinceStartup;
        if (_cachedNames != null && now - _lastRefreshTime < 2.0)
            return _cachedNames;

        var names = new List<string>();

        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded)
                continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var switchers = root.GetComponentsInChildren<PandaPostProcessSwitcher>(true);
                foreach (var switcher in switchers)
                {
                    if (switcher == null)
                        continue;

                    switcher.RefreshEffects();
                    IReadOnlyList<PandaPostProcess> effects = switcher.Effects;
                    for (int i = 0; i < effects.Count; i++)
                    {
                        PandaPostProcess effect = effects[i];
                        if (effect == null)
                            continue;

                        // Match order mirrors CombatEffectManager.NamesMatch: material name first.
                        string effectName = effect.PostProcessMat != null
                            ? effect.PostProcessMat.name
                            : effect.name;

                        if (!string.IsNullOrEmpty(effectName) && !names.Contains(effectName))
                            names.Add(effectName);
                    }
                }
            }
        }

        names.Sort(System.StringComparer.OrdinalIgnoreCase);
        _cachedNames = names.ToArray();
        _lastRefreshTime = now;
        return _cachedNames;
    }
}
