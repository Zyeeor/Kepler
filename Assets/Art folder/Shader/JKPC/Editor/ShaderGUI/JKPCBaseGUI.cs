// =============================================================================
// JKPC - JKPCBaseGUI.cs
// 基础GUI类 (所有GUI继承此类)
// 规范 → 文档 §5.1, §5.2
// =============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using JKPC.Editor.ShaderGUI.Utilities;

namespace JKPC.Editor.ShaderGUI
{
    public abstract class JKPCBaseGUI : UnityEditor.ShaderGUI
    {
        // ===== SurfaceType / BlendMode 枚举 =====
        protected enum SurfaceType { Opaque = 0, AlphaTest = 1, Transparent = 2 }
        protected enum BlendType  { Alpha = 0, Additive = 1, Premultiply = 2 }

        // =================================================================
        // Feature 自动发现机制
        // 子类 override GetFeatureDrawers() 注册特性面板
        // GUI 通过探测属性是否存在来自动显示/隐藏对应面板
        // =================================================================
        protected struct FeatureDrawerEntry
        {
            public string probeProperty;   // 探测属性名 — shader中存在此属性则显示面板
            public string foldoutTitle;    // 面板折叠标题
            public string foldoutKey;      // EditorPrefs 折叠状态 key
            public System.Action<MaterialEditor, MaterialProperty[]> drawAction; // 绘制方法
        }

        /// <summary>
        /// 子类 override 此方法注册特性面板。
        /// 每个 FeatureDrawerEntry 定义一个可选特性模块。
        /// </summary>
        protected virtual List<FeatureDrawerEntry> GetFeatureDrawers() => new List<FeatureDrawerEntry>();

        /// <summary>
        /// 遍历所有已注册的特性，按属性探测自动显示对应面板。
        /// 在 OnGUI 中 Emission 之后、Rendering 之前调用。
        /// </summary>
        protected void DrawFeatureModules(MaterialEditor editor, MaterialProperty[] props)
        {
            foreach (var entry in GetFeatureDrawers())
            {
                if (PropertyHelper.FindProp(entry.probeProperty, props) == null)
                    continue;

                if (Foldout(entry.foldoutTitle, entry.foldoutKey))
                {
                    EditorGUI.indentLevel++;
                    entry.drawAction(editor, props);
                    EditorGUI.indentLevel--;
                }
            }
        }

        // 折叠状态管理
        protected bool Foldout(string title, string key, bool defaultOpen = true)
        {
            string prefsKey = $"JKPC_Foldout_{key}";
            bool state = EditorPrefs.GetBool(prefsKey, defaultOpen);
            bool newState = EditorGUILayout.Foldout(state, title, true, GUIStyles.FoldoutHeader);
            if (newState != state)
                EditorPrefs.SetBool(prefsKey, newState);
            return newState;
        }

        // ===== 表面选项 (顶部) =====
        protected void DrawSurfaceOptions(MaterialEditor editor, MaterialProperty[] props)
        {
            var surfaceProp = PropertyHelper.FindProp("_Surface", props);
            var blendProp   = PropertyHelper.FindProp("_Blend", props);
            var cullProp    = PropertyHelper.FindProp("_CullMode", props);
            var cutoffProp  = PropertyHelper.FindProp("_Cutoff", props);

            if (surfaceProp == null) return; // Effect等无此属性则跳过

            EditorGUILayout.LabelField("表面选项", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            // --- Surface Type ---
            EditorGUI.BeginChangeCheck();
            var surface = (SurfaceType)(int)surfaceProp.floatValue;
            surface = (SurfaceType)EditorGUILayout.EnumPopup("Surface Type", surface);
            if (EditorGUI.EndChangeCheck())
            {
                editor.RegisterPropertyChangeUndo("Surface Type");
                surfaceProp.floatValue = (float)surface;
                foreach (var obj in editor.targets)
                    ApplySurfaceType((Material)obj, surface);
            }

            // --- ZWrite + Blend Mode (仅Transparent) ---
            if (surface == SurfaceType.Transparent && blendProp != null)
            {
                var zwriteProp = PropertyHelper.FindProp("_ZWrite", props);
                if (zwriteProp != null)
                {
                    EditorGUI.BeginChangeCheck();
                    bool zwriteOn = zwriteProp.floatValue > 0.5f;
                    zwriteOn = EditorGUILayout.Toggle(
                        new GUIContent("ZWrite", "透明物体是否写入深度：\n" +
                            "Off = 不写深度，避免透明区域遮挡自身；\n" +
                            "On  = 写深度，用于需要深度遮挡关系的半透明材质。"),
                        zwriteOn);
                    if (EditorGUI.EndChangeCheck())
                    {
                        editor.RegisterPropertyChangeUndo("ZWrite");
                        zwriteProp.floatValue = zwriteOn ? 1f : 0f;
                    }
                }

                EditorGUI.BeginChangeCheck();
                var blend = (BlendType)(int)blendProp.floatValue;
                blend = (BlendType)EditorGUILayout.EnumPopup("Blend Mode", blend);
                if (EditorGUI.EndChangeCheck())
                {
                    editor.RegisterPropertyChangeUndo("Blend Mode");
                    blendProp.floatValue = (float)blend;
                    foreach (var obj in editor.targets)
                        ApplyBlendMode((Material)obj, blend);
                }
            }

            // --- Cull Mode ---
            if (cullProp != null)
                editor.ShaderProperty(cullProp, "Cull Mode");

            // --- Alpha Cutoff (仅AlphaTest) ---
            if (surface == SurfaceType.AlphaTest && cutoffProp != null)
            {
                editor.ShaderProperty(cutoffProp, "Alpha Cutoff");
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // --- 联动: SurfaceType 改变时设置材质状态 ---
        protected static void ApplySurfaceType(Material mat, SurfaceType surface)
        {
            float queueOffset = mat.HasProperty("_QueueOffset") ? mat.GetFloat("_QueueOffset") : 0;

            switch (surface)
            {
                case SurfaceType.Opaque:
                    mat.SetFloat("_SrcBlend", (float)BlendMode.One);
                    mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
                    mat.SetFloat("_ZWrite", 1);
                    mat.SetOverrideTag("RenderType", "Opaque");
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    // Hero 系列棋子 +200 推后绘制（用于角色后绘制保证地形不穿插的渲染顺序约定）
                    // 其它族（Map / Effect / LittleLegend / Ink 等）保持 URP 默认 Geometry queue
                    {
                        bool isHero = mat.shader != null && mat.shader.name.Contains("/Hero/");
                        int heroOffset = isHero ? 200 : 0;
                        mat.renderQueue = (int)RenderQueue.Geometry + heroOffset + (int)queueOffset;
                    }
                    if (mat.HasProperty("_AlphaToMask")) mat.SetFloat("_AlphaToMask", 0f);
                    break;

                case SurfaceType.AlphaTest:
                    mat.SetFloat("_SrcBlend", (float)BlendMode.One);
                    mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
                    mat.SetFloat("_ZWrite", 1);
                    mat.SetOverrideTag("RenderType", "TransparentCutout");
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = (int)RenderQueue.AlphaTest + (int)queueOffset;
                    // AlphaTest 默认开启 A2C（MSAA 下 sub-pixel 软化边缘；关 MSAA 时等价于普通 cutout）
                    if (mat.HasProperty("_AlphaToMask")) mat.SetFloat("_AlphaToMask", 1f);
                    break;

                case SurfaceType.Transparent:
                    // Transparent 默认不写 Z；如需深度遮挡关系，可在 GUI 的 ZWrite 开关里手动打开。
                    mat.SetFloat("_ZWrite", 0);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    float blendVal = mat.HasProperty("_Blend") ? mat.GetFloat("_Blend") : 0;
                    ApplyBlendMode(mat, (BlendType)(int)blendVal);
                    mat.renderQueue = (int)RenderQueue.Transparent + (int)queueOffset;
                    if (mat.HasProperty("_AlphaToMask")) mat.SetFloat("_AlphaToMask", 0f);
                    break;
            }
        }

        // --- 联动: BlendMode 改变时设置 Src/Dst ---
        protected static void ApplyBlendMode(Material mat, BlendType blend)
        {
            switch (blend)
            {
                case BlendType.Alpha:       // SrcAlpha / OneMinusSrcAlpha
                    mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    break;
                case BlendType.Additive:    // One / One
                    mat.SetFloat("_SrcBlend", (float)BlendMode.One);
                    mat.SetFloat("_DstBlend", (float)BlendMode.One);
                    break;
                case BlendType.Premultiply: // One / OneMinusSrcAlpha
                    mat.SetFloat("_SrcBlend", (float)BlendMode.One);
                    mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    break;
            }
        }

        // ===== 标准PBR属性组 =====
        protected void DrawBaseProperties(MaterialEditor editor, MaterialProperty[] props)
        {
            if (Foldout("基础PBR", "BasePBR"))
            {
                EditorGUI.indentLevel++;

                var baseMap = PropertyHelper.FindProp("_BaseMap", props);
                var baseColor = PropertyHelper.FindProp("_BaseColor", props);
                var normalMap = PropertyHelper.FindProp("_NormalMap", props);
                var normalScale = PropertyHelper.FindProp("_NormalScale", props);
                var metallicGlossMap = PropertyHelper.FindProp("_MetallicGlossMap", props);
                var metallic = PropertyHelper.FindProp("_Metallic", props);
                var smoothness = PropertyHelper.FindProp("_Smoothness", props);
                var aoIntensity = PropertyHelper.FindProp("_AOIntensity", props);

                if (baseMap != null)
                    editor.TexturePropertySingleLine(new GUIContent("Base Map"), baseMap, baseColor);
                if (normalMap != null)
                    PropertyHelper.TextureWithSlider(editor, normalMap, normalScale, "Normal Map");
                if (metallicGlossMap != null)
                    editor.TexturePropertySingleLine(new GUIContent("Metallic Gloss Map"), metallicGlossMap);
                if (metallic != null)
                    editor.ShaderProperty(metallic, "Metallic");
                if (smoothness != null)
                    editor.ShaderProperty(smoothness, "Smoothness");
                if (aoIntensity != null)
                    editor.ShaderProperty(aoIntensity, "AO Intensity");

                EditorGUI.indentLevel--;
            }
        }

        // ===== 自发光属性组 (含Toggle) =====
        protected void DrawEmissionProperties(MaterialEditor editor, MaterialProperty[] props)
        {
            if (Foldout("自发光", "Emission"))
            {
                EditorGUI.indentLevel++;

                var emissionEnabled = PropertyHelper.FindProp("_EmissionEnabled", props);
                bool enabled = PropertyHelper.DrawToggle(editor, emissionEnabled, "Enable Emission", "_EMISSION_ON");

                if (enabled)
                {
                    var emissionMap = PropertyHelper.FindProp("_EmissionMap", props);
                    var emissionColor = PropertyHelper.FindProp("_EmissionColor", props);
                    var emissionIntensity = PropertyHelper.FindProp("_EmissionIntensity", props);

                    if (emissionMap != null)
                        editor.TexturePropertySingleLine(new GUIContent("Emission Map"), emissionMap, emissionColor);
                    if (emissionIntensity != null)
                        editor.ShaderProperty(emissionIntensity, "Emission Intensity");
                }

                EditorGUI.indentLevel--;
            }
        }

        // ===== CharacterLighting权重滑条组 (5个权重，无Toggle开关) =====
        protected void DrawCharacterLightingWeights(MaterialEditor editor, MaterialProperty[] props)
        {
            if (Foldout("角色光照权重", "CLWeights"))
            {
                EditorGUI.indentLevel++;

                var wH  = PropertyHelper.FindProp("_CLWeightHemisphere", props);
                var wS  = PropertyHelper.FindProp("_CLWeightSpecular", props);
                var wR  = PropertyHelper.FindProp("_CLWeightRim", props);
                var wR2 = PropertyHelper.FindProp("_CLWeightRim2", props);
                var wC  = PropertyHelper.FindProp("_CLWeightCameraLight", props);

                PropertyHelper.DrawWeightSlider(editor, wH,  "天光 (Hemisphere)");
                PropertyHelper.DrawWeightSlider(editor, wS,  "程序化金属反射 (FinalMetalReflection)");
                PropertyHelper.DrawWeightSlider(editor, wR,  "边缘光 1 (Rim Light)");

                if (wR2 != null)
                    PropertyHelper.DrawWeightSlider(editor, wR2, "边缘光 2 (Rim Light 2)");
                PropertyHelper.DrawWeightSlider(editor, wC,  "镜头光 (Camera Light)");


                // Rim 聚集度（材质下放，乘法缩放全局 rimPower）
                // 仅 shader 声明了 _RimPowerScale / _Rim2PowerScale 才会出现
                var rimPS  = PropertyHelper.FindProp("_RimPowerScale", props);
                var rim2PS = PropertyHelper.FindProp("_Rim2PowerScale", props);
                if (rimPS != null || rim2PS != null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("── 边缘光聚集度（材质下放，乘全局 rimPower）──", EditorStyles.boldLabel);
                    if (rimPS  != null) editor.ShaderProperty(rimPS,  new GUIContent("Rim1 聚集度缩放", "= CharacterLighting 全局 rimPower × 该值。1=使用全局；>1 更收紧；<1 更发散。"));
                    if (rim2PS != null) editor.ShaderProperty(rim2PS, new GUIContent("Rim2 聚集度缩放", "= CharacterLighting 全局 rim2Power × 该值。1=使用全局；>1 更收紧；<1 更发散。"));
                }

                EditorGUILayout.HelpBox(
                    "全局参数由 CharacterLighting 组件控制\n" +
                    "权重=0 可关闭该项视觉效果\n" +
                    "其中“程序化金属反射”当前只保留 FinalMetalReflection，Cubemap 已下线。",
                    MessageType.Info);

                if (GUILayout.Button("全部重置为1.0"))
                {
                    if (wH  != null) wH.floatValue  = 1.0f;
                    if (wS  != null) wS.floatValue  = 1.0f;
                    if (wR  != null) wR.floatValue  = 1.0f;
                    if (wR2 != null) wR2.floatValue = 1.0f;
                    if (wC  != null) wC.floatValue  = 1.0f;

                    if (rimPS  != null) rimPS.floatValue  = 1.0f;
                    if (rim2PS != null) rim2PS.floatValue = 1.0f;
                }


                EditorGUI.indentLevel--;
            }
        }


        // ★ v5.2 边缘光轴向衰减 (模型空间) — 仅 Hero Standard / NPRStandard 有此参数
        // 方向复用 CharacterLighting 全局的 Rim Dir, 材质球只控制衰减半径
        protected void DrawRimAxisFalloff(MaterialEditor editor, MaterialProperty[] props)
        {
            var rimRadius  = PropertyHelper.FindProp("_CLRimAxisRadius", props);
            var rim2Radius = PropertyHelper.FindProp("_CLRim2AxisRadius", props);

            if (rimRadius == null && rim2Radius == null) return;  // 无参数 → 非Hero Shader

            if (!Foldout("★ 边缘光轴向衰减 (Rim Axis Falloff)", "RimAxisFalloff")) return;

            EditorGUI.indentLevel++;

            // --- Rim1 轴向衰减 ---
            if (rimRadius != null)
            {
                EditorGUILayout.LabelField("── 边缘光 1 ──", EditorStyles.boldLabel);
                editor.ShaderProperty(rimRadius,
                    new GUIContent("Rim1 Radius",
                        "衰减半径 (0=无衰减/关闭)\n" +
                        "方向自动复用 CharacterLighting 的 Rim1 方向\n" +
                        ">0: 沿边缘光方向正向=全亮, 反向=消失, 线性过渡\n" +
                        "典型值: 棋子高度的一半"));
                EditorGUILayout.Space(4);
            }

            // --- Rim2 轴向衰减 ---
            if (rim2Radius != null)
            {
                EditorGUILayout.LabelField("── 边缘光 2 ──", EditorStyles.boldLabel);
                editor.ShaderProperty(rim2Radius,
                    new GUIContent("Rim2 Radius",
                        "衰减半径 (0=无衰减/关闭)\n" +
                        "方向自动复用 CharacterLighting 的 Rim2 方向\n" +
                        ">0: 沿边缘光方向正向=全亮, 反向=消失, 线性过渡"));
            }

            EditorGUILayout.HelpBox(
                "v5.2: 基于模型空间坐标的轴向距离衰减\n" +
                "• 衰减方向自动复用 CharacterLighting 全局的 Rim Dir\n" +
                "• 沿 Rim Dir 正方向 = 全亮, 反方向 = 完全消失, 线性过渡\n" +
                "• Radius=0 完全关闭轴向衰减 (默认行为)\n" +
                "• 仅材质球控制半径，不受 CharacterLighting 组件影响",
                MessageType.Info);

            EditorGUI.indentLevel--;
        }

        // =================================================================
        // Feature Drawers — UV2 流光（已废弃 — D16.A α 后由 LittleLegendStandardGUI.DrawFlowProperties 接管）
        // 旧 _UV2Flow* 字段已全量改名为 _Flow*；此 base 方法不再使用，已删除以避免误调用。
        // =================================================================

        // =================================================================
        // Feature Drawers — SparkleViewMask 闪片
        // =================================================================
        protected void DrawSparkleViewMaskProperties(MaterialEditor editor, MaterialProperty[] props)
        {
            var enabledProp = PropertyHelper.FindProp("_SparkleEnabled", props);
            bool enabled = PropertyHelper.DrawToggle(editor, enabledProp, "Enable Sparkle", "_SPARKLE_ON");

            if (enabled)
            {
                var useMask = PropertyHelper.FindProp("_UseSparkleMask", props);
                var tex01 = PropertyHelper.FindProp("_SparkleViewTex01", props);
                var tex02 = PropertyHelper.FindProp("_SparkleViewTex02", props);
                var color01 = PropertyHelper.FindProp("_SparkleColor01", props);
                var color02 = PropertyHelper.FindProp("_SparkleColor02", props);
                var flowSpeed = PropertyHelper.FindProp("_FlowSpeed", props);
                var sparkleSpeed = PropertyHelper.FindProp("_SparkleSpeed", props);
                var sparklePow = PropertyHelper.FindProp("_SparklePow", props);
                var gemEnabled = PropertyHelper.FindProp("_Gem_Enabled", props);
                var gemMin = PropertyHelper.FindProp("_GemMin", props);
                var gemMax = PropertyHelper.FindProp("_GemMax", props);
                var gemVector = PropertyHelper.FindProp("_GemVector", props);

                EditorGUILayout.Space(2);
                if (useMask != null) editor.ShaderProperty(useMask, "Use Sparkle Mask");

                EditorGUILayout.LabelField("── 闪片纹理 ──", EditorStyles.miniLabel);
                if (tex01 != null)
                    editor.TexturePropertySingleLine(new GUIContent("Sparkle Tex 01"), tex01, color01);
                if (tex02 != null)
                    editor.TexturePropertySingleLine(new GUIContent("Sparkle Tex 02"), tex02, color02);

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("── 动画参数 ──", EditorStyles.miniLabel);
                if (flowSpeed != null) editor.ShaderProperty(flowSpeed, "Flow Speed (XY=Tex01, ZW=Tex02)");
                if (sparkleSpeed != null) editor.ShaderProperty(sparkleSpeed, "Sparkle Speed");
                if (sparklePow != null) editor.ShaderProperty(sparklePow, "Sparkle Power");

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("── 宝石效果 ──", EditorStyles.miniLabel);
                if (gemEnabled != null) editor.ShaderProperty(gemEnabled, "Gem Enabled");
                if (gemMin != null) editor.ShaderProperty(gemMin, "Gem Min");
                if (gemMax != null) editor.ShaderProperty(gemMax, "Gem Max");
                if (gemVector != null) editor.ShaderProperty(gemVector, "Gem Vector");
            }
        }

        // =================================================================
        // Feature Drawers — LittleLegend SparkleView (视角闪光)
        // =================================================================
        protected void DrawLLSparkleViewProperties(MaterialEditor editor, MaterialProperty[] props)
        {
            var enabledProp = PropertyHelper.FindProp("_SparkleViewEnabled", props);
            bool enabled = PropertyHelper.DrawToggle(editor, enabledProp, "Enable Sparkle View", "_SPARKLEVIEW_ON");

            if (enabled)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("── 闪光纹理 ──", EditorStyles.miniLabel);
                var tex01 = PropertyHelper.FindProp("_SparkleViewTex01", props);
                var tex02 = PropertyHelper.FindProp("_SparkleViewTex02", props);
                var color01 = PropertyHelper.FindProp("_SparkleViewColor01", props);
                var color02 = PropertyHelper.FindProp("_SparkleViewColor02", props);
                if (tex01 != null)
                {
                    editor.TexturePropertySingleLine(new GUIContent("Sparkle Tex 01"), tex01, color01);
                    editor.TextureScaleOffsetProperty(tex01);
                }
                if (tex02 != null)
                {
                    editor.TexturePropertySingleLine(new GUIContent("Sparkle Tex 02"), tex02, color02);
                    editor.TextureScaleOffsetProperty(tex02);
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("── 视角参数 ──", EditorStyles.miniLabel);
                var sparklePow = PropertyHelper.FindProp("_SparkleViewPow", props);
                var sparkleSpeed = PropertyHelper.FindProp("_SparkleViewSpeed", props);
                var useMask = PropertyHelper.FindProp("_SparkleViewUseMask", props);
                if (sparklePow != null) editor.ShaderProperty(sparklePow, "Sparkle Power (视角衰减)");
                if (sparkleSpeed != null) editor.ShaderProperty(sparkleSpeed, "Sparkle Speed (深层偏移)");
                if (useMask != null) editor.ShaderProperty(useMask, "Use Feature Mask");

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("── 宝石遮罩 (可选) ──", EditorStyles.miniLabel);
                var gemEnabled = PropertyHelper.FindProp("_SparkleViewGemEnabled", props);
                var gemMin = PropertyHelper.FindProp("_SparkleViewGemMin", props);
                var gemMax = PropertyHelper.FindProp("_SparkleViewGemMax", props);
                var gemDir = PropertyHelper.FindProp("_SparkleViewGemDir", props);
                if (gemEnabled != null) editor.ShaderProperty(gemEnabled, "Gem Mask Enabled");
                if (gemEnabled != null && gemEnabled.floatValue > 0.5f)
                {
                    if (gemMin != null) editor.ShaderProperty(gemMin, "Gem Min");
                    if (gemMax != null) editor.ShaderProperty(gemMax, "Gem Max");
                    if (gemDir != null) editor.ShaderProperty(gemDir, "Gem Direction");
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("── 流动 (可选) ──", EditorStyles.miniLabel);
                var flowEnabled = PropertyHelper.FindProp("_SparkleViewFlowEnabled", props);
                var flowSpeed = PropertyHelper.FindProp("_SparkleViewFlowSpeed", props);
                if (flowEnabled != null) editor.ShaderProperty(flowEnabled, "Flow Enabled");
                if (flowEnabled != null && flowEnabled.floatValue > 0.5f)
                {
                    if (flowSpeed != null) editor.ShaderProperty(flowSpeed, "Flow Speed (XY=Tex01, ZW=Tex02)");
                }
            }
        }

        // =================================================================
        // Feature Drawers — LittleLegend StarParallax (双层视差星光)
        // =================================================================
        protected void DrawLLStarParallaxProperties(MaterialEditor editor, MaterialProperty[] props)
        {
            var enabledProp = PropertyHelper.FindProp("_StarParallaxEnabled", props);
            bool enabled = PropertyHelper.DrawToggle(editor, enabledProp, "Enable Star Parallax", "_STARPARALLAX_ON");

            if (enabled)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("── 浅层星光 ──", EditorStyles.miniLabel);
                var map01 = PropertyHelper.FindProp("_StarParallaxMap01", props);
                var color01 = PropertyHelper.FindProp("_StarParallaxColor01", props);
                var depth01 = PropertyHelper.FindProp("_StarParallaxDepth01", props);
                var speed01 = PropertyHelper.FindProp("_StarParallaxSpeed01", props);
                if (map01 != null)
                    editor.TexturePropertySingleLine(new GUIContent("Star Map 01"), map01, color01);
                if (depth01 != null) editor.ShaderProperty(depth01, "Parallax Depth 01");
                if (speed01 != null) editor.ShaderProperty(speed01, "Speed 01 (XY=Tiling, ZW=Flow)");

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("── 深层星光 ──", EditorStyles.miniLabel);
                var map02 = PropertyHelper.FindProp("_StarParallaxMap02", props);
                var color02 = PropertyHelper.FindProp("_StarParallaxColor02", props);
                var depth02 = PropertyHelper.FindProp("_StarParallaxDepth02", props);
                var speed02 = PropertyHelper.FindProp("_StarParallaxSpeed02", props);
                if (map02 != null)
                    editor.TexturePropertySingleLine(new GUIContent("Star Map 02"), map02, color02);
                if (depth02 != null) editor.ShaderProperty(depth02, "Parallax Depth 02");
                if (speed02 != null) editor.ShaderProperty(speed02, "Speed 02 (XY=Tiling, ZW=Flow)");

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("── 模型空间参数 ──", EditorStyles.miniLabel);
                var modelScale = PropertyHelper.FindProp("_StarParallaxModelScale", props);
                var modelOffset = PropertyHelper.FindProp("_StarParallaxModelOffset", props);
                var uvMode = PropertyHelper.FindProp("_StarParallaxUVMode", props);
                if (modelScale != null) editor.ShaderProperty(modelScale, "Model Pos Scale (XYZ=缩放, W=归一化)");
                if (modelOffset != null) editor.ShaderProperty(modelOffset, "Model Pos Offset");
                if (uvMode != null) editor.ShaderProperty(uvMode, "Use UV (关闭=用模型坐标)");
            }
        }

        // ===== 渲染设置 =====
        protected void DrawRenderingSettings(MaterialEditor editor, MaterialProperty[] props)
        {
            if (Foldout("渲染设置", "Rendering"))
            {
                EditorGUI.indentLevel++;

                // PCSS 软阴影（仅当 shader 声明了 _SHADOWS_PCSS 关键字时可见）
                var pcssProp = PropertyHelper.FindProp("_PCSSEnabled", props);
                if (pcssProp != null)
                {
                    editor.ShaderProperty(pcssProp, "PCSS 软阴影");
                }

                var queueOffset = PropertyHelper.FindProp("_QueueOffset", props);

                if (queueOffset != null)
                {
                    EditorGUI.BeginChangeCheck();
                    editor.ShaderProperty(queueOffset, "Queue Offset");
                    if (EditorGUI.EndChangeCheck())
                    {
                        // QueueOffset 变动后刷新 renderQueue
                        var surfaceProp = PropertyHelper.FindProp("_Surface", props);
                        if (surfaceProp != null)
                        {
                            foreach (var obj in editor.targets)
                                ApplySurfaceType((Material)obj, (SurfaceType)(int)surfaceProp.floatValue);
                        }
                    }
                }

                editor.RenderQueueField();

                EditorGUI.indentLevel--;
            }
        }
    }
}
