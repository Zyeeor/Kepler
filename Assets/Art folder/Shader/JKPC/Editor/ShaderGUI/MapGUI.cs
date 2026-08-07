// =============================================================================
// JKPC - MapGUI.cs
// 棋盘专用GUI
// 面板: Base → Emission → Rendering (无CharacterLighting)
// 规范 → 文档 §5.3
// =============================================================================


using UnityEditor;
using UnityEngine;
using JKPC.Editor.ShaderGUI.Utilities;

namespace JKPC.Editor.ShaderGUI
{
    public class MapGUI : JKPCBaseGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            DrawSurfaceOptions(materialEditor, properties);
            DrawBaseProperties(materialEditor, properties);
            DrawEmissionProperties(materialEditor, properties);
            DrawRenderingSettings(materialEditor, properties);
            // ★ Map Standard 仅保留基础 PBR + Emission

        }

    }
}

