// =============================================================================
// JKPC - PropertyHelper.cs
// 属性辅助工具
// =============================================================================

using UnityEditor;
using UnityEngine;

namespace JKPC.Editor.ShaderGUI.Utilities
{
    public static class PropertyHelper
    {
        /// <summary>
        /// 查找材质属性 (安全版本，找不到返回null)
        /// </summary>
        public static MaterialProperty FindProp(string name, MaterialProperty[] properties)
        {
            return System.Array.Find(properties, p => p.name == name);
        }

        /// <summary>
        /// 绘制纹理+右侧滑条（单行布局）
        /// </summary>
        public static void TextureWithSlider(
            MaterialEditor editor,
            MaterialProperty texProp,
            MaterialProperty sliderProp,
            string label)
        {
            editor.TexturePropertySingleLine(
                new GUIContent(label, sliderProp?.displayName ?? ""),
                texProp,
                sliderProp
            );
        }

        /// <summary>
        /// 绘制 Toggle + Keyword 联动
        /// </summary>
        public static bool DrawToggle(
            MaterialEditor editor,
            MaterialProperty toggleProp,
            string label,
            string keyword)
        {
            if (toggleProp == null) return false;

            EditorGUI.BeginChangeCheck();
            editor.ShaderProperty(toggleProp, label);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var obj in editor.targets)
                {
                    Material mat = (Material)obj;
                    if (toggleProp.floatValue > 0.5f)
                        mat.EnableKeyword(keyword);
                    else
                        mat.DisableKeyword(keyword);
                }
            }
            return toggleProp.floatValue > 0.5f;
        }

        /// <summary>
        /// 绘制权重滑条 (0~2) 带 Tooltip
        /// </summary>
        public static void DrawWeightSlider(
            MaterialEditor editor,
            MaterialProperty prop,
            string label,
            string tooltip = "0=不接收, 1=全局原值, 2=加倍")
        {
            if (prop == null) return;
            editor.ShaderProperty(prop, new GUIContent(label, tooltip));
        }
    }
}
