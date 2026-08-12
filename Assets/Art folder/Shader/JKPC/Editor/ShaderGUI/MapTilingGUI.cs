// =============================================================================
// JKPC - MapTilingGUI.cs
// 棋盘 Standard_Tiling 专用 GUI — 继承自 MapGUI，原文件不改动
// 在原有 Map 面板（表面 / 基础PBR / 自发光 / 渲染设置）下方追加
// Base Map 的 Tiling / Offset 控件
// =============================================================================

using UnityEditor;
using UnityEngine;
using JKPC.Editor.ShaderGUI.Utilities;

namespace JKPC.Editor.ShaderGUI
{
    public class MapTilingGUI : MapGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            // 先画原有 Map 面板（与 JKPC/Map/Standard 完全一致）
            base.OnGUI(materialEditor, properties);

            // 在底部追加 Tiling / Offset 控件
            var baseMap = PropertyHelper.FindProp("_BaseMap", properties);
            if (baseMap != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("纹理平铺 (Tiling)", EditorStyles.boldLabel);
                materialEditor.TextureScaleOffsetProperty(baseMap);
                EditorGUILayout.HelpBox(
                    "Tiling < 1 = 贴图被放大、细节变少（地面常用 0.5）\n" +
                    "Tiling > 1 = 贴图被缩小、细节变密",
                    MessageType.Info);
            }
        }
    }
}
