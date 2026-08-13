// =============================================================================
// JKPC - GUIStyles.cs
// 统一样式库
// =============================================================================

using UnityEditor;
using UnityEngine;

namespace JKPC.Editor.ShaderGUI.Utilities
{
    public static class GUIStyles
    {
        // 折叠组标题
        public static readonly GUIStyle FoldoutHeader = new GUIStyle(EditorStyles.foldoutHeader)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 12
        };

        // HelpBox 样式
        public static readonly GUIStyle HelpBox = new GUIStyle(EditorStyles.helpBox)
        {
            fontSize = 10,
            padding = new RectOffset(8, 8, 4, 4)
        };

        // 分割线
        public static void DrawSeparator()
        {
            EditorGUILayout.Space(4);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 1));
            EditorGUILayout.Space(4);
        }

        // 居中标签
        public static void CenteredLabel(string text)
        {
            EditorGUILayout.LabelField(text, new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 11
            });
        }
    }
}
