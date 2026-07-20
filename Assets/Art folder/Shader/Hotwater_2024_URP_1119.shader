// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Hotwater/2024/UrpAll_GUI_1119"
{
	Properties
	{
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		[Main(g17, _, off, off)]_Color_Group3("基础设置", Float) = 0
		[SubEnum(g17,UnityEngine.Rendering.BlendMode)][SubEnum(g17,add,1,blend,10)]_Float1("材质模式", Float) = 10
		[SubEnum(g17,UnityEngine.Rendering.CullMode)]_Float2("双面模式", Float) = 0
		[SubToggle(g17,_)]_Float4("深度写入", Float) = 0
		[SubEnum(g17,UnityEngine.Rendering.CompareFunction)]_Ztestmode("深度测试", Float) = 4
		[SubEnum(g17,UnityEngine.Rendering.ColorWriteMask)]_Float60("colormask", Float) = 15
		[SubEnum(g17,UnityEngine.Rendering.CompareFunction)]_Ztestmode1("stencil_comp", Float) = 0
		[SubEnum(g17,UnityEngine.Rendering.StencilOp)]_Ztestmode2("stencil_pass", Float) = 0
		[Sub(g17)]_Float46("stencil_reference", Float) = 0
		[Toggle][SubToggle(g17,_)]_Float131("启用2u(需关闭customdata)", Float) = 0
		[Main(g10, _, off, off)]_Color_Group2("颜色", Float) = 0
		[HDR][Sub(g10)]_Color0("颜色", Color) = (1,1,1,1)
		[Sub(g10)]_Float14("整体颜色强度", Float) = 1
		[SubToggle(g10, _)]_Float35("双面颜色（默认关闭，勾上开启）", Float) = 0
		[HDR][Sub(g10)]_Color3("颜色2", Color) = (1,1,1,1)
		[Sub(g10)]_Float15("alpha强度", Float) = 1
		[SubToggle(g10, _)]_Float70("限制alpha值为0-1", Float) = 0
		[Main(g9, _, off, off)]_Depthfade_Group1("Depthfade", Float) = 0
		[Sub(g9)]_Float16("软粒子（羽化边缘）", Float) = 0
		[SubToggle(g9, _)]_Float5("反向软粒子(强化边缘）", Float) = 0
		[Sub(g9)]_Float28("边缘强度", Float) = 1
		[Sub(g9)]_Float30("边缘收窄", Float) = 1
		[Sub(g9)]_Float55("相机软粒子（贴脸羽化）距离", Float) = 0
		[Sub(g9)]_Float0("相机软粒子（贴脸羽化）位置", Float) = 0
		[Main(g8, _, off, off)]_Fresnel_Group2("菲尼尔", Float) = 0
		[Enum(off,0,on,1)][KWEnum(g8,off,_0,on,_1)]_Float33("单独菲尼尔开关", Float) = 0
		[SubToggle(g8, _)]_Float145("开启外边缘", Float) = 0
		[Sub(g8)]_power3("菲尼尔范围", Float) = 1
		[HDR][Sub(g8)]_Color6("外边缘颜色", Color) = (1,1,1,1)
		[Sub(g8)]_Float19("菲尼尔软硬", Float) = 1
		[SubToggle(g8, _)]_Float20("反向菲尼尔（虚化边缘）", Float) = 0
		[Main(g1, _, off, off)]_Maintex_Group("主贴图", Float) = 0
		[SubToggle(g1, _)]_Float144("使用屏幕uv", Float) = 0
		[Sub(g1)]_maintex("主贴图", 2D) = "white" {}
		[Enum(A,0,R,1,G,2,B,3)][KWEnum(g1,A,_0,R,_1,G,_2,B,_3)]_maintex_alpha("主贴图通道", Float) = 0
		[SubToggle(g1, _)]_Float62("主贴图x轴clamp", Float) = 0
		[SubToggle(g1, _)]_Float71("主贴图y轴clamp", Float) = 0
		[SubToggle(g1, _)]_Float49("主贴图极坐标（竖向贴图）", Float) = 0
		[Sub(g1)]_Float39("主贴图旋转", Range( -1 , 1)) = 0
		[HideInInspector]_maintex_ST("主贴图tilling&offset", Vector) = (1,1,0,0)
		[Sub(g1)]_Float61("主贴图去色", Range( 0 , 1)) = 0
		[Sub(g1)]_Float34("主贴图细节对比度", Float) = 1
		[Sub(g1)]_Float37("主贴图细节提亮", Float) = 1
		[Sub(g1)]_Float36("细节平滑过渡", Range( 0 , 1)) = 1
		[Sub(g1)]_Vector16("色散", Vector) = (0,0,0,0)
		[Sub(g1)]_Vector0("主贴图流动&斜切", Vector) = (0,0,0,0)
		[Main(g11, _, off, off)]_Ramp("Ramp", Float) = 0
		[SubToggle(g11, _)]_Float159("ramp图极坐标（竖向贴图）", Float) = 0
		[Ramp(g11)]_Ramptex("Ramp贴图", 2D) = "white" {}
		[SubToggle(g11, _)]_Float63("ramp图x轴Clamp", Float) = 0
		[SubToggle(g11, _)]_Float81("ramp图y轴Clamp", Float) = 0
		[Sub(g11)]_Float40("ramp图旋转", Range( -1 , 1)) = 0
		[Sub(g11)]_Vector9("ramp图tilling&offset", Vector) = (1,1,0,0)
		[Sub(g11)]_Vector7("ramp图流动速度", Vector) = (0,0,0,0)
		[Enum(Multiply,0,Maintex,1,Dissolove,2,Off,3,Fresnel,4)][SubKeywordEnumDrawer(g11,Multiply,Maintex,Dissolove,Off,Fresnel)]_Float154("ramp映射", Float) = 3
		[Sub(g11)]_Float152("映射power", Float) = 2
		[Main(g2, _, off, off)]_Mask_Group("遮罩", Float) = 0
		[Sub(g2)]_Mask("遮罩01", 2D) = "white" {}
		[Enum(A,0,R,1,G,2,B,3)][KWEnum(g2,A,_0,R,_1,G,_2,B,_3)]_mask01_alpha("遮罩01通道", Float) = 0
		[SubToggle(g2, _)]_Float64("遮罩01x轴Clamp", Float) = 0
		[SubToggle(g2, _)]_Float82("遮罩01y轴Clamp", Float) = 0
		[SubToggle(g2, _)]_Float51("遮罩01极坐标（竖向贴图）", Float) = 0
		[Sub(g2)]_Float43("遮罩01旋转", Range( -1 , 1)) = 0
		[HideInInspector]_Mask_ST("_Mask_ST", Vector) = (1,1,0,0)
		[Sub(g2)]_Vector11("遮罩01流动速度&斜切", Vector) = (0,0,0,0)
		[Sub(g2)]_Mask1("遮罩02", 2D) = "white" {}
		[Enum(A,0,R,1,G,2,B,3)][KWEnum(g2,A,_0,R,_1,G,_2,B,_3)]_mask02_alpha("遮罩02通道", Float) = 0
		[SubToggle(g2, _)]_Float65("遮罩02x轴Clamp", Float) = 0
		[SubToggle(g2, _)]_Float83("遮罩02y轴Clamp", Float) = 0
		[SubToggle(g2, _)]_Float52("遮罩02极坐标（竖向贴图）", Float) = 0
		[Sub(g2)]_Float42("遮罩02旋转", Range( -1 , 1)) = 0
		[HideInInspector]_Mask1_ST("_Mask1_ST", Vector) = (1,1,0,0)
		[Sub(g2)]_Vector13("遮罩02流动&斜切", Vector) = (0,0,0,0)
		[Main(g3, _, off, off)]_Disolove_Group("溶解", Float) = 0
		[Sub(g3)]_dissolvetex("溶解贴图", 2D) = "white" {}
		[Enum(A,0,R,1,G,2,B,3)][KWEnum(g3,A,_0,R,_1,G,_2,B,_3)]_maintex_alpha1("溶解图通道", Float) = 1
		[SubToggle(g3, _)]_Float66("溶解贴图x轴Clamp", Float) = 0
		[SubToggle(g3, _)]_Float87("溶解贴图y轴Clamp", Float) = 0
		[SubToggle(g3, _)]_Float53("溶解极坐标（竖向贴图）", Float) = 0
		[Sub(g3)]_Float41("溶解贴图旋转", Range( -1 , 1)) = 0
		[HideInInspector]_dissolvetex_ST("_dissolvetex_ST", Vector) = (1,1,0,0)
		[Sub(g3)]_Vector15("溶解流动&斜切", Vector) = (0,0,0,0)
		[Ramp(g3)]_TextureSample1("溶解方向贴图（渐变）", 2D) = "white" {}
		[Sub(g3)]_Float47("溶解方向旋转", Range( -1 , 1)) = 0
		[Sub(g3)]_Float130("混合溶解强度", Range( 0 , 1)) = 0
		[Sub(g3)]_Float6("溶解", Float) = 0
		[Sub(g3)]_Float8("软硬", Range( 0 , 0.5)) = 0.5
		[SubToggle(g3, _)]_Float25("亮边溶解（默认关闭，勾上开启）", Float) = 0
		[Sub(g3)]_Float17("一层亮边宽度", Float) = 0
		[Sub(g3)]_Float160("二层亮边宽度", Float) = 0
		[HDR][Sub(g3)]_Color1("一层亮边颜色", Color) = (1,1,1,1)
		[HDR][Sub(g3)]_Color7("二层亮边颜色（软边颜色）", Color) = (1,1,1,1)
		[SubToggle(g3, _)]_Float139("开洞（开启后方向失效）", Float) = 0
		[Enum(local,0,world,1)][KWEnum(g3,local,_0,world,_1)]_Float155("local/world", Float) = 0
		[Sub(g3)]_Vector35("开洞坐标", Vector) = (0,0,0,0)
		[Sub(g3)]_Float72("alphaclip溶解（层级2000以下使用）", Float) = 0
		[Main(g4, _, off, off)]_Noise_Group("扰动", Float) = 0
		[Sub(g4)]_noise("扰动贴图", 2D) = "white" {}
		[SubToggle(g4, _)]_Float67("扰动贴图x轴Clamp", Float) = 0
		[SubToggle(g4, _)]_Float85("扰动贴图y轴Clamp", Float) = 0
		[SubToggle(g4, _)]_Float54("扰动极坐标（竖向贴图）", Float) = 0
		[Sub(g4)]_Float44("扰动贴图旋转", Range( -1 , 1)) = 0
		[HideInInspector]_noise_ST("_noise_ST", Vector) = (1,1,0,0)
		[Sub(g4)]_Vector17("扰动流动&斜切", Vector) = (0,0,0,0)
		[Enum(multiply,0,add,1)][KWEnum(g4,multiply,_0,add,_1)]_Float76("扰动遮罩/双重扰动（add为双重扰动）", Float) = 0
		[Sub(g4)]_noisemask("扰动遮罩", 2D) = "white" {}
		[SubToggle(g4, _)]_Float73("扰动遮罩x轴Clamp", Float) = 0
		[SubToggle(g4, _)]_Float84("扰动遮罩y轴Clamp", Float) = 0
		[SubToggle(g4, _)]_Float57("扰动遮罩极坐标", Float) = 0
		[Sub(g4)]_Float74("扰动遮罩旋转", Range( -1 , 1)) = 0
		[HideInInspector]_noisemask_ST("_noisemask_ST", Vector) = (1,1,0,0)
		[Sub(g4)]_Vector23("扰动遮罩流动&斜切", Vector) = (0,0,0,0)
		[Sub(g4)]_Vector33("扰动贴图remap", Vector) = (0,1,0,1)
		[Sub(g4)]_Float9("主贴图扰动强度", Float) = 0
		[Sub(g4)]_Float80("mask扰动强度", Float) = 0
		[Sub(g4)]_Float79("溶解扰动强度", Float) = 0
		[Main(g14, _, off, off)]_Maintex_Group4("Flowmap", Float) = 0
		[Sub(g14)]_flowmap("flowmapTex", 2D) = "white" {}
		[Sub(g14)]_Float32("flowmap扰动", Range( 0 , 1)) = 0
		[Main(g5, _, off, off)]_Vertex_Group("顶点偏移", Float) = 0
		[Enum(off,0,on,1)][KWEnum(g5,off,_0,on,_1)]_Float135("顶点法线", Float) = 1
		[Sub(g5)]_vertextex("顶点偏移贴图", 2D) = "white" {}
		[Sub(g5)]_Vector32("顶点偏移贴图remap", Vector) = (0,1,0,1)
		[SubToggle(g5, _)]_Float68("顶点偏移贴图x轴Clamp", Float) = 0
		[SubToggle(g5, _)]_Float89("顶点偏移贴图y轴Clamp", Float) = 0
		[SubToggle(g5, _)]_Float56("顶点偏移极坐标（竖向贴图）", Float) = 0
		[Sub(g5)]_Float45("顶点贴图旋转", Range( -1 , 1)) = 0
		[HideInInspector]_vertextex_ST("_vertextex_ST", Vector) = (1,1,0,0)
		[Sub(g5)]_Vector19("顶点偏移流动&斜切", Vector) = (0,0,0,0)
		[Sub(g5)]_Vector5("顶点偏移xyz强度", Vector) = (0,0,0,0)
		[Sub(g5)]_vertextex1("顶点偏移遮罩", 2D) = "white" {}
		[SubToggle(g5, _)]_Float78("顶点偏移遮罩x轴Clamp", Float) = 0
		[SubToggle(g5, _)]_Float88("顶点偏移遮罩y轴Clamp", Float) = 0
		[SubToggle(g5, _)]_Float75("顶点偏移遮罩极坐标", Float) = 0
		[Sub(g5)]_Float77("顶点遮罩旋转", Range( -1 , 1)) = 0
		[HideInInspector]_vertextex1_ST("_vertextex1_ST", Vector) = (1,1,0,0)
		[Sub(g5)]_Vector25("顶点偏移遮罩流动＆斜切", Vector) = (0,0,0,0)
		[Main(g6, _, off, off)]_Ref_Group("折射", Float) = 0
		[Enum(off,0,on,1)][KWEnum(g6,off,_0,on,_1)]_Float48("折射开关", Float) = 0
		[Sub(g6)]_reftex(" 折射贴图（法线）", 2D) = "white" {}
		[SubToggle(g6, _)]_Float69("折射贴图x轴Clamp", Float) = 0
		[SubToggle(g6, _)]_Float86("折射贴图y轴Clamp", Float) = 0
		[SubToggle(g6, _)]_Float58("折射极坐标（竖向贴图）", Float) = 0
		[Sub(g6)]_Float59("折射贴图旋转", Range( -1 , 1)) = 1
		[HideInInspector]_reftex_ST("_reftex_ST", Vector) = (1,1,0,0)
		[Sub(g6)]_Vector21("折射流动&斜切", Vector) = (0,0,0,0)
		[HDR][Sub(g6)]_Color2("折射颜色", Color) = (1,1,1,1)
		[Sub(g6)]_Float23("折射强度", Float) = 0
		[Main(g12, _, off, off)]_Maintex_Group2("阴影", Float) = 0
		[Enum(off,0,20,1)][KWEnum(g12,off,_0,on,_1)]_Float134("阴影开关", Float) = 0
		[Sub(g12)]_normallight("法线", 2D) = "white" {}
		[Sub(g12)]_Vector10("法线流动&斜切", Vector) = (0,0,0,0)
		[Sub(g12)]_Float146("法线强度", Float) = 0
		[Sub(g12)]_Float133("阴影软硬", Range( 0.5 , 1)) = 0.5
		[Sub(g12)]_Float132("阴影范围", Float) = 0.5
		[HDR][Sub(g12)]_Color5("亮部颜色", Color) = (1,1,1,1)
		[HDR][Sub(g12)]_Color4("暗部颜色", Color) = (0.490566,0.490566,0.490566,1)
		[SubToggle(g12, _)]_Float137("切换为假点光（默认为平行光）", Float) = 0
		[Sub(g12)]_Vector34("假点光坐标", Vector) = (0,0,0,0)
		[Main(g13, _, off, off)]_Maintex_Group3("Matcap&Cubemap", Float) = 0
		[Enum(off,0,on,1)][KWEnum(g13,off,_0,on,_1)]_Float141("反射开关", Float) = 0
		[Sub(g13)]_matcap("matcap", 2D) = "white" {}
		[Sub(g13)]_Float157("matcap去色", Range( 0 , 1)) = 0
		[Sub(g13)]_Float140("matcap强度", Float) = 0
		[NoScaleOffset][Sub(g13)]_cubemap("cubemap", CUBE) = "white" {}
		[Sub(g13)]_Float258("cube强度", Float) = 0
		[Main(g15, _, off, off)]_Maintex_Group5("视差", Float) = 0
		[Enum(off,0,on,1)][KWEnum(g15,off,_0,on,_1)]_Float143("开启视差映射(mesh模式下使用）", Float) = 0
		[Sub(g15)]_parallax("视差贴图", 2D) = "white" {}
		[Sub(g15)]_Float38("视差缩放", Float) = 0
		[Sub(g15)]_refplane("refplane(0黑色下沉,1白色抬高)", Range( 0 , 1)) = 1
		[Main(g7, _, off, off)]_Custom_Group1("custom控制", Float) = 0
		[SubToggle(g7, _)]_Float10("主贴图自定义偏移", Float) = 0
		[Enum(custom1x,0,custom1y,1,custom1z,2,custom1w,3,custom2x,4,custom2y,5,custom2z,6,custom2w,7)][SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w)]_Custom1("主贴图x轴", Float) = 0
		[Enum(custom1x,0,custom1y,1,custom1z,2,custom1w,3,custom2x,4,custom2y,5,custom2z,6,custom2w,7)][SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w)]_Custom2("主贴图y轴", Float) = 0
		[SubToggle(g7, _)][Space][Space]_Float12("mask01自定义偏移", Float) = 0
		[Enum(custom1x,0,custom1y,1,custom1z,2,custom1w,3,custom2x,4,custom2y,5,custom2z,6,custom2w,7)][SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w)]_Custom3("Maskx轴", Float) = 0
		[Enum(custom1x,0,custom1y,1,custom1z,2,custom1w,3,custom2x,4,custom2y,5,custom2z,6,custom2w,7)][SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w)]_Custom4("Masky轴", Float) = 0
		[SubToggle(g7, _)][Space][Space]_Float50("溶解自定义偏移", Float) = 0
		[Enum(custom1x,0,custom1y,1,custom1z,2,custom1w,3,custom2x,4,custom2y,5,custom2z,6,custom2w,7)][SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w)]_Custom5("溶解x轴", Float) = 0
		[Enum(custom1x,0,custom1y,1,custom1z,2,custom1w,3,custom2x,4,custom2y,5,custom2z,6,custom2w,7)][SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w)]_Custom6("溶解y轴", Float) = 0
		[SubToggle(g7, _)][Space][Space]_Float11("custom控制溶解", Float) = 0
		[SubToggle(g7, _)]_Float142("custom控制溶解软硬", Float) = 0
		[Enum(off,0,20,1)][KWEnum(g7,off,_0,on,_1)]_Float129("粒子alpha控制溶解（溶解拖尾使用）", Float) = 0
		[Enum(custom1x,0,custom1y,1,custom1z,2,custom1w,3,custom2x,4,custom2y,5,custom2z,6,custom2w,7)][SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w)]_Custom7("自定义溶解", Float) = 4
		[Enum(custom1x,0,custom1y,1,custom1z,2,custom1w,3,custom2x,4,custom2y,5,custom2z,6,custom2w,7)][SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w)]_Custom9("自定义溶解软硬", Float) = 4
		[SubToggle(g7, _)][Space][Space]_Float151("custom控制alphaclip", Float) = 0
		[Enum(custom1x,0,custom1y,1,custom1z,2,custom1w,3,custom2x,4,custom2y,5,custom2z,6,custom2w,7)][SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w)]_Custom13("自定义alphaclip溶解", Float) = 4
		[SubToggle(g7, _)][Space][Space]_Float31("custom控制flowmap扭曲", Float) = 0
		[Enum(custom1x,0,custom1y,1,custom1z,2,custom1w,3,custom2x,4,custom2y,5,custom2z,6,custom2w,7)][SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w)]_Custom8("自定义flowmap扭曲", Float) = 4
		[SubToggle(g7, _)][Space][Space]_Float257("custom控制扰动总强度", Float) = 0
		[Enum(custom1x,0,custom1y,1,custom1z,2,custom1w,3,custom2x,4,custom2y,5,custom2z,6,custom2w,7)][SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w)]_Custom12("自定义扰动强度", Float) = 4
		[SubToggle(g7, _)][Space][Space]_Float24("custom控制折射", Float) = 0
		[Enum(custom1x,0,custom1y,1,custom1z,2,custom1w,3,custom2x,4,custom2y,5,custom2z,6,custom2w,7)][SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w)]_Custom10("自定义折射", Float) = 4
		[SubToggle(g7, _)][Space][Space]_Float22("custom控制顶点偏移强度", Float) = 0
		[ASEEnd][Enum(custom1x,0,custom1y,1,custom1z,2,custom1w,3,custom2x,4,custom2y,5,custom2z,6,custom2w,7)][SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w)]_Custom11("自定义顶点偏移强度", Float) = 4
		[HideInInspector] _texcoord( "", 2D ) = "white" {}

		//_TessPhongStrength( "Tess Phong Strength", Range( 0, 1 ) ) = 0.5
		//_TessValue( "Tess Max Tessellation", Range( 1, 32 ) ) = 16
		//_TessMin( "Tess Min Distance", Float ) = 10
		//_TessMax( "Tess Max Distance", Float ) = 25
		//_TessEdgeLength ( "Tess Edge length", Range( 2, 50 ) ) = 16
		//_TessMaxDisp( "Tess Max Displacement", Float ) = 25
	}

	SubShader
	{
		LOD 0

		
		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
		
		Cull [_Float2]
		AlphaToMask Off
		HLSLINCLUDE
		#pragma target 4.0

		#ifndef ASE_TESS_FUNCS
		#define ASE_TESS_FUNCS
		float4 FixedTess( float tessValue )
		{
			return tessValue;
		}
		
		float CalcDistanceTessFactor (float4 vertex, float minDist, float maxDist, float tess, float4x4 o2w, float3 cameraPos )
		{
			float3 wpos = mul(o2w,vertex).xyz;
			float dist = distance (wpos, cameraPos);
			float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
			return f;
		}

		float4 CalcTriEdgeTessFactors (float3 triVertexFactors)
		{
			float4 tess;
			tess.x = 0.5 * (triVertexFactors.y + triVertexFactors.z);
			tess.y = 0.5 * (triVertexFactors.x + triVertexFactors.z);
			tess.z = 0.5 * (triVertexFactors.x + triVertexFactors.y);
			tess.w = (triVertexFactors.x + triVertexFactors.y + triVertexFactors.z) / 3.0f;
			return tess;
		}

		float CalcEdgeTessFactor (float3 wpos0, float3 wpos1, float edgeLen, float3 cameraPos, float4 scParams )
		{
			float dist = distance (0.5 * (wpos0+wpos1), cameraPos);
			float len = distance(wpos0, wpos1);
			float f = max(len * scParams.y / (edgeLen * dist), 1.0);
			return f;
		}

		float DistanceFromPlane (float3 pos, float4 plane)
		{
			float d = dot (float4(pos,1.0f), plane);
			return d;
		}

		bool WorldViewFrustumCull (float3 wpos0, float3 wpos1, float3 wpos2, float cullEps, float4 planes[6] )
		{
			float4 planeTest;
			planeTest.x = (( DistanceFromPlane(wpos0, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[0]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.y = (( DistanceFromPlane(wpos0, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[1]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.z = (( DistanceFromPlane(wpos0, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[2]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.w = (( DistanceFromPlane(wpos0, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[3]) > -cullEps) ? 1.0f : 0.0f );
			return !all (planeTest);
		}

		float4 DistanceBasedTess( float4 v0, float4 v1, float4 v2, float tess, float minDist, float maxDist, float4x4 o2w, float3 cameraPos )
		{
			float3 f;
			f.x = CalcDistanceTessFactor (v0,minDist,maxDist,tess,o2w,cameraPos);
			f.y = CalcDistanceTessFactor (v1,minDist,maxDist,tess,o2w,cameraPos);
			f.z = CalcDistanceTessFactor (v2,minDist,maxDist,tess,o2w,cameraPos);

			return CalcTriEdgeTessFactors (f);
		}

		float4 EdgeLengthBasedTess( float4 v0, float4 v1, float4 v2, float edgeLength, float4x4 o2w, float3 cameraPos, float4 scParams )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;
			tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
			tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
			tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
			tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			return tess;
		}

		float4 EdgeLengthBasedTessCull( float4 v0, float4 v1, float4 v2, float edgeLength, float maxDisplacement, float4x4 o2w, float3 cameraPos, float4 scParams, float4 planes[6] )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;

			if (WorldViewFrustumCull(pos0, pos1, pos2, maxDisplacement, planes))
			{
				tess = 0.0f;
			}
			else
			{
				tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
				tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
				tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
				tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			}
			return tess;
		}
		#endif //ASE_TESS_FUNCS

		ENDHLSL

		
		Pass
		{
			
			Name "Forward"
			Tags { "LightMode"="UniversalForward" }
			
			Blend SrcAlpha [_Float1]
			ZWrite [_Float4]
			ZTest [_Ztestmode]
			Offset 0 , 0
			ColorMask [_Float60]
			Stencil
			{
				Ref [_Float46]
				Comp [_Ztestmode1]
				Pass [_Ztestmode2]
				Fail Keep
				ZFail Keep
			}

			HLSLPROGRAM
			#define _RECEIVE_SHADOWS_OFF 1
			#pragma multi_compile_instancing
			#define ASE_SRP_VERSION 999999
			#define REQUIRE_DEPTH_TEXTURE 1

			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x

			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"

			#if ASE_SRP_VERSION <= 70108
			#define REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
			#endif

			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_FRAG_COLOR


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_tangent : TANGENT;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 worldPos : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				float4 shadowCoord : TEXCOORD1;
				#endif
				#ifdef ASE_FOG
				float fogFactor : TEXCOORD2;
				#endif
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				float4 ase_texcoord6 : TEXCOORD6;
				float4 ase_texcoord7 : TEXCOORD7;
				float4 ase_texcoord8 : TEXCOORD8;
				float4 ase_texcoord9 : TEXCOORD9;
				float4 ase_texcoord10 : TEXCOORD10;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Vector15;
			float4 _Vector35;
			float4 _reftex_ST;
			float4 _Color1;
			float4 _flowmap_ST;
			float4 _Vector25;
			float4 _vertextex1_ST;
			float4 _Vector34;
			float4 _normallight_ST;
			float4 _Vector10;
			float4 _Vector33;
			float4 _Color5;
			float4 _Color4;
			float4 _Vector0;
			float4 _maintex_ST;
			float4 _Color2;
			float4 _Color7;
			float4 _noise_ST;
			float4 _Vector17;
			float4 _parallax_ST;
			float4 _Vector23;
			float4 _noisemask_ST;
			float4 _Vector16;
			float4 _vertextex_ST;
			float4 _Vector32;
			float4 _Vector21;
			float4 _Color3;
			float4 _Color0;
			float4 _Vector19;
			float4 _Color6;
			float4 _Mask1_ST;
			float4 _Vector9;
			float4 _TextureSample1_ST;
			float4 _Vector11;
			float4 _dissolvetex_ST;
			float4 _Ramptex_ST;
			float4 _Vector7;
			float4 _Mask_ST;
			float4 _Vector13;
			float3 _Vector5;
			float _Custom6;
			float _Float142;
			float _Float50;
			float _Float53;
			float _Custom9;
			float _Float8;
			float _Float160;
			float _Custom5;
			float _Float79;
			float _Float41;
			float _Custom8;
			float _Float66;
			float _Float129;
			float _Float87;
			float _Float11;
			float _Custom7;
			float _maintex_alpha1;
			float _Float6;
			float _Float130;
			float _Float139;
			float _Float155;
			float _Float17;
			float _Float47;
			float _Float24;
			float _Float58;
			float _Custom3;
			float _Custom4;
			float _Float12;
			float _Float51;
			float _Float80;
			float _Float43;
			float _Float64;
			float _Float15;
			float _Float82;
			float _Float52;
			float _Float42;
			float _Float65;
			float _Float83;
			float _mask02_alpha;
			float _Float70;
			float _Float72;
			float _mask01_alpha;
			float _Float36;
			float _Float37;
			float _Float34;
			float _Float59;
			float _Float69;
			float _Float86;
			float _Float23;
			float _Custom10;
			float _Float48;
			float _Float133;
			float _Float146;
			float _Float137;
			float _Float132;
			float _Float134;
			float _Float157;
			float _Float140;
			float _Float258;
			float _Float141;
			float _Float14;
			float _maintex_alpha;
			float _Float25;
			float _Vertex_Group;
			float _Float81;
			float _Float145;
			float _Noise_Group;
			float _Ztestmode2;
			float _Depthfade_Group1;
			float _Float131;
			float _Float56;
			float _Float45;
			float _Float68;
			float _Ramp;
			float _Float89;
			float _Custom11;
			float _Float22;
			float _Float75;
			float _Float77;
			float _Float78;
			float _Float88;
			float _Float28;
			float _Float135;
			float _Float55;
			float _Float1;
			float _Float4;
			float _Maintex_Group;
			float _Ref_Group;
			float _Disolove_Group;
			float _Maintex_Group4;
			float _Ztestmode;
			float _Color_Group2;
			float _Float2;
			float _Mask_Group;
			float _Float60;
			float _Color_Group3;
			float _Ztestmode1;
			float _Float46;
			float _Maintex_Group3;
			float _Fresnel_Group2;
			float _Maintex_Group5;
			float _Custom_Group1;
			float _Maintex_Group2;
			float _Float0;
			float _Float16;
			float _Float5;
			float _Float32;
			float _Float31;
			float _Float39;
			float _Float62;
			float _Float71;
			float _Float61;
			float _Float159;
			float _Float257;
			float _Float152;
			float _Float40;
			float _Float63;
			float _Custom13;
			float _Float35;
			float _power3;
			float _Float19;
			float _Float20;
			float _Float154;
			float _Custom12;
			float _Float9;
			float _Float76;
			float _Float30;
			float _Float144;
			float _Custom1;
			float _Custom2;
			float _Float10;
			float _Float49;
			float _Float38;
			float _refplane;
			float _Float143;
			float _Float57;
			float _Float74;
			float _Float73;
			float _Float84;
			float _Float54;
			float _Float44;
			float _Float67;
			float _Float85;
			float _Float33;
			float _Float151;
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END
			sampler2D _vertextex;
			sampler2D _vertextex1;
			uniform float4 _CameraDepthTexture_TexelSize;
			sampler2D _maintex;
			sampler2D _parallax;
			sampler1D _noisemask;
			sampler2D _noise;
			sampler2D _flowmap;
			sampler2D _Ramptex;
			sampler2D _dissolvetex;
			sampler2D _TextureSample1;
			sampler2D _PandaGrabTex;
			sampler2D _reftex;
			sampler2D _normallight;
			sampler2D _matcap;
			samplerCUBE _cubemap;
			sampler2D _Mask;
			sampler2D _Mask1;


			inline float2 POM( sampler2D heightMap, float2 uvs, float2 dx, float2 dy, float3 normalWorld, float3 viewWorld, float3 viewDirTan, int minSamples, int maxSamples, float parallax, float refPlane, float2 tilling, float2 curv, int index )
			{
				float3 result = 0;
				int stepIndex = 0;
				int numSteps = ( int )lerp( (float)maxSamples, (float)minSamples, saturate( dot( normalWorld, viewWorld ) ) );
				float layerHeight = 1.0 / numSteps;
				float2 plane = parallax * ( viewDirTan.xy / viewDirTan.z );
				uvs.xy += refPlane * plane;
				float2 deltaTex = -plane * layerHeight;
				float2 prevTexOffset = 0;
				float prevRayZ = 1.0f;
				float prevHeight = 0.0f;
				float2 currTexOffset = deltaTex;
				float currRayZ = 1.0f - layerHeight;
				float currHeight = 0.0f;
				float intersection = 0;
				float2 finalTexOffset = 0;
				while ( stepIndex < numSteps + 1 )
				{
				 	currHeight = tex2Dgrad( heightMap, uvs + currTexOffset, dx, dy ).r;
				 	if ( currHeight > currRayZ )
				 	{
				 	 	stepIndex = numSteps + 1;
				 	}
				 	else
				 	{
				 	 	stepIndex++;
				 	 	prevTexOffset = currTexOffset;
				 	 	prevRayZ = currRayZ;
				 	 	prevHeight = currHeight;
				 	 	currTexOffset += deltaTex;
				 	 	currRayZ -= layerHeight;
				 	}
				}
				int sectionSteps = 10;
				int sectionIndex = 0;
				float newZ = 0;
				float newHeight = 0;
				while ( sectionIndex < sectionSteps )
				{
				 	intersection = ( prevHeight - prevRayZ ) / ( prevHeight - currHeight + currRayZ - prevRayZ );
				 	finalTexOffset = prevTexOffset + intersection * deltaTex;
				 	newZ = prevRayZ - intersection * layerHeight;
				 	newHeight = tex2Dgrad( heightMap, uvs + finalTexOffset, dx, dy ).r;
				 	if ( newHeight > newZ )
				 	{
				 	 	currTexOffset = finalTexOffset;
				 	 	currHeight = newHeight;
				 	 	currRayZ = newZ;
				 	 	deltaTex = intersection * deltaTex;
				 	 	layerHeight = intersection * layerHeight;
				 	}
				 	else
				 	{
				 	 	prevTexOffset = finalTexOffset;
				 	 	prevHeight = newHeight;
				 	 	prevRayZ = newZ;
				 	 	deltaTex = ( 1 - intersection ) * deltaTex;
				 	 	layerHeight = ( 1 - intersection ) * layerHeight;
				 	}
				 	sectionIndex++;
				}
				return uvs.xy + finalTexOffset;
			}
			
			inline float4 ASE_ComputeGrabScreenPos( float4 pos )
			{
				#if UNITY_UV_STARTS_AT_TOP
				float scale = -1.0;
				#else
				float scale = 1.0;
				#endif
				float4 o = pos;
				o.y = pos.w * 0.5f;
				o.y = ( pos.y - o.y ) * _ProjectionParams.x * scale + o.y;
				return o;
			}
			
			
			VertexOutput VertexFunction ( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float2 appendResult557 = (float2(_Vector19.x , _Vector19.y));
				float2 uv_vertextex = v.ase_texcoord.xyz * _vertextex_ST.xy + _vertextex_ST.zw;
				float2 uv2_vertextex = v.ase_texcoord1 * _vertextex_ST.xy + _vertextex_ST.zw;
				float uv2930 = _Float131;
				float2 lerpResult949 = lerp( uv_vertextex , uv2_vertextex , uv2930);
				float3 appendResult895 = (float3(1.0 , _Vector19.z , 0.0));
				float3 appendResult897 = (float3(_Vector19.w , 1.0 , 0.0));
				float2 temp_output_898_0 = mul( float3( lerpResult949 ,  0.0 ), float3x3(appendResult895, appendResult897, float3(0,0,1)) ).xy;
				float2 appendResult546 = (float2(_vertextex_ST.z , _vertextex_ST.w));
				float2 CenteredUV15_g166 = ( v.ase_texcoord.xy - float2( 0.5,0.5 ) );
				float2 break17_g166 = CenteredUV15_g166;
				float2 appendResult23_g166 = (float2(( length( CenteredUV15_g166 ) * _vertextex_ST.x * 2.0 ) , ( atan2( break17_g166.x , break17_g166.y ) * ( 1.0 / TWO_PI ) * _vertextex_ST.y )));
				float2 lerpResult556 = lerp( ( temp_output_898_0 + appendResult546 ) , (( appendResult23_g166 * temp_output_898_0 )*float2( 1,1 ) + appendResult546) , _Float56);
				float2 panner168 = ( 1.0 * _Time.y * appendResult557 + lerpResult556);
				float cos398 = cos( ( _Float45 * PI ) );
				float sin398 = sin( ( _Float45 * PI ) );
				float2 rotator398 = mul( panner168 - float2( 0.5,0.5 ) , float2x2( cos398 , -sin398 , sin398 , cos398 )) + float2( 0.5,0.5 );
				float2 break644 = rotator398;
				float clampResult646 = clamp( break644.x , 0.0 , 1.0 );
				float lerpResult790 = lerp( break644.x , clampResult646 , _Float68);
				float clampResult645 = clamp( break644.y , 0.0 , 1.0 );
				float lerpResult791 = lerp( break644.y , clampResult645 , _Float89);
				float2 appendResult647 = (float2(lerpResult790 , lerpResult791));
				float3 temp_cast_4 = (1.0).xxx;
				float3 lerpResult993 = lerp( temp_cast_4 , v.ase_normal , _Float135);
				float4 texCoord1915 = v.ase_texcoord1;
				texCoord1915.xy = v.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1914 = clamp( _Custom11 , 0.0 , 1.0 );
				float lerpResult1918 = lerp( texCoord1915.x , texCoord1915.y , clampResult1914);
				float clampResult1919 = clamp( ( _Custom11 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1920 = lerp( lerpResult1918 , texCoord1915.z , clampResult1919);
				float clampResult1921 = clamp( ( _Custom11 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1924 = lerp( lerpResult1920 , texCoord1915.w , clampResult1921);
				float4 texCoord1926 = v.ase_texcoord2;
				texCoord1926.xy = v.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1925 = clamp( ( _Custom11 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1929 = lerp( lerpResult1924 , texCoord1926.x , clampResult1925);
				float clampResult1928 = clamp( ( _Custom11 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1930 = lerp( lerpResult1929 , texCoord1926.y , clampResult1928);
				float clampResult1931 = clamp( ( _Custom11 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1934 = lerp( lerpResult1930 , texCoord1926.z , clampResult1931);
				float clampResult1933 = clamp( ( _Custom11 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1935 = lerp( lerpResult1934 , texCoord1926.w , clampResult1933);
				float custom111553 = lerpResult1935;
				float lerpResult176 = lerp( 1.0 , custom111553 , _Float22);
				float2 appendResult719 = (float2(_Vector25.x , _Vector25.y));
				float2 uv_vertextex1 = v.ase_texcoord.xy * _vertextex1_ST.xy + _vertextex1_ST.zw;
				float2 uv2_vertextex1 = v.ase_texcoord1.xy * _vertextex1_ST.xy + _vertextex1_ST.zw;
				float2 lerpResult946 = lerp( uv_vertextex1 , uv2_vertextex1 , uv2930);
				float3 appendResult886 = (float3(1.0 , _Vector25.z , 0.0));
				float3 appendResult888 = (float3(_Vector25.w , 1.0 , 0.0));
				float2 appendResult710 = (float2(_vertextex1_ST.z , _vertextex1_ST.w));
				float2 CenteredUV15_g167 = ( v.ase_texcoord.xy - float2( 0.5,0.5 ) );
				float2 break17_g167 = CenteredUV15_g167;
				float2 appendResult23_g167 = (float2(( length( CenteredUV15_g167 ) * _vertextex1_ST.x * 2.0 ) , ( atan2( break17_g167.x , break17_g167.y ) * ( 1.0 / TWO_PI ) * _vertextex1_ST.y )));
				float2 lerpResult728 = lerp( ( mul( float3( lerpResult946 ,  0.0 ), float3x3(appendResult886, appendResult888, float3(0,0,1)) ).xy + appendResult710 ) , (mul( float3( appendResult23_g167 ,  0.0 ), float3x3(appendResult886, appendResult888, float3(0,0,1)) ).xy*float2( 1,1 ) + appendResult710) , _Float75);
				float2 panner725 = ( 1.0 * _Time.y * appendResult719 + lerpResult728);
				float cos726 = cos( ( _Float77 * PI ) );
				float sin726 = sin( ( _Float77 * PI ) );
				float2 rotator726 = mul( panner725 - float2( 0.5,0.5 ) , float2x2( cos726 , -sin726 , sin726 , cos726 )) + float2( 0.5,0.5 );
				float2 break720 = rotator726;
				float clampResult721 = clamp( break720.x , 0.0 , 1.0 );
				float lerpResult787 = lerp( break720.x , clampResult721 , _Float78);
				float clampResult722 = clamp( break720.y , 0.0 , 1.0 );
				float lerpResult789 = lerp( break720.y , clampResult722 , _Float88);
				float2 appendResult723 = (float2(lerpResult787 , lerpResult789));
				float3 vertexoffset181 = ( (_Vector32.z + (tex2Dlod( _vertextex, float4( appendResult647, 0, 0.0) ).r - _Vector32.x) * (_Vector32.w - _Vector32.z) / (_Vector32.y - _Vector32.x)) * lerpResult993 * _Vector5 * lerpResult176 * tex2Dlod( _vertextex1, float4( appendResult723, 0, 0.0) ).r );
				
				float3 customSurfaceDepth754 = v.vertex.xyz;
				float customEye754 = -TransformWorldToView(TransformObjectToWorld(customSurfaceDepth754)).z;
				o.ase_texcoord3.x = customEye754;
				float3 vertexPos97 = v.vertex.xyz;
				float4 ase_clipPos97 = TransformObjectToHClip((vertexPos97).xyz);
				float4 screenPos97 = ComputeScreenPos(ase_clipPos97);
				o.ase_texcoord4 = screenPos97;
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord5 = screenPos;
				float3 ase_worldTangent = TransformObjectToWorldDir(v.ase_tangent.xyz);
				o.ase_texcoord8.xyz = ase_worldTangent;
				float3 ase_worldNormal = TransformObjectToWorldNormal(v.ase_normal);
				o.ase_texcoord9.xyz = ase_worldNormal;
				float ase_vertexTangentSign = v.ase_tangent.w * unity_WorldTransformParams.w;
				float3 ase_worldBitangent = cross( ase_worldNormal, ase_worldTangent ) * ase_vertexTangentSign;
				o.ase_texcoord10.xyz = ase_worldBitangent;
				
				o.ase_texcoord3.yzw = v.ase_texcoord.xyz;
				o.ase_texcoord6 = v.ase_texcoord1;
				o.ase_texcoord7 = v.ase_texcoord2;
				o.ase_color = v.ase_color;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord8.w = 0;
				o.ase_texcoord9.w = 0;
				o.ase_texcoord10.w = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = vertexoffset181;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float4 positionCS = TransformWorldToHClip( positionWS );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				o.worldPos = positionWS;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				VertexPositionInputs vertexInput = (VertexPositionInputs)0;
				vertexInput.positionWS = positionWS;
				vertexInput.positionCS = positionCS;
				o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				#ifdef ASE_FOG
				o.fogFactor = ComputeFogFactor( positionCS.z );
				#endif
				o.clipPos = positionCS;
				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_tangent : TANGENT;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_texcoord = v.ase_texcoord;
				o.ase_texcoord1 = v.ase_texcoord1;
				o.ase_texcoord2 = v.ase_texcoord2;
				o.ase_tangent = v.ase_tangent;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				o.ase_texcoord1 = patch[0].ase_texcoord1 * bary.x + patch[1].ase_texcoord1 * bary.y + patch[2].ase_texcoord1 * bary.z;
				o.ase_texcoord2 = patch[0].ase_texcoord2 * bary.x + patch[1].ase_texcoord2 * bary.y + patch[2].ase_texcoord2 * bary.z;
				o.ase_tangent = patch[0].ase_tangent * bary.x + patch[1].ase_tangent * bary.y + patch[2].ase_tangent * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag ( VertexOutput IN , half ase_vface : VFACE ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 WorldPosition = IN.worldPos;
				#endif
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif
				float customEye754 = IN.ase_texcoord3.x;
				float cameraDepthFade754 = (( customEye754 -_ProjectionParams.y - _Float0 ) / _Float55);
				float4 screenPos97 = IN.ase_texcoord4;
				float4 ase_screenPosNorm97 = screenPos97 / screenPos97.w;
				ase_screenPosNorm97.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm97.z : ase_screenPosNorm97.z * 0.5 + 0.5;
				float screenDepth97 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm97.xy ),_ZBufferParams);
				float distanceDepth97 = saturate( abs( ( screenDepth97 - LinearEyeDepth( ase_screenPosNorm97.z,_ZBufferParams ) ) / ( _Float16 ) ) );
				float depthfade_switch334 = _Float5;
				float lerpResult336 = lerp( distanceDepth97 , ( 1.0 - distanceDepth97 ) , depthfade_switch334);
				float depthfade126 = ( saturate( cameraDepthFade754 ) * lerpResult336 );
				float lerpResult330 = lerp( 0.0 , depthfade126 , depthfade_switch334);
				float2 appendResult440 = (float2(_Vector0.x , _Vector0.y));
				float2 uv_maintex = IN.ase_texcoord3.yzw.xy * _maintex_ST.xy + _maintex_ST.zw;
				float4 screenPos = IN.ase_texcoord5;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float2 appendResult1679 = (float2(ase_screenPosNorm.x , ase_screenPosNorm.y));
				float2 appendResult1131 = (float2(_maintex_ST.x , _maintex_ST.y));
				float2 appendResult1130 = (float2(_maintex_ST.z , _maintex_ST.w));
				float screenuv1625 = _Float144;
				float2 lerpResult1127 = lerp( uv_maintex , (appendResult1679*appendResult1131 + appendResult1130) , screenuv1625);
				float3 appendResult799 = (float3(1.0 , _Vector0.z , 0.0));
				float3 appendResult803 = (float3(_Vector0.w , 1.0 , 0.0));
				float2 appendResult433 = (float2(_maintex_ST.z , _maintex_ST.w));
				float4 texCoord1281 = IN.ase_texcoord6;
				texCoord1281.xy = IN.ase_texcoord6.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1694 = clamp( _Custom1 , 0.0 , 1.0 );
				float lerpResult1681 = lerp( texCoord1281.x , texCoord1281.y , clampResult1694);
				float clampResult1692 = clamp( ( _Custom1 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1683 = lerp( lerpResult1681 , texCoord1281.z , clampResult1692);
				float clampResult1693 = clamp( ( _Custom1 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1689 = lerp( lerpResult1683 , texCoord1281.w , clampResult1693);
				float4 texCoord1698 = IN.ase_texcoord7;
				texCoord1698.xy = IN.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1695 = clamp( ( _Custom1 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1697 = lerp( lerpResult1689 , texCoord1698.x , clampResult1695);
				float clampResult1699 = clamp( ( _Custom1 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1700 = lerp( lerpResult1697 , texCoord1698.y , clampResult1699);
				float clampResult1702 = clamp( ( _Custom1 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1704 = lerp( lerpResult1700 , texCoord1698.z , clampResult1702);
				float clampResult1705 = clamp( ( _Custom1 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1706 = lerp( lerpResult1704 , texCoord1698.w , clampResult1705);
				float custom11336 = lerpResult1706;
				float4 texCoord1728 = IN.ase_texcoord6;
				texCoord1728.xy = IN.ase_texcoord6.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1729 = clamp( _Custom2 , 0.0 , 1.0 );
				float lerpResult1710 = lerp( texCoord1728.x , texCoord1728.y , clampResult1729);
				float clampResult1709 = clamp( ( _Custom2 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1712 = lerp( lerpResult1710 , texCoord1728.z , clampResult1709);
				float clampResult1713 = clamp( ( _Custom2 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1717 = lerp( lerpResult1712 , texCoord1728.w , clampResult1713);
				float4 texCoord1718 = IN.ase_texcoord7;
				texCoord1718.xy = IN.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1715 = clamp( ( _Custom2 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1719 = lerp( lerpResult1717 , texCoord1718.x , clampResult1715);
				float clampResult1720 = clamp( ( _Custom2 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1722 = lerp( lerpResult1719 , texCoord1718.y , clampResult1720);
				float clampResult1723 = clamp( ( _Custom2 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1726 = lerp( lerpResult1722 , texCoord1718.z , clampResult1723);
				float clampResult1725 = clamp( ( _Custom2 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1727 = lerp( lerpResult1726 , texCoord1718.w , clampResult1725);
				float custom21337 = lerpResult1727;
				float2 appendResult42 = (float2(custom11336 , custom21337));
				float2 lerpResult59 = lerp( appendResult433 , appendResult42 , _Float10);
				float2 CenteredUV15_g104 = ( IN.ase_texcoord3.yzw.xy - float2( 0.5,0.5 ) );
				float2 break17_g104 = CenteredUV15_g104;
				float2 appendResult23_g104 = (float2(( length( CenteredUV15_g104 ) * _maintex_ST.x * 2.0 ) , ( atan2( break17_g104.x , break17_g104.y ) * ( 1.0 / TWO_PI ) * _maintex_ST.y )));
				float2 lerpResult449 = lerp( appendResult433 , appendResult42 , _Float10);
				float2 lerpResult444 = lerp( ( mul( float3( lerpResult1127 ,  0.0 ), float3x3(appendResult799, appendResult803, float3(0,0,1)) ).xy + lerpResult59 ) , (mul( float3( appendResult23_g104 ,  0.0 ), float3x3(appendResult799, appendResult803, float3(0,0,1)) ).xy*float2( 1,1 ) + lerpResult449) , _Float49);
				float2 panner36 = ( 1.0 * _Time.y * appendResult440 + lerpResult444);
				float2 maintexUV_00161 = panner36;
				float3 ase_worldTangent = IN.ase_texcoord8.xyz;
				float3 ase_worldNormal = IN.ase_texcoord9.xyz;
				float3 ase_worldBitangent = IN.ase_texcoord10.xyz;
				float3 tanToWorld0 = float3( ase_worldTangent.x, ase_worldBitangent.x, ase_worldNormal.x );
				float3 tanToWorld1 = float3( ase_worldTangent.y, ase_worldBitangent.y, ase_worldNormal.y );
				float3 tanToWorld2 = float3( ase_worldTangent.z, ase_worldBitangent.z, ase_worldNormal.z );
				float3 ase_worldViewDir = ( _WorldSpaceCameraPos.xyz - WorldPosition );
				ase_worldViewDir = normalize(ase_worldViewDir);
				float3 ase_tanViewDir =  tanToWorld0 * ase_worldViewDir.x + tanToWorld1 * ase_worldViewDir.y  + tanToWorld2 * ase_worldViewDir.z;
				ase_tanViewDir = normalize(ase_tanViewDir);
				float2 OffsetPOM1092 = POM( _parallax, maintexUV_00161, ddx(maintexUV_00161), ddy(maintexUV_00161), ase_worldNormal, ase_worldViewDir, ase_tanViewDir, 128, 128, ( _Float38 * 0.1 ), _refplane, _parallax_ST.xy, float2(0,0), 0 );
				float2 parallax1097 = OffsetPOM1092;
				float2 lerpResult1099 = lerp( maintexUV_00161 , parallax1097 , _Float143);
				float2 appendResult687 = (float2(_Vector23.x , _Vector23.y));
				float2 uv_noisemask = IN.ase_texcoord3.yzw.xy * _noisemask_ST.xy + _noisemask_ST.zw;
				float2 uv2_noisemask = IN.ase_texcoord6.xy * _noisemask_ST.xy + _noisemask_ST.zw;
				float uv2930 = _Float131;
				float2 lerpResult938 = lerp( uv_noisemask , uv2_noisemask , uv2930);
				float2 appendResult1629 = (float2(ase_screenPosNorm.x , ase_screenPosNorm.y));
				float2 appendResult1628 = (float2(_noisemask_ST.x , _noisemask_ST.y));
				float2 appendResult1633 = (float2(_noisemask_ST.z , _noisemask_ST.w));
				float2 lerpResult1631 = lerp( lerpResult938 , (appendResult1629*appendResult1628 + appendResult1633) , screenuv1625);
				float3 appendResult866 = (float3(1.0 , _Vector23.z , 0.0));
				float3 appendResult865 = (float3(_Vector23.w , 1.0 , 0.0));
				float2 appendResult679 = (float2(_noisemask_ST.z , _noisemask_ST.w));
				float2 CenteredUV15_g47 = ( IN.ase_texcoord3.yzw.xy - float2( 0.5,0.5 ) );
				float2 break17_g47 = CenteredUV15_g47;
				float2 appendResult23_g47 = (float2(( length( CenteredUV15_g47 ) * _noisemask_ST.x * 2.0 ) , ( atan2( break17_g47.x , break17_g47.y ) * ( 1.0 / TWO_PI ) * _noisemask_ST.y )));
				float2 lerpResult688 = lerp( ( mul( float3( lerpResult1631 ,  0.0 ), float3x3(appendResult866, appendResult865, float3(0,0,1)) ).xy + appendResult679 ) , (mul( float3( appendResult23_g47 ,  0.0 ), float3x3(appendResult866, appendResult865, float3(0,0,1)) ).xy*float2( 1,1 ) + appendResult679) , _Float57);
				float2 panner697 = ( 1.0 * _Time.y * appendResult687 + lerpResult688);
				float cos698 = cos( ( _Float74 * PI ) );
				float sin698 = sin( ( _Float74 * PI ) );
				float2 rotator698 = mul( panner697 - float2( 0.5,0.5 ) , float2x2( cos698 , -sin698 , sin698 , cos698 )) + float2( 0.5,0.5 );
				float2 break689 = rotator698;
				float clampResult690 = clamp( break689.x , 0.0 , 1.0 );
				float lerpResult775 = lerp( break689.x , clampResult690 , _Float73);
				float clampResult691 = clamp( break689.y , 0.0 , 1.0 );
				float lerpResult776 = lerp( break689.x , clampResult691 , _Float84);
				float2 appendResult693 = (float2(lerpResult775 , lerpResult776));
				float4 tex1DNode564 = tex1D( _noisemask, appendResult693.x );
				float2 appendResult530 = (float2(_Vector17.x , _Vector17.y));
				float2 uv_noise = IN.ase_texcoord3.yzw.xy * _noise_ST.xy + _noise_ST.zw;
				float2 uv2_noise = IN.ase_texcoord6.xy * _noise_ST.xy + _noise_ST.zw;
				float2 lerpResult941 = lerp( uv_noise , uv2_noise , uv2930);
				float2 appendResult1637 = (float2(ase_screenPosNorm.x , ase_screenPosNorm.y));
				float2 appendResult1636 = (float2(_noise_ST.x , _noise_ST.y));
				float2 appendResult1641 = (float2(_noise_ST.z , _noise_ST.w));
				float2 lerpResult1639 = lerp( lerpResult941 , (appendResult1637*appendResult1636 + appendResult1641) , screenuv1625);
				float3 appendResult876 = (float3(1.0 , _Vector17.z , 0.0));
				float3 appendResult878 = (float3(_Vector17.w , 1.0 , 0.0));
				float2 appendResult531 = (float2(_noise_ST.z , _noise_ST.w));
				float2 CenteredUV15_g46 = ( IN.ase_texcoord3.yzw.xy - float2( 0.5,0.5 ) );
				float2 break17_g46 = CenteredUV15_g46;
				float2 appendResult23_g46 = (float2(( length( CenteredUV15_g46 ) * _noise_ST.x * 2.0 ) , ( atan2( break17_g46.x , break17_g46.y ) * ( 1.0 / TWO_PI ) * _noise_ST.y )));
				float2 lerpResult539 = lerp( ( mul( float3( lerpResult1639 ,  0.0 ), float3x3(appendResult876, appendResult878, float3(0,0,1)) ).xy + appendResult531 ) , (mul( float3( appendResult23_g46 ,  0.0 ), float3x3(appendResult876, appendResult878, float3(0,0,1)) ).xy*float2( 1,1 ) + appendResult531) , _Float54);
				float2 panner53 = ( 1.0 * _Time.y * appendResult530 + lerpResult539);
				float cos395 = cos( ( _Float44 * PI ) );
				float sin395 = sin( ( _Float44 * PI ) );
				float2 rotator395 = mul( panner53 - float2( 0.5,0.5 ) , float2x2( cos395 , -sin395 , sin395 , cos395 )) + float2( 0.5,0.5 );
				float2 break638 = rotator395;
				float clampResult640 = clamp( break638.x , 0.0 , 1.0 );
				float lerpResult778 = lerp( break638.x , clampResult640 , _Float67);
				float clampResult639 = clamp( break638.y , 0.0 , 1.0 );
				float lerpResult780 = lerp( break638.y , clampResult639 , _Float85);
				float2 appendResult641 = (float2(lerpResult778 , lerpResult780));
				float temp_output_923_0 = (_Vector33.z + (tex2D( _noise, appendResult641 ).r - _Vector33.x) * (_Vector33.w - _Vector33.z) / (_Vector33.y - _Vector33.x));
				float lerpResult701 = lerp( ( tex1DNode564.r * temp_output_923_0 ) , ( tex1DNode564.r + temp_output_923_0 ) , _Float76);
				float noise70 = lerpResult701;
				float4 texCoord1952 = IN.ase_texcoord6;
				texCoord1952.xy = IN.ase_texcoord6.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1936 = clamp( _Custom12 , 0.0 , 1.0 );
				float lerpResult1939 = lerp( texCoord1952.x , texCoord1952.y , clampResult1936);
				float clampResult1940 = clamp( ( _Custom12 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1941 = lerp( lerpResult1939 , texCoord1952.z , clampResult1940);
				float clampResult1942 = clamp( ( _Custom12 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1945 = lerp( lerpResult1941 , texCoord1952.w , clampResult1942);
				float4 texCoord1947 = IN.ase_texcoord7;
				texCoord1947.xy = IN.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1946 = clamp( ( _Custom12 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1953 = lerp( lerpResult1945 , texCoord1947.x , clampResult1946);
				float clampResult1949 = clamp( ( _Custom12 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1954 = lerp( lerpResult1953 , texCoord1947.y , clampResult1949);
				float clampResult1950 = clamp( ( _Custom12 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1955 = lerp( lerpResult1954 , texCoord1947.z , clampResult1950);
				float clampResult1951 = clamp( ( _Custom12 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1956 = lerp( lerpResult1955 , texCoord1947.w , clampResult1951);
				float custom121574 = lerpResult1956;
				float lerpResult1575 = lerp( 1.0 , custom121574 , _Float257);
				float noise_intensity_main67 = ( _Float9 * 0.1 * lerpResult1575 );
				float2 uv_flowmap = IN.ase_texcoord3.yzw.xy * _flowmap_ST.xy + _flowmap_ST.zw;
				float4 tex2DNode1982 = tex2D( _flowmap, uv_flowmap );
				float2 appendResult242 = (float2(tex2DNode1982.r , tex2DNode1982.g));
				float2 flowmap285 = appendResult242;
				float flowmap_intensity311 = _Float32;
				float4 texCoord100 = IN.ase_texcoord7;
				texCoord100.xy = IN.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float flpwmap_custom_switch316 = _Float31;
				float lerpResult99 = lerp( flowmap_intensity311 , texCoord100.y , flpwmap_custom_switch316);
				float2 lerpResult283 = lerp( ( lerpResult1099 + ( noise70 * noise_intensity_main67 ) ) , flowmap285 , lerpResult99);
				float cos377 = cos( ( _Float39 * PI ) );
				float sin377 = sin( ( _Float39 * PI ) );
				float2 rotator377 = mul( lerpResult283 - float2( 0.5,0.5 ) , float2x2( cos377 , -sin377 , sin377 , cos377 )) + float2( 0.5,0.5 );
				float2 break603 = rotator377;
				float clampResult604 = clamp( break603.x , 0.0 , 1.0 );
				float lerpResult607 = lerp( break603.x , clampResult604 , _Float62);
				float clampResult605 = clamp( break603.y , 0.0 , 1.0 );
				float lerpResult764 = lerp( break603.y , clampResult605 , _Float71);
				float2 appendResult606 = (float2(lerpResult607 , lerpResult764));
				float2 appendResult1225 = (float2(_Vector16.x , _Vector16.y));
				float2 temp_output_1229_0 = ( 0.01 * appendResult1225 );
				float4 tex2DNode1 = tex2D( _maintex, appendResult606 );
				float3 appendResult1228 = (float3(tex2D( _maintex, ( appendResult606 - temp_output_1229_0 ) ).r , tex2DNode1.g , tex2D( _maintex, ( temp_output_1229_0 + appendResult606 ) ).b));
				float3 desaturateInitialColor2130 = appendResult1228;
				float desaturateDot2130 = dot( desaturateInitialColor2130, float3( 0.299, 0.587, 0.114 ));
				float3 desaturateVar2130 = lerp( desaturateInitialColor2130, desaturateDot2130.xxx, _Float61 );
				float2 appendResult464 = (float2(_Vector7.x , _Vector7.y));
				float2 uv_Ramptex = IN.ase_texcoord3.yzw.xy * _Ramptex_ST.xy + _Ramptex_ST.zw;
				float3 appendResult851 = (float3(1.0 , _Vector0.z , 0.0));
				float3 appendResult852 = (float3(_Vector0.w , 1.0 , 0.0));
				float2 temp_output_854_0 = mul( float3( uv_Ramptex ,  0.0 ), float3x3(appendResult851, appendResult852, float3(0,0,1)) ).xy;
				float2 appendResult454 = (float2(( _Vector9.x - 1.0 ) , ( _Vector9.y - 1.0 )));
				float2 appendResult457 = (float2(_Vector9.z , _Vector9.w));
				float2 CenteredUV15_g170 = ( uv_Ramptex - float2( 0.5,0.5 ) );
				float2 break17_g170 = CenteredUV15_g170;
				float2 appendResult23_g170 = (float2(( length( CenteredUV15_g170 ) * _Vector9.x * 2.0 ) , ( atan2( break17_g170.x , break17_g170.y ) * ( 1.0 / TWO_PI ) * _Vector9.y )));
				float2 lerpResult451 = lerp( (temp_output_854_0*appendResult454 + ( temp_output_854_0 + appendResult457 )) , (mul( float3( appendResult23_g170 ,  0.0 ), float3x3(appendResult851, appendResult852, float3(0,0,1)) ).xy*float2( 1,1 ) + appendResult457) , _Float159);
				float2 panner229 = ( 1.0 * _Time.y * appendResult464 + lerpResult451);
				float2 Gradienttex231 = panner229;
				float2 temp_cast_20 = (noise70).xx;
				float2 lerpResult235 = lerp( Gradienttex231 , temp_cast_20 , noise_intensity_main67);
				float3 clampResult2036 = clamp( desaturateVar2130 , float3( 0,0,0 ) , float3( 1,0,0 ) );
				float3 temp_cast_21 = (_Float152).xxx;
				float2 appendResult2034 = (float2(( 1.0 - pow( ( 1.0 - clampResult2036 ) , temp_cast_21 ) ).xy));
				float2 ramp_maintex2035 = appendResult2034;
				float clampResult2051 = clamp( _Float154 , 0.0 , 1.0 );
				float mapping_toggle2040 = clampResult2051;
				float2 lerpResult2037 = lerp( lerpResult235 , ramp_maintex2035 , mapping_toggle2040);
				float cos383 = cos( ( _Float40 * PI ) );
				float sin383 = sin( ( _Float40 * PI ) );
				float2 rotator383 = mul( lerpResult2037 - float2( 0.5,0.5 ) , float2x2( cos383 , -sin383 , sin383 , cos383 )) + float2( 0.5,0.5 );
				float2 break609 = rotator383;
				float clampResult610 = clamp( break609.x , 0.0 , 1.0 );
				float lerpResult765 = lerp( break609.x , clampResult610 , _Float63);
				float clampResult611 = clamp( break609.y , 0.0 , 1.0 );
				float lerpResult767 = lerp( break609.y , clampResult611 , _Float81);
				float2 appendResult768 = (float2(lerpResult765 , lerpResult767));
				float2 ramp_speed2090 = appendResult464;
				float2 appendResult2072 = (float2(_Vector9.x , _Vector9.y));
				float2 ramp_tilling2091 = appendResult2072;
				float2 ramp_offset2092 = appendResult457;
				float2 panner2060 = ( 1.0 * _Time.y * ramp_speed2090 + (appendResult768*ramp_tilling2091 + ramp_offset2092));
				float2 lerpResult2128 = lerp( appendResult768 , panner2060 , mapping_toggle2040);
				float4 tex2DNode212 = tex2D( _Ramptex, lerpResult2128 );
				float4 lerpResult2042 = lerp( ( float4( desaturateVar2130 , 0.0 ) * tex2DNode212 ) , tex2DNode212 , _Float154);
				float clampResult2084 = clamp( ( _Float154 - 1.0 ) , 0.0 , 1.0 );
				float4 lerpResult2087 = lerp( lerpResult2042 , float4( desaturateVar2130 , 0.0 ) , clampResult2084);
				float clampResult2134 = clamp( ( _Float154 - 2.0 ) , 0.0 , 1.0 );
				float4 lerpResult2136 = lerp( lerpResult2087 , float4( desaturateVar2130 , 0.0 ) , clampResult2134);
				float4 lerpResult359 = lerp( _Color0 , _Color3 , _Float35);
				float4 switchResult356 = (((ase_vface>0)?(_Color0):(lerpResult359)));
				ase_worldViewDir = SafeNormalize( ase_worldViewDir );
				float3 normalizedWorldNormal = normalize( ase_worldNormal );
				float fresnelNdotV139 = dot( normalize( ( normalizedWorldNormal * ase_vface ) ), ase_worldViewDir );
				float fresnelNode139 = ( 0.0 + _power3 * pow( max( 1.0 - fresnelNdotV139 , 0.0001 ), _Float19 ) );
				float temp_output_140_0 = saturate( fresnelNode139 );
				float lerpResult144 = lerp( temp_output_140_0 , ( 1.0 - temp_output_140_0 ) , _Float20);
				float fresnel147 = lerpResult144;
				float4 lerpResult1135 = lerp( switchResult356 , _Color6 , fresnel147);
				float4 lerpResult1145 = lerp( switchResult356 , lerpResult1135 , _Float145);
				float lerpResult347 = lerp( 1.0 , fresnel147 , _Float33);
				float4 temp_cast_25 = (1.0).xxxx;
				float clampResult2108 = clamp( lerpResult144 , 0.0 , 1.0 );
				float ramppower2057 = _Float152;
				float2 appendResult2113 = (float2(( 1.0 - pow( ( 1.0 - clampResult2108 ) , ramppower2057 ) ) , 0.0));
				float2 ramp_fre2114 = appendResult2113;
				float2 panner2121 = ( 1.0 * _Time.y * ramp_speed2090 + (ramp_fre2114*ramp_tilling2091 + ramp_offset2092));
				float ramp_rotater2144 = ( _Float40 * PI );
				float cos2143 = cos( ramp_rotater2144 );
				float sin2143 = sin( ramp_rotater2144 );
				float2 rotator2143 = mul( panner2121 - float2( 0.5,0.5 ) , float2x2( cos2143 , -sin2143 , sin2143 , cos2143 )) + float2( 0.5,0.5 );
				float clampResult2124 = clamp( ( _Float154 - 3.0 ) , 0.0 , 1.0 );
				float4 lerpResult2126 = lerp( temp_cast_25 , tex2D( _Ramptex, rotator2143 ) , clampResult2124);
				float4 temp_cast_26 = (1.0).xxxx;
				float2 appendResult501 = (float2(_Vector15.x , _Vector15.y));
				float2 uv_dissolvetex = IN.ase_texcoord3.yzw.xy * _dissolvetex_ST.xy + _dissolvetex_ST.zw;
				float2 uv2_dissolvetex = IN.ase_texcoord6.xy * _dissolvetex_ST.xy + _dissolvetex_ST.zw;
				float2 lerpResult952 = lerp( uv_dissolvetex , uv2_dissolvetex , uv2930);
				float2 appendResult1644 = (float2(ase_screenPosNorm.x , ase_screenPosNorm.y));
				float2 appendResult1643 = (float2(_dissolvetex_ST.x , _dissolvetex_ST.y));
				float2 appendResult1648 = (float2(_dissolvetex_ST.z , _dissolvetex_ST.w));
				float2 lerpResult1646 = lerp( lerpResult952 , (appendResult1644*appendResult1643 + appendResult1648) , screenuv1625);
				float3 appendResult810 = (float3(1.0 , _Vector15.z , 0.0));
				float3 appendResult811 = (float3(_Vector15.w , 1.0 , 0.0));
				float2 appendResult502 = (float2(_dissolvetex_ST.z , _dissolvetex_ST.w));
				float4 texCoord1779 = IN.ase_texcoord6;
				texCoord1779.xy = IN.ase_texcoord6.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1777 = clamp( _Custom5 , 0.0 , 1.0 );
				float lerpResult1781 = lerp( texCoord1779.x , texCoord1779.y , clampResult1777);
				float clampResult1782 = clamp( ( _Custom5 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1785 = lerp( lerpResult1781 , texCoord1779.z , clampResult1782);
				float clampResult1783 = clamp( ( _Custom5 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1788 = lerp( lerpResult1785 , texCoord1779.w , clampResult1783);
				float4 texCoord1789 = IN.ase_texcoord7;
				texCoord1789.xy = IN.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1787 = clamp( ( _Custom5 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1791 = lerp( lerpResult1788 , texCoord1789.x , clampResult1787);
				float clampResult1792 = clamp( ( _Custom5 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1793 = lerp( lerpResult1791 , texCoord1789.y , clampResult1792);
				float clampResult1795 = clamp( ( _Custom5 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1796 = lerp( lerpResult1793 , texCoord1789.z , clampResult1795);
				float clampResult1797 = clamp( ( _Custom5 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1798 = lerp( lerpResult1796 , texCoord1789.w , clampResult1797);
				float custom51401 = lerpResult1798;
				float4 texCoord1802 = IN.ase_texcoord6;
				texCoord1802.xy = IN.ase_texcoord6.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1800 = clamp( _Custom6 , 0.0 , 1.0 );
				float lerpResult1804 = lerp( texCoord1802.x , texCoord1802.y , clampResult1800);
				float clampResult1805 = clamp( ( _Custom6 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1808 = lerp( lerpResult1804 , texCoord1802.z , clampResult1805);
				float clampResult1806 = clamp( ( _Custom6 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1811 = lerp( lerpResult1808 , texCoord1802.w , clampResult1806);
				float4 texCoord1812 = IN.ase_texcoord7;
				texCoord1812.xy = IN.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1810 = clamp( ( _Custom6 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1814 = lerp( lerpResult1811 , texCoord1812.x , clampResult1810);
				float clampResult1815 = clamp( ( _Custom6 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1816 = lerp( lerpResult1814 , texCoord1812.y , clampResult1815);
				float clampResult1818 = clamp( ( _Custom6 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1819 = lerp( lerpResult1816 , texCoord1812.z , clampResult1818);
				float clampResult1820 = clamp( ( _Custom6 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1821 = lerp( lerpResult1819 , texCoord1812.w , clampResult1820);
				float custom61420 = lerpResult1821;
				float2 appendResult1425 = (float2(custom51401 , custom61420));
				float2 lerpResult1426 = lerp( appendResult502 , appendResult1425 , _Float50);
				float2 CenteredUV15_g105 = ( IN.ase_texcoord3.yzw.xy - float2( 0.5,0.5 ) );
				float2 break17_g105 = CenteredUV15_g105;
				float2 appendResult23_g105 = (float2(( length( CenteredUV15_g105 ) * _dissolvetex_ST.x * 2.0 ) , ( atan2( break17_g105.x , break17_g105.y ) * ( 1.0 / TWO_PI ) * _dissolvetex_ST.y )));
				float2 lerpResult1422 = lerp( appendResult502 , appendResult1425 , _Float50);
				float2 lerpResult511 = lerp( ( mul( float3( lerpResult1646 ,  0.0 ), float3x3(appendResult810, appendResult811, float3(0,0,1)) ).xy + lerpResult1426 ) , (mul( float3( appendResult23_g105 ,  0.0 ), float3x3(appendResult810, appendResult811, float3(0,0,1)) ).xy*float2( 1,1 ) + lerpResult1422) , _Float53);
				float2 panner58 = ( 1.0 * _Time.y * appendResult501 + lerpResult511);
				float noise_intensity_dis733 = ( _Float79 * 0.1 * lerpResult1575 );
				float4 texCoord1846 = IN.ase_texcoord6;
				texCoord1846.xy = IN.ase_texcoord6.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1847 = clamp( _Custom8 , 0.0 , 1.0 );
				float lerpResult1849 = lerp( texCoord1846.x , texCoord1846.y , clampResult1847);
				float clampResult1848 = clamp( ( _Custom8 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1853 = lerp( lerpResult1849 , texCoord1846.z , clampResult1848);
				float clampResult1851 = clamp( ( _Custom8 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1855 = lerp( lerpResult1853 , texCoord1846.w , clampResult1851);
				float4 texCoord1856 = IN.ase_texcoord7;
				texCoord1856.xy = IN.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1857 = clamp( ( _Custom8 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1858 = lerp( lerpResult1855 , texCoord1856.x , clampResult1857);
				float clampResult1859 = clamp( ( _Custom8 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1863 = lerp( lerpResult1858 , texCoord1856.y , clampResult1859);
				float clampResult1861 = clamp( ( _Custom8 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1864 = lerp( lerpResult1863 , texCoord1856.z , clampResult1861);
				float clampResult1865 = clamp( ( _Custom8 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1866 = lerp( lerpResult1864 , texCoord1856.w , clampResult1865);
				float custom81489 = lerpResult1866;
				float lerpResult307 = lerp( flowmap_intensity311 , custom81489 , flpwmap_custom_switch316);
				float2 lerpResult309 = lerp( ( panner58 + ( noise70 * noise_intensity_dis733 ) ) , flowmap285 , lerpResult307);
				float2 dissolveUV92 = lerpResult309;
				float cos386 = cos( ( _Float41 * PI ) );
				float sin386 = sin( ( _Float41 * PI ) );
				float2 rotator386 = mul( dissolveUV92 - float2( 0.5,0.5 ) , float2x2( cos386 , -sin386 , sin386 , cos386 )) + float2( 0.5,0.5 );
				float2 break635 = rotator386;
				float clampResult632 = clamp( break635.x , 0.0 , 1.0 );
				float lerpResult784 = lerp( break635.x , clampResult632 , _Float66);
				float clampResult633 = clamp( break635.y , 0.0 , 1.0 );
				float lerpResult786 = lerp( break635.y , clampResult633 , _Float87);
				float2 appendResult631 = (float2(lerpResult784 , lerpResult786));
				float4 tex2DNode23 = tex2D( _dissolvetex, appendResult631 );
				float lerpResult1623 = lerp( tex2DNode23.a , tex2DNode23.r , _maintex_alpha1);
				float clampResult2004 = clamp( ( _maintex_alpha1 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult2006 = lerp( lerpResult1623 , tex2DNode23.g , clampResult2004);
				float clampResult2005 = clamp( ( _maintex_alpha1 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult2007 = lerp( lerpResult2006 , tex2DNode23.b , clampResult2005);
				float2 uv_TextureSample1 = IN.ase_texcoord3.yzw.xy * _TextureSample1_ST.xy + _TextureSample1_ST.zw;
				float2 uv2_TextureSample1 = IN.ase_texcoord6.xy * _TextureSample1_ST.xy + _TextureSample1_ST.zw;
				float2 lerpResult955 = lerp( uv_TextureSample1 , uv2_TextureSample1 , uv2930);
				float2 CenteredUV15_g165 = ( IN.ase_texcoord3.yzw.xy - float2( 0.5,0.5 ) );
				float2 break17_g165 = CenteredUV15_g165;
				float2 appendResult23_g165 = (float2(( length( CenteredUV15_g165 ) * 1.0 * 2.0 ) , ( atan2( break17_g165.x , break17_g165.y ) * ( 1.0 / TWO_PI ) * 1.0 )));
				float2 lerpResult568 = lerp( lerpResult955 , appendResult23_g165 , _Float53);
				float cos417 = cos( ( _Float47 * PI ) );
				float sin417 = sin( ( _Float47 * PI ) );
				float2 rotator417 = mul( lerpResult568 - float2( 0.5,0.5 ) , float2x2( cos417 , -sin417 , sin417 , cos417 )) + float2( 0.5,0.5 );
				float3 objToWorld1197 = mul( GetObjectToWorldMatrix(), float4( float3( 0,0,0 ), 1 ) ).xyz;
				float3 appendResult1017 = (float3(_Vector35.x , _Vector35.y , _Vector35.z));
				float3 lerpResult1199 = lerp( ( objToWorld1197 + appendResult1017 ) , appendResult1017 , _Float155);
				float lerpResult1057 = lerp( tex2D( _TextureSample1, rotator417 ).r , distance( lerpResult1199 , WorldPosition ) , _Float139);
				float dis_direction277 = lerpResult1057;
				float lerpResult1060 = lerp( _Float130 , ( 1.0 - _Float130 ) , _Float139);
				float lerpResult916 = lerp( lerpResult2007 , dis_direction277 , lerpResult1060);
				float clampResult2052 = clamp( lerpResult916 , 0.0 , 1.0 );
				float2 appendResult2056 = (float2(( 1.0 - pow( ( 1.0 - clampResult2052 ) , ramppower2057 ) ) , 0.0));
				float2 ramp_distex2059 = appendResult2056;
				float2 panner2088 = ( 1.0 * _Time.y * ramp_speed2090 + (ramp_distex2059*ramp_tilling2091 + ramp_offset2092));
				float cos2146 = cos( ramp_rotater2144 );
				float sin2146 = sin( ramp_rotater2144 );
				float2 rotator2146 = mul( panner2088 - float2( 0.5,0.5 ) , float2x2( cos2146 , -sin2146 , sin2146 , cos2146 )) + float2( 0.5,0.5 );
				float4 lerpResult2080 = lerp( temp_cast_26 , tex2D( _Ramptex, rotator2146 ) , clampResult2084);
				float4 texCoord1824 = IN.ase_texcoord6;
				texCoord1824.xy = IN.ase_texcoord6.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1825 = clamp( _Custom7 , 0.0 , 1.0 );
				float lerpResult1827 = lerp( texCoord1824.x , texCoord1824.y , clampResult1825);
				float clampResult1826 = clamp( ( _Custom7 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1831 = lerp( lerpResult1827 , texCoord1824.z , clampResult1826);
				float clampResult1829 = clamp( ( _Custom7 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1833 = lerp( lerpResult1831 , texCoord1824.w , clampResult1829);
				float4 texCoord1834 = IN.ase_texcoord7;
				texCoord1834.xy = IN.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1835 = clamp( ( _Custom7 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1836 = lerp( lerpResult1833 , texCoord1834.x , clampResult1835);
				float clampResult1837 = clamp( ( _Custom7 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1841 = lerp( lerpResult1836 , texCoord1834.y , clampResult1837);
				float clampResult1839 = clamp( ( _Custom7 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1842 = lerp( lerpResult1841 , texCoord1834.z , clampResult1839);
				float clampResult1843 = clamp( ( _Custom7 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1844 = lerp( lerpResult1842 , texCoord1834.w , clampResult1843);
				float custom71466 = lerpResult1844;
				float lerpResult62 = lerp( _Float6 , custom71466 , _Float11);
				float lerpResult913 = lerp( lerpResult62 , ( 1.0 - IN.ase_color.a ) , _Float129);
				float dis_tex661 = lerpResult916;
				float temp_output_130_0 = (0.0 + (dis_tex661 - -0.5) * (1.0 - 0.0) / (1.5 - -0.5));
				float temp_output_105_0 = step( lerpResult913 , temp_output_130_0 );
				float temp_output_109_0 = ( lerpResult913 + ( _Float17 * 0.1 ) );
				float temp_output_1263_0 = ( temp_output_105_0 - step( temp_output_109_0 , temp_output_130_0 ) );
				float dis_edge21241 = ( temp_output_1263_0 - step( ( temp_output_109_0 + ( _Float160 * -0.1 ) ) , temp_output_130_0 ) );
				float4 lerpResult1242 = lerp( ( _Color7 * lerpResult2080 ) , ( lerpResult2080 * _Color1 ) , dis_edge21241);
				float temp_output_45_0 = saturate( ( ( lerpResult916 + 1.0 ) - ( lerpResult913 * 2.0 ) ) );
				float4 texCoord1869 = IN.ase_texcoord6;
				texCoord1869.xy = IN.ase_texcoord6.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1868 = clamp( _Custom9 , 0.0 , 1.0 );
				float lerpResult1872 = lerp( texCoord1869.x , texCoord1869.y , clampResult1868);
				float clampResult1873 = clamp( ( _Custom9 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1876 = lerp( lerpResult1872 , texCoord1869.z , clampResult1873);
				float clampResult1874 = clamp( ( _Custom9 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1878 = lerp( lerpResult1876 , texCoord1869.w , clampResult1874);
				float4 texCoord1879 = IN.ase_texcoord7;
				texCoord1879.xy = IN.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1880 = clamp( ( _Custom9 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1883 = lerp( lerpResult1878 , texCoord1879.x , clampResult1880);
				float clampResult1881 = clamp( ( _Custom9 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1884 = lerp( lerpResult1883 , texCoord1879.y , clampResult1881);
				float clampResult1886 = clamp( ( _Custom9 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1888 = lerp( lerpResult1884 , texCoord1879.z , clampResult1886);
				float clampResult1887 = clamp( ( _Custom9 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1889 = lerp( lerpResult1888 , texCoord1879.w , clampResult1887);
				float custom91511 = lerpResult1889;
				float lerpResult1090 = lerp( _Float8 , custom91511 , _Float142);
				float smoothstepResult32 = smoothstep( lerpResult1090 , ( 1.0 - lerpResult1090 ) , temp_output_45_0);
				float dis_soft_edge1271 = saturate( ( temp_output_45_0 - smoothstepResult32 ) );
				float dis_edge133 = temp_output_1263_0;
				float lerpResult1272 = lerp( dis_soft_edge1271 , dis_edge133 , _Float25);
				float4 lerpResult131 = lerp( ( ( ( _Float28 * pow( lerpResult330 , _Float30 ) ) + lerpResult2136 ) * IN.ase_color * lerpResult1145 * lerpResult347 * lerpResult2126 ) , lerpResult1242 , lerpResult1272);
				float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( screenPos );
				float4 ase_grabScreenPosNorm = ase_grabScreenPos / ase_grabScreenPos.w;
				float2 appendResult580 = (float2(_Vector21.x , _Vector21.y));
				float2 uv_reftex = IN.ase_texcoord3.yzw.xy * _reftex_ST.xy + _reftex_ST.zw;
				float3 appendResult906 = (float3(1.0 , _Vector21.z , 0.0));
				float3 appendResult908 = (float3(_Vector21.w , 1.0 , 0.0));
				float2 appendResult579 = (float2(_reftex_ST.z , _reftex_ST.w));
				float2 CenteredUV15_g169 = ( IN.ase_texcoord3.yzw.xy - float2( 0.5,0.5 ) );
				float2 break17_g169 = CenteredUV15_g169;
				float2 appendResult23_g169 = (float2(( length( CenteredUV15_g169 ) * _reftex_ST.x * 2.0 ) , ( atan2( break17_g169.x , break17_g169.y ) * ( 1.0 / TWO_PI ) * _reftex_ST.y )));
				float2 lerpResult585 = lerp( ( mul( float3( uv_reftex ,  0.0 ), float3x3(appendResult906, appendResult908, float3(0,0,1)) ).xy + appendResult579 ) , (mul( float3( appendResult23_g169 ,  0.0 ), float3x3(appendResult906, appendResult908, float3(0,0,1)) ).xy*float2( 1,1 ) + appendResult579) , _Float58);
				float2 panner188 = ( 1.0 * _Time.y * appendResult580 + lerpResult585);
				float cos589 = cos( ( _Float59 * PI ) );
				float sin589 = sin( ( _Float59 * PI ) );
				float2 rotator589 = mul( panner188 - float2( 0.5,0.5 ) , float2x2( cos589 , -sin589 , sin589 , cos589 )) + float2( 0.5,0.5 );
				float2 break650 = rotator589;
				float clampResult652 = clamp( break650.x , 0.0 , 1.0 );
				float lerpResult781 = lerp( break650.x , clampResult652 , _Float69);
				float clampResult651 = clamp( break650.y , 0.0 , 1.0 );
				float lerpResult782 = lerp( break650.y , clampResult651 , _Float86);
				float2 appendResult653 = (float2(lerpResult781 , lerpResult782));
				float4 texCoord1891 = IN.ase_texcoord6;
				texCoord1891.xy = IN.ase_texcoord6.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1890 = clamp( _Custom10 , 0.0 , 1.0 );
				float lerpResult1894 = lerp( texCoord1891.x , texCoord1891.y , clampResult1890);
				float clampResult1895 = clamp( ( _Custom10 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1898 = lerp( lerpResult1894 , texCoord1891.z , clampResult1895);
				float clampResult1896 = clamp( ( _Custom10 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1900 = lerp( lerpResult1898 , texCoord1891.w , clampResult1896);
				float4 texCoord1912 = IN.ase_texcoord7;
				texCoord1912.xy = IN.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1901 = clamp( ( _Custom10 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1904 = lerp( lerpResult1900 , texCoord1912.x , clampResult1901);
				float clampResult1902 = clamp( ( _Custom10 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1905 = lerp( lerpResult1904 , texCoord1912.y , clampResult1902);
				float clampResult1907 = clamp( ( _Custom10 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1909 = lerp( lerpResult1905 , texCoord1912.z , clampResult1907);
				float clampResult1908 = clamp( ( _Custom10 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1910 = lerp( lerpResult1909 , texCoord1912.w , clampResult1908);
				float custom101531 = lerpResult1910;
				float lerpResult193 = lerp( _Float23 , custom101531 , _Float24);
				float3 unpack190 = UnpackNormalScale( tex2D( _reftex, appendResult653 ), ( 0.01 * lerpResult193 ) );
				unpack190.z = lerp( 1, unpack190.z, saturate(( 0.01 * lerpResult193 )) );
				float4 temp_output_185_0 = ( ase_grabScreenPosNorm + float4( unpack190 , 0.0 ) );
				float4 ref196 = tex2D( _PandaGrabTex, temp_output_185_0.xy );
				float4 lerpResult422 = lerp( lerpResult131 , ( ref196 * _Color2 * IN.ase_color ) , _Float48);
				float2 appendResult1178 = (float2(_Vector10.x , _Vector10.y));
				float3 appendResult1172 = (float3(1.0 , _Vector10.z , 0.0));
				float3 appendResult1173 = (float3(_Vector10.w , 1.0 , 0.0));
				float2 uv_normallight = IN.ase_texcoord3.yzw.xy * _normallight_ST.xy + _normallight_ST.zw;
				float2 panner1177 = ( 1.0 * _Time.y * appendResult1178 + mul( float3x3(appendResult1172, appendResult1173, float3(0,0,1)), float3( uv_normallight ,  0.0 ) ).xy);
				float3 unpack1147 = UnpackNormalScale( tex2D( _normallight, panner1177 ), _Float146 );
				unpack1147.z = lerp( 1, unpack1147.z, saturate(_Float146) );
				float3 tex2DNode1147 = unpack1147;
				float3 tanNormal957 = tex2DNode1147;
				float3 worldNormal957 = normalize( float3(dot(tanToWorld0,tanNormal957), dot(tanToWorld1,tanNormal957), dot(tanToWorld2,tanNormal957)) );
				float dotResult960 = dot( _MainLightPosition.xyz , worldNormal957 );
				float3 appendResult996 = (float3(_Vector34.x , _Vector34.y , _Vector34.z));
				float pointlight1007 = distance( appendResult996 , WorldPosition );
				float lerpResult1008 = lerp( dotResult960 , pointlight1007 , _Float137);
				float smoothstepResult968 = smoothstep( ( 1.0 - _Float133 ) , _Float133 , ( ( lerpResult1008 + 1.0 ) - ( _Float132 * 2.0 ) ));
				float lit971 = smoothstepResult968;
				float4 lerpResult969 = lerp( ( _Color4 * lerpResult422 ) , ( _Color5 * lerpResult422 ) , lit971);
				float4 lerpResult977 = lerp( lerpResult422 , lerpResult969 , _Float134);
				float4 temp_cast_39 = (1.0).xxxx;
				float3 normal_1111598 = tex2DNode1147;
				float3 tanNormal1064 = normal_1111598;
				float3 worldNormal1064 = float3(dot(tanToWorld0,tanNormal1064), dot(tanToWorld1,tanNormal1064), dot(tanToWorld2,tanNormal1064));
				float3 desaturateInitialColor1210 = tex2D( _matcap, ( ( (mul( UNITY_MATRIX_V, float4( worldNormal1064 , 0.0 ) ).xyz).xy + float2( 1,1 ) ) * float2( 0.5,0.5 ) ) ).rgb;
				float desaturateDot1210 = dot( desaturateInitialColor1210, float3( 0.299, 0.587, 0.114 ));
				float3 desaturateVar1210 = lerp( desaturateInitialColor1210, desaturateDot1210.xxx, _Float157 );
				float3 tanNormal1591 = normal_1111598;
				float3 worldNormal1591 = float3(dot(tanToWorld0,tanNormal1591), dot(tanToWorld1,tanNormal1591), dot(tanToWorld2,tanNormal1591));
				float4 cubemap1593 = ( texCUBE( _cubemap, reflect( -ase_worldViewDir , worldNormal1591 ) ) * _Float258 );
				float4 matcap1074 = ( float4( ( desaturateVar1210 * _Float140 ) , 0.0 ) + cubemap1593 );
				float4 lerpResult1078 = lerp( temp_cast_39 , matcap1074 , _Float141);
				float4 temp_output_928_0 = ( ( lerpResult977 * lerpResult1078 ) * _Float14 );
				
				float lerpResult402 = lerp( tex2DNode1.a , tex2DNode1.r , _maintex_alpha);
				float clampResult1985 = clamp( ( _maintex_alpha - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1983 = lerp( lerpResult402 , tex2DNode1.g , clampResult1985);
				float clampResult1987 = clamp( ( _maintex_alpha - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1988 = lerp( lerpResult1983 , tex2DNode1.b , clampResult1987);
				float lerpResult374 = lerp( lerpResult1988 , ( pow( abs( lerpResult1988 ) , _Float34 ) * _Float37 ) , _Float36);
				float dis_soft122 = smoothstepResult32;
				float dis_bright124 = temp_output_105_0;
				float lerpResult413 = lerp( dis_soft122 , dis_bright124 , _Float25);
				float lerpResult338 = lerp( depthfade126 , 1.0 , depthfade_switch334);
				float2 appendResult480 = (float2(_Vector11.x , _Vector11.y));
				float2 uv_Mask = IN.ase_texcoord3.yzw.xy * _Mask_ST.xy + _Mask_ST.zw;
				float2 uv2_Mask = IN.ase_texcoord6.xy * _Mask_ST.xy + _Mask_ST.zw;
				float2 lerpResult931 = lerp( uv_Mask , uv2_Mask , uv2930);
				float3 appendResult823 = (float3(1.0 , _Vector11.z , 0.0));
				float3 appendResult824 = (float3(_Vector11.w , 1.0 , 0.0));
				float2 appendResult481 = (float2(_Mask_ST.z , _Mask_ST.w));
				float4 texCoord1750 = IN.ase_texcoord6;
				texCoord1750.xy = IN.ase_texcoord6.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1751 = clamp( _Custom3 , 0.0 , 1.0 );
				float lerpResult1732 = lerp( texCoord1750.x , texCoord1750.y , clampResult1751);
				float clampResult1731 = clamp( ( _Custom3 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1734 = lerp( lerpResult1732 , texCoord1750.z , clampResult1731);
				float clampResult1735 = clamp( ( _Custom3 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1739 = lerp( lerpResult1734 , texCoord1750.w , clampResult1735);
				float4 texCoord1740 = IN.ase_texcoord7;
				texCoord1740.xy = IN.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1737 = clamp( ( _Custom3 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1741 = lerp( lerpResult1739 , texCoord1740.x , clampResult1737);
				float clampResult1742 = clamp( ( _Custom3 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1744 = lerp( lerpResult1741 , texCoord1740.y , clampResult1742);
				float clampResult1745 = clamp( ( _Custom3 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1748 = lerp( lerpResult1744 , texCoord1740.z , clampResult1745);
				float clampResult1747 = clamp( ( _Custom3 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1749 = lerp( lerpResult1748 , texCoord1740.w , clampResult1747);
				float custom31359 = lerpResult1749;
				float4 texCoord1755 = IN.ase_texcoord6;
				texCoord1755.xy = IN.ase_texcoord6.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1754 = clamp( _Custom4 , 0.0 , 1.0 );
				float lerpResult1758 = lerp( texCoord1755.x , texCoord1755.y , clampResult1754);
				float clampResult1757 = clamp( ( _Custom4 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1760 = lerp( lerpResult1758 , texCoord1755.z , clampResult1757);
				float clampResult1761 = clamp( ( _Custom4 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1765 = lerp( lerpResult1760 , texCoord1755.w , clampResult1761);
				float4 texCoord1766 = IN.ase_texcoord7;
				texCoord1766.xy = IN.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1763 = clamp( ( _Custom4 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1768 = lerp( lerpResult1765 , texCoord1766.x , clampResult1763);
				float clampResult1769 = clamp( ( _Custom4 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1770 = lerp( lerpResult1768 , texCoord1766.y , clampResult1769);
				float clampResult1771 = clamp( ( _Custom4 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1773 = lerp( lerpResult1770 , texCoord1766.z , clampResult1771);
				float clampResult1774 = clamp( ( _Custom4 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1775 = lerp( lerpResult1773 , texCoord1766.w , clampResult1774);
				float custom41379 = lerpResult1775;
				float2 appendResult75 = (float2(custom31359 , custom41379));
				float2 lerpResult474 = lerp( appendResult481 , appendResult75 , _Float12);
				float2 CenteredUV15_g164 = ( IN.ase_texcoord3.yzw.xy - float2( 0.5,0.5 ) );
				float2 break17_g164 = CenteredUV15_g164;
				float2 appendResult23_g164 = (float2(( length( CenteredUV15_g164 ) * _Mask_ST.x * 2.0 ) , ( atan2( break17_g164.x , break17_g164.y ) * ( 1.0 / TWO_PI ) * _Mask_ST.y )));
				float2 lerpResult471 = lerp( appendResult481 , appendResult75 , _Float12);
				float2 lerpResult467 = lerp( ( mul( float3( lerpResult931 ,  0.0 ), float3x3(appendResult823, appendResult824, float3(0,0,1)) ).xy + lerpResult474 ) , (mul( float3( appendResult23_g164 ,  0.0 ), float3x3(appendResult823, appendResult824, float3(0,0,1)) ).xy*float2( 1,1 ) + lerpResult471) , _Float51);
				float2 panner79 = ( 1.0 * _Time.y * appendResult480 + lerpResult467);
				float noise_intensity_mask736 = ( _Float80 * 0.1 * lerpResult1575 );
				float temp_output_322_0 = ( noise70 * noise_intensity_mask736 );
				float cos392 = cos( ( _Float43 * PI ) );
				float sin392 = sin( ( _Float43 * PI ) );
				float2 rotator392 = mul( ( panner79 + temp_output_322_0 ) - float2( 0.5,0.5 ) , float2x2( cos392 , -sin392 , sin392 , cos392 )) + float2( 0.5,0.5 );
				float2 break617 = rotator392;
				float clampResult618 = clamp( break617.x , 0.0 , 1.0 );
				float lerpResult769 = lerp( break617.x , clampResult618 , _Float64);
				float clampResult619 = clamp( break617.y , 0.0 , 1.0 );
				float lerpResult771 = lerp( break617.y , clampResult619 , _Float82);
				float2 appendResult616 = (float2(lerpResult769 , lerpResult771));
				float4 tex2DNode8 = tex2D( _Mask, appendResult616 );
				float lerpResult406 = lerp( tex2DNode8.a , tex2DNode8.r , _mask01_alpha);
				float clampResult1993 = clamp( ( _mask01_alpha - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1994 = lerp( lerpResult406 , tex2DNode8.g , clampResult1993);
				float clampResult1990 = clamp( ( _mask01_alpha - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1989 = lerp( lerpResult1994 , tex2DNode8.b , clampResult1990);
				float2 appendResult485 = (float2(_Vector13.x , _Vector13.y));
				float2 uv_Mask1 = IN.ase_texcoord3.yzw.xy * _Mask1_ST.xy + _Mask1_ST.zw;
				float2 uv2_Mask1 = IN.ase_texcoord6.xy * _Mask1_ST.xy + _Mask1_ST.zw;
				float2 lerpResult934 = lerp( uv_Mask1 , uv2_Mask1 , uv2930);
				float3 appendResult833 = (float3(1.0 , _Vector13.z , 0.0));
				float3 appendResult834 = (float3(_Vector13.w , 1.0 , 0.0));
				float2 appendResult486 = (float2(_Mask1_ST.z , _Mask1_ST.w));
				float2 CenteredUV15_g163 = ( IN.ase_texcoord3.yzw.xy - float2( 0.5,0.5 ) );
				float2 break17_g163 = CenteredUV15_g163;
				float2 appendResult23_g163 = (float2(( length( CenteredUV15_g163 ) * _Mask1_ST.x * 2.0 ) , ( atan2( break17_g163.x , break17_g163.y ) * ( 1.0 / TWO_PI ) * _Mask1_ST.y )));
				float2 lerpResult495 = lerp( ( mul( float3( lerpResult934 ,  0.0 ), float3x3(appendResult833, appendResult834, float3(0,0,1)) ).xy + appendResult486 ) , (mul( float3( appendResult23_g163 ,  0.0 ), float3x3(appendResult833, appendResult834, float3(0,0,1)) ).xy*float2( 1,1 ) + appendResult486) , _Float52);
				float2 panner216 = ( 1.0 * _Time.y * appendResult485 + lerpResult495);
				float cos389 = cos( ( _Float42 * PI ) );
				float sin389 = sin( ( _Float42 * PI ) );
				float2 rotator389 = mul( ( temp_output_322_0 + panner216 ) - float2( 0.5,0.5 ) , float2x2( cos389 , -sin389 , sin389 , cos389 )) + float2( 0.5,0.5 );
				float2 break623 = rotator389;
				float clampResult624 = clamp( break623.x , 0.0 , 1.0 );
				float lerpResult772 = lerp( break623.x , clampResult624 , _Float65);
				float clampResult625 = clamp( break623.y , 0.0 , 1.0 );
				float lerpResult773 = lerp( break623.y , clampResult625 , _Float83);
				float2 appendResult622 = (float2(lerpResult772 , lerpResult773));
				float4 tex2DNode218 = tex2D( _Mask1, appendResult622 );
				float lerpResult407 = lerp( tex2DNode218.a , tex2DNode218.r , _mask02_alpha);
				float clampResult2000 = clamp( ( _mask02_alpha - 1.0 ) , 0.0 , 1.0 );
				float lerpResult2001 = lerp( lerpResult407 , tex2DNode218.g , clampResult2000);
				float clampResult1997 = clamp( ( _mask02_alpha - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1996 = lerp( lerpResult2001 , tex2DNode218.b , clampResult1997);
				float Mask82 = ( lerpResult1989 * lerpResult1996 );
				float temp_output_6_0 = ( lerpResult374 * IN.ase_color.a * _Color0.a * lerpResult413 * _Float15 * lerpResult338 * Mask82 * lerpResult347 );
				float clampResult602 = clamp( temp_output_6_0 , 0.0 , 1.0 );
				float lerpResult656 = lerp( temp_output_6_0 , clampResult602 , _Float70);
				float4 texCoord1975 = IN.ase_texcoord6;
				texCoord1975.xy = IN.ase_texcoord6.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1959 = clamp( _Custom13 , 0.0 , 1.0 );
				float lerpResult1962 = lerp( texCoord1975.x , texCoord1975.y , clampResult1959);
				float clampResult1963 = clamp( ( _Custom13 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1964 = lerp( lerpResult1962 , texCoord1975.z , clampResult1963);
				float clampResult1965 = clamp( ( _Custom13 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1968 = lerp( lerpResult1964 , texCoord1975.w , clampResult1965);
				float4 texCoord1970 = IN.ase_texcoord7;
				texCoord1970.xy = IN.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1969 = clamp( ( _Custom13 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1976 = lerp( lerpResult1968 , texCoord1970.x , clampResult1969);
				float clampResult1972 = clamp( ( _Custom13 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1977 = lerp( lerpResult1976 , texCoord1970.y , clampResult1972);
				float clampResult1973 = clamp( ( _Custom13 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1978 = lerp( lerpResult1977 , texCoord1970.z , clampResult1973);
				float clampResult1974 = clamp( ( _Custom13 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1979 = lerp( lerpResult1978 , texCoord1970.w , clampResult1974);
				float custom131619 = lerpResult1979;
				float lerpResult1621 = lerp( _Float72 , custom131619 , _Float151);
				clip( dis_tex661 - lerpResult1621);
				
				float3 BakedAlbedo = 0;
				float3 BakedEmission = 0;
				float3 Color = temp_output_928_0.rgb;
				float Alpha = lerpResult656;
				float AlphaClipThreshold = 0.5;
				float AlphaClipThresholdShadow = 0.5;

				#ifdef _ALPHATEST_ON
					clip( Alpha - AlphaClipThreshold );
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif

				#ifdef ASE_FOG
					Color = MixFog( Color, IN.fogFactor );
				#endif

				return half4( Color, Alpha );
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "DepthOnly"
			Tags { "LightMode"="DepthOnly" }

			ZWrite On
			ColorMask 0
			AlphaToMask Off

			HLSLPROGRAM
			#define _RECEIVE_SHADOWS_OFF 1
			#pragma multi_compile_instancing
			#define ASE_SRP_VERSION 999999
			#define REQUIRE_DEPTH_TEXTURE 1

			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x

			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_FRAG_COLOR
			#define ASE_NEEDS_VERT_POSITION


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_tangent : TANGENT;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 worldPos : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				float4 shadowCoord : TEXCOORD1;
				#endif
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				float4 ase_texcoord6 : TEXCOORD6;
				float4 ase_texcoord7 : TEXCOORD7;
				float4 ase_texcoord8 : TEXCOORD8;
				float4 ase_color : COLOR;
				float4 ase_texcoord9 : TEXCOORD9;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Vector15;
			float4 _Vector35;
			float4 _reftex_ST;
			float4 _Color1;
			float4 _flowmap_ST;
			float4 _Vector25;
			float4 _vertextex1_ST;
			float4 _Vector34;
			float4 _normallight_ST;
			float4 _Vector10;
			float4 _Vector33;
			float4 _Color5;
			float4 _Color4;
			float4 _Vector0;
			float4 _maintex_ST;
			float4 _Color2;
			float4 _Color7;
			float4 _noise_ST;
			float4 _Vector17;
			float4 _parallax_ST;
			float4 _Vector23;
			float4 _noisemask_ST;
			float4 _Vector16;
			float4 _vertextex_ST;
			float4 _Vector32;
			float4 _Vector21;
			float4 _Color3;
			float4 _Color0;
			float4 _Vector19;
			float4 _Color6;
			float4 _Mask1_ST;
			float4 _Vector9;
			float4 _TextureSample1_ST;
			float4 _Vector11;
			float4 _dissolvetex_ST;
			float4 _Ramptex_ST;
			float4 _Vector7;
			float4 _Mask_ST;
			float4 _Vector13;
			float3 _Vector5;
			float _Custom6;
			float _Float142;
			float _Float50;
			float _Float53;
			float _Custom9;
			float _Float8;
			float _Float160;
			float _Custom5;
			float _Float79;
			float _Float41;
			float _Custom8;
			float _Float66;
			float _Float129;
			float _Float87;
			float _Float11;
			float _Custom7;
			float _maintex_alpha1;
			float _Float6;
			float _Float130;
			float _Float139;
			float _Float155;
			float _Float17;
			float _Float47;
			float _Float24;
			float _Float58;
			float _Custom3;
			float _Custom4;
			float _Float12;
			float _Float51;
			float _Float80;
			float _Float43;
			float _Float64;
			float _Float15;
			float _Float82;
			float _Float52;
			float _Float42;
			float _Float65;
			float _Float83;
			float _mask02_alpha;
			float _Float70;
			float _Float72;
			float _mask01_alpha;
			float _Float36;
			float _Float37;
			float _Float34;
			float _Float59;
			float _Float69;
			float _Float86;
			float _Float23;
			float _Custom10;
			float _Float48;
			float _Float133;
			float _Float146;
			float _Float137;
			float _Float132;
			float _Float134;
			float _Float157;
			float _Float140;
			float _Float258;
			float _Float141;
			float _Float14;
			float _maintex_alpha;
			float _Float25;
			float _Vertex_Group;
			float _Float81;
			float _Float145;
			float _Noise_Group;
			float _Ztestmode2;
			float _Depthfade_Group1;
			float _Float131;
			float _Float56;
			float _Float45;
			float _Float68;
			float _Ramp;
			float _Float89;
			float _Custom11;
			float _Float22;
			float _Float75;
			float _Float77;
			float _Float78;
			float _Float88;
			float _Float28;
			float _Float135;
			float _Float55;
			float _Float1;
			float _Float4;
			float _Maintex_Group;
			float _Ref_Group;
			float _Disolove_Group;
			float _Maintex_Group4;
			float _Ztestmode;
			float _Color_Group2;
			float _Float2;
			float _Mask_Group;
			float _Float60;
			float _Color_Group3;
			float _Ztestmode1;
			float _Float46;
			float _Maintex_Group3;
			float _Fresnel_Group2;
			float _Maintex_Group5;
			float _Custom_Group1;
			float _Maintex_Group2;
			float _Float0;
			float _Float16;
			float _Float5;
			float _Float32;
			float _Float31;
			float _Float39;
			float _Float62;
			float _Float71;
			float _Float61;
			float _Float159;
			float _Float257;
			float _Float152;
			float _Float40;
			float _Float63;
			float _Custom13;
			float _Float35;
			float _power3;
			float _Float19;
			float _Float20;
			float _Float154;
			float _Custom12;
			float _Float9;
			float _Float76;
			float _Float30;
			float _Float144;
			float _Custom1;
			float _Custom2;
			float _Float10;
			float _Float49;
			float _Float38;
			float _refplane;
			float _Float143;
			float _Float57;
			float _Float74;
			float _Float73;
			float _Float84;
			float _Float54;
			float _Float44;
			float _Float67;
			float _Float85;
			float _Float33;
			float _Float151;
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END
			sampler2D _vertextex;
			sampler2D _vertextex1;
			sampler2D _maintex;
			sampler2D _parallax;
			sampler1D _noisemask;
			sampler2D _noise;
			sampler2D _flowmap;
			sampler2D _dissolvetex;
			sampler2D _TextureSample1;
			uniform float4 _CameraDepthTexture_TexelSize;
			sampler2D _Mask;
			sampler2D _Mask1;


			inline float2 POM( sampler2D heightMap, float2 uvs, float2 dx, float2 dy, float3 normalWorld, float3 viewWorld, float3 viewDirTan, int minSamples, int maxSamples, float parallax, float refPlane, float2 tilling, float2 curv, int index )
			{
				float3 result = 0;
				int stepIndex = 0;
				int numSteps = ( int )lerp( (float)maxSamples, (float)minSamples, saturate( dot( normalWorld, viewWorld ) ) );
				float layerHeight = 1.0 / numSteps;
				float2 plane = parallax * ( viewDirTan.xy / viewDirTan.z );
				uvs.xy += refPlane * plane;
				float2 deltaTex = -plane * layerHeight;
				float2 prevTexOffset = 0;
				float prevRayZ = 1.0f;
				float prevHeight = 0.0f;
				float2 currTexOffset = deltaTex;
				float currRayZ = 1.0f - layerHeight;
				float currHeight = 0.0f;
				float intersection = 0;
				float2 finalTexOffset = 0;
				while ( stepIndex < numSteps + 1 )
				{
				 	currHeight = tex2Dgrad( heightMap, uvs + currTexOffset, dx, dy ).r;
				 	if ( currHeight > currRayZ )
				 	{
				 	 	stepIndex = numSteps + 1;
				 	}
				 	else
				 	{
				 	 	stepIndex++;
				 	 	prevTexOffset = currTexOffset;
				 	 	prevRayZ = currRayZ;
				 	 	prevHeight = currHeight;
				 	 	currTexOffset += deltaTex;
				 	 	currRayZ -= layerHeight;
				 	}
				}
				int sectionSteps = 10;
				int sectionIndex = 0;
				float newZ = 0;
				float newHeight = 0;
				while ( sectionIndex < sectionSteps )
				{
				 	intersection = ( prevHeight - prevRayZ ) / ( prevHeight - currHeight + currRayZ - prevRayZ );
				 	finalTexOffset = prevTexOffset + intersection * deltaTex;
				 	newZ = prevRayZ - intersection * layerHeight;
				 	newHeight = tex2Dgrad( heightMap, uvs + finalTexOffset, dx, dy ).r;
				 	if ( newHeight > newZ )
				 	{
				 	 	currTexOffset = finalTexOffset;
				 	 	currHeight = newHeight;
				 	 	currRayZ = newZ;
				 	 	deltaTex = intersection * deltaTex;
				 	 	layerHeight = intersection * layerHeight;
				 	}
				 	else
				 	{
				 	 	prevTexOffset = finalTexOffset;
				 	 	prevHeight = newHeight;
				 	 	prevRayZ = newZ;
				 	 	deltaTex = ( 1 - intersection ) * deltaTex;
				 	 	layerHeight = ( 1 - intersection ) * layerHeight;
				 	}
				 	sectionIndex++;
				}
				return uvs.xy + finalTexOffset;
			}
			

			VertexOutput VertexFunction( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float2 appendResult557 = (float2(_Vector19.x , _Vector19.y));
				float2 uv_vertextex = v.ase_texcoord.xy * _vertextex_ST.xy + _vertextex_ST.zw;
				float2 uv2_vertextex = v.ase_texcoord1 * _vertextex_ST.xy + _vertextex_ST.zw;
				float uv2930 = _Float131;
				float2 lerpResult949 = lerp( uv_vertextex , uv2_vertextex , uv2930);
				float3 appendResult895 = (float3(1.0 , _Vector19.z , 0.0));
				float3 appendResult897 = (float3(_Vector19.w , 1.0 , 0.0));
				float2 temp_output_898_0 = mul( float3( lerpResult949 ,  0.0 ), float3x3(appendResult895, appendResult897, float3(0,0,1)) ).xy;
				float2 appendResult546 = (float2(_vertextex_ST.z , _vertextex_ST.w));
				float2 CenteredUV15_g166 = ( v.ase_texcoord.xy - float2( 0.5,0.5 ) );
				float2 break17_g166 = CenteredUV15_g166;
				float2 appendResult23_g166 = (float2(( length( CenteredUV15_g166 ) * _vertextex_ST.x * 2.0 ) , ( atan2( break17_g166.x , break17_g166.y ) * ( 1.0 / TWO_PI ) * _vertextex_ST.y )));
				float2 lerpResult556 = lerp( ( temp_output_898_0 + appendResult546 ) , (( appendResult23_g166 * temp_output_898_0 )*float2( 1,1 ) + appendResult546) , _Float56);
				float2 panner168 = ( 1.0 * _Time.y * appendResult557 + lerpResult556);
				float cos398 = cos( ( _Float45 * PI ) );
				float sin398 = sin( ( _Float45 * PI ) );
				float2 rotator398 = mul( panner168 - float2( 0.5,0.5 ) , float2x2( cos398 , -sin398 , sin398 , cos398 )) + float2( 0.5,0.5 );
				float2 break644 = rotator398;
				float clampResult646 = clamp( break644.x , 0.0 , 1.0 );
				float lerpResult790 = lerp( break644.x , clampResult646 , _Float68);
				float clampResult645 = clamp( break644.y , 0.0 , 1.0 );
				float lerpResult791 = lerp( break644.y , clampResult645 , _Float89);
				float2 appendResult647 = (float2(lerpResult790 , lerpResult791));
				float3 temp_cast_4 = (1.0).xxx;
				float3 lerpResult993 = lerp( temp_cast_4 , v.ase_normal , _Float135);
				float4 texCoord1915 = v.ase_texcoord1;
				texCoord1915.xy = v.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1914 = clamp( _Custom11 , 0.0 , 1.0 );
				float lerpResult1918 = lerp( texCoord1915.x , texCoord1915.y , clampResult1914);
				float clampResult1919 = clamp( ( _Custom11 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1920 = lerp( lerpResult1918 , texCoord1915.z , clampResult1919);
				float clampResult1921 = clamp( ( _Custom11 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1924 = lerp( lerpResult1920 , texCoord1915.w , clampResult1921);
				float4 texCoord1926 = v.ase_texcoord2;
				texCoord1926.xy = v.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1925 = clamp( ( _Custom11 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1929 = lerp( lerpResult1924 , texCoord1926.x , clampResult1925);
				float clampResult1928 = clamp( ( _Custom11 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1930 = lerp( lerpResult1929 , texCoord1926.y , clampResult1928);
				float clampResult1931 = clamp( ( _Custom11 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1934 = lerp( lerpResult1930 , texCoord1926.z , clampResult1931);
				float clampResult1933 = clamp( ( _Custom11 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1935 = lerp( lerpResult1934 , texCoord1926.w , clampResult1933);
				float custom111553 = lerpResult1935;
				float lerpResult176 = lerp( 1.0 , custom111553 , _Float22);
				float2 appendResult719 = (float2(_Vector25.x , _Vector25.y));
				float2 uv_vertextex1 = v.ase_texcoord.xy * _vertextex1_ST.xy + _vertextex1_ST.zw;
				float2 uv2_vertextex1 = v.ase_texcoord1.xy * _vertextex1_ST.xy + _vertextex1_ST.zw;
				float2 lerpResult946 = lerp( uv_vertextex1 , uv2_vertextex1 , uv2930);
				float3 appendResult886 = (float3(1.0 , _Vector25.z , 0.0));
				float3 appendResult888 = (float3(_Vector25.w , 1.0 , 0.0));
				float2 appendResult710 = (float2(_vertextex1_ST.z , _vertextex1_ST.w));
				float2 CenteredUV15_g167 = ( v.ase_texcoord.xy - float2( 0.5,0.5 ) );
				float2 break17_g167 = CenteredUV15_g167;
				float2 appendResult23_g167 = (float2(( length( CenteredUV15_g167 ) * _vertextex1_ST.x * 2.0 ) , ( atan2( break17_g167.x , break17_g167.y ) * ( 1.0 / TWO_PI ) * _vertextex1_ST.y )));
				float2 lerpResult728 = lerp( ( mul( float3( lerpResult946 ,  0.0 ), float3x3(appendResult886, appendResult888, float3(0,0,1)) ).xy + appendResult710 ) , (mul( float3( appendResult23_g167 ,  0.0 ), float3x3(appendResult886, appendResult888, float3(0,0,1)) ).xy*float2( 1,1 ) + appendResult710) , _Float75);
				float2 panner725 = ( 1.0 * _Time.y * appendResult719 + lerpResult728);
				float cos726 = cos( ( _Float77 * PI ) );
				float sin726 = sin( ( _Float77 * PI ) );
				float2 rotator726 = mul( panner725 - float2( 0.5,0.5 ) , float2x2( cos726 , -sin726 , sin726 , cos726 )) + float2( 0.5,0.5 );
				float2 break720 = rotator726;
				float clampResult721 = clamp( break720.x , 0.0 , 1.0 );
				float lerpResult787 = lerp( break720.x , clampResult721 , _Float78);
				float clampResult722 = clamp( break720.y , 0.0 , 1.0 );
				float lerpResult789 = lerp( break720.y , clampResult722 , _Float88);
				float2 appendResult723 = (float2(lerpResult787 , lerpResult789));
				float3 vertexoffset181 = ( (_Vector32.z + (tex2Dlod( _vertextex, float4( appendResult647, 0, 0.0) ).r - _Vector32.x) * (_Vector32.w - _Vector32.z) / (_Vector32.y - _Vector32.x)) * lerpResult993 * _Vector5 * lerpResult176 * tex2Dlod( _vertextex1, float4( appendResult723, 0, 0.0) ).r );
				
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord3 = screenPos;
				float3 ase_worldTangent = TransformObjectToWorldDir(v.ase_tangent.xyz);
				o.ase_texcoord6.xyz = ase_worldTangent;
				float3 ase_worldNormal = TransformObjectToWorldNormal(v.ase_normal);
				o.ase_texcoord7.xyz = ase_worldNormal;
				float ase_vertexTangentSign = v.ase_tangent.w * unity_WorldTransformParams.w;
				float3 ase_worldBitangent = cross( ase_worldNormal, ase_worldTangent ) * ase_vertexTangentSign;
				o.ase_texcoord8.xyz = ase_worldBitangent;
				float3 customSurfaceDepth754 = v.vertex.xyz;
				float customEye754 = -TransformWorldToView(TransformObjectToWorld(customSurfaceDepth754)).z;
				o.ase_texcoord2.z = customEye754;
				float3 vertexPos97 = v.vertex.xyz;
				float4 ase_clipPos97 = TransformObjectToHClip((vertexPos97).xyz);
				float4 screenPos97 = ComputeScreenPos(ase_clipPos97);
				o.ase_texcoord9 = screenPos97;
				
				o.ase_texcoord2.xy = v.ase_texcoord.xy;
				o.ase_texcoord4 = v.ase_texcoord1;
				o.ase_texcoord5 = v.ase_texcoord2;
				o.ase_color = v.ase_color;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord2.w = 0;
				o.ase_texcoord6.w = 0;
				o.ase_texcoord7.w = 0;
				o.ase_texcoord8.w = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = vertexoffset181;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif

				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				o.worldPos = positionWS;
				#endif

				o.clipPos = TransformWorldToHClip( positionWS );
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					VertexPositionInputs vertexInput = (VertexPositionInputs)0;
					vertexInput.positionWS = positionWS;
					vertexInput.positionCS = clipPos;
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_tangent : TANGENT;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_texcoord = v.ase_texcoord;
				o.ase_texcoord1 = v.ase_texcoord1;
				o.ase_texcoord2 = v.ase_texcoord2;
				o.ase_tangent = v.ase_tangent;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				o.ase_texcoord1 = patch[0].ase_texcoord1 * bary.x + patch[1].ase_texcoord1 * bary.y + patch[2].ase_texcoord1 * bary.z;
				o.ase_texcoord2 = patch[0].ase_texcoord2 * bary.x + patch[1].ase_texcoord2 * bary.y + patch[2].ase_texcoord2 * bary.z;
				o.ase_tangent = patch[0].ase_tangent * bary.x + patch[1].ase_tangent * bary.y + patch[2].ase_tangent * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag(VertexOutput IN , half ase_vface : VFACE ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 WorldPosition = IN.worldPos;
				#endif
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float2 appendResult440 = (float2(_Vector0.x , _Vector0.y));
				float2 uv_maintex = IN.ase_texcoord2.xy * _maintex_ST.xy + _maintex_ST.zw;
				float4 screenPos = IN.ase_texcoord3;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float2 appendResult1679 = (float2(ase_screenPosNorm.x , ase_screenPosNorm.y));
				float2 appendResult1131 = (float2(_maintex_ST.x , _maintex_ST.y));
				float2 appendResult1130 = (float2(_maintex_ST.z , _maintex_ST.w));
				float screenuv1625 = _Float144;
				float2 lerpResult1127 = lerp( uv_maintex , (appendResult1679*appendResult1131 + appendResult1130) , screenuv1625);
				float3 appendResult799 = (float3(1.0 , _Vector0.z , 0.0));
				float3 appendResult803 = (float3(_Vector0.w , 1.0 , 0.0));
				float2 appendResult433 = (float2(_maintex_ST.z , _maintex_ST.w));
				float4 texCoord1281 = IN.ase_texcoord4;
				texCoord1281.xy = IN.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1694 = clamp( _Custom1 , 0.0 , 1.0 );
				float lerpResult1681 = lerp( texCoord1281.x , texCoord1281.y , clampResult1694);
				float clampResult1692 = clamp( ( _Custom1 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1683 = lerp( lerpResult1681 , texCoord1281.z , clampResult1692);
				float clampResult1693 = clamp( ( _Custom1 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1689 = lerp( lerpResult1683 , texCoord1281.w , clampResult1693);
				float4 texCoord1698 = IN.ase_texcoord5;
				texCoord1698.xy = IN.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1695 = clamp( ( _Custom1 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1697 = lerp( lerpResult1689 , texCoord1698.x , clampResult1695);
				float clampResult1699 = clamp( ( _Custom1 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1700 = lerp( lerpResult1697 , texCoord1698.y , clampResult1699);
				float clampResult1702 = clamp( ( _Custom1 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1704 = lerp( lerpResult1700 , texCoord1698.z , clampResult1702);
				float clampResult1705 = clamp( ( _Custom1 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1706 = lerp( lerpResult1704 , texCoord1698.w , clampResult1705);
				float custom11336 = lerpResult1706;
				float4 texCoord1728 = IN.ase_texcoord4;
				texCoord1728.xy = IN.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1729 = clamp( _Custom2 , 0.0 , 1.0 );
				float lerpResult1710 = lerp( texCoord1728.x , texCoord1728.y , clampResult1729);
				float clampResult1709 = clamp( ( _Custom2 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1712 = lerp( lerpResult1710 , texCoord1728.z , clampResult1709);
				float clampResult1713 = clamp( ( _Custom2 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1717 = lerp( lerpResult1712 , texCoord1728.w , clampResult1713);
				float4 texCoord1718 = IN.ase_texcoord5;
				texCoord1718.xy = IN.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1715 = clamp( ( _Custom2 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1719 = lerp( lerpResult1717 , texCoord1718.x , clampResult1715);
				float clampResult1720 = clamp( ( _Custom2 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1722 = lerp( lerpResult1719 , texCoord1718.y , clampResult1720);
				float clampResult1723 = clamp( ( _Custom2 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1726 = lerp( lerpResult1722 , texCoord1718.z , clampResult1723);
				float clampResult1725 = clamp( ( _Custom2 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1727 = lerp( lerpResult1726 , texCoord1718.w , clampResult1725);
				float custom21337 = lerpResult1727;
				float2 appendResult42 = (float2(custom11336 , custom21337));
				float2 lerpResult59 = lerp( appendResult433 , appendResult42 , _Float10);
				float2 CenteredUV15_g104 = ( IN.ase_texcoord2.xy - float2( 0.5,0.5 ) );
				float2 break17_g104 = CenteredUV15_g104;
				float2 appendResult23_g104 = (float2(( length( CenteredUV15_g104 ) * _maintex_ST.x * 2.0 ) , ( atan2( break17_g104.x , break17_g104.y ) * ( 1.0 / TWO_PI ) * _maintex_ST.y )));
				float2 lerpResult449 = lerp( appendResult433 , appendResult42 , _Float10);
				float2 lerpResult444 = lerp( ( mul( float3( lerpResult1127 ,  0.0 ), float3x3(appendResult799, appendResult803, float3(0,0,1)) ).xy + lerpResult59 ) , (mul( float3( appendResult23_g104 ,  0.0 ), float3x3(appendResult799, appendResult803, float3(0,0,1)) ).xy*float2( 1,1 ) + lerpResult449) , _Float49);
				float2 panner36 = ( 1.0 * _Time.y * appendResult440 + lerpResult444);
				float2 maintexUV_00161 = panner36;
				float3 ase_worldTangent = IN.ase_texcoord6.xyz;
				float3 ase_worldNormal = IN.ase_texcoord7.xyz;
				float3 ase_worldBitangent = IN.ase_texcoord8.xyz;
				float3 tanToWorld0 = float3( ase_worldTangent.x, ase_worldBitangent.x, ase_worldNormal.x );
				float3 tanToWorld1 = float3( ase_worldTangent.y, ase_worldBitangent.y, ase_worldNormal.y );
				float3 tanToWorld2 = float3( ase_worldTangent.z, ase_worldBitangent.z, ase_worldNormal.z );
				float3 ase_worldViewDir = ( _WorldSpaceCameraPos.xyz - WorldPosition );
				ase_worldViewDir = normalize(ase_worldViewDir);
				float3 ase_tanViewDir =  tanToWorld0 * ase_worldViewDir.x + tanToWorld1 * ase_worldViewDir.y  + tanToWorld2 * ase_worldViewDir.z;
				ase_tanViewDir = normalize(ase_tanViewDir);
				float2 OffsetPOM1092 = POM( _parallax, maintexUV_00161, ddx(maintexUV_00161), ddy(maintexUV_00161), ase_worldNormal, ase_worldViewDir, ase_tanViewDir, 128, 128, ( _Float38 * 0.1 ), _refplane, _parallax_ST.xy, float2(0,0), 0 );
				float2 parallax1097 = OffsetPOM1092;
				float2 lerpResult1099 = lerp( maintexUV_00161 , parallax1097 , _Float143);
				float2 appendResult687 = (float2(_Vector23.x , _Vector23.y));
				float2 uv_noisemask = IN.ase_texcoord2.xy * _noisemask_ST.xy + _noisemask_ST.zw;
				float2 uv2_noisemask = IN.ase_texcoord4.xy * _noisemask_ST.xy + _noisemask_ST.zw;
				float uv2930 = _Float131;
				float2 lerpResult938 = lerp( uv_noisemask , uv2_noisemask , uv2930);
				float2 appendResult1629 = (float2(ase_screenPosNorm.x , ase_screenPosNorm.y));
				float2 appendResult1628 = (float2(_noisemask_ST.x , _noisemask_ST.y));
				float2 appendResult1633 = (float2(_noisemask_ST.z , _noisemask_ST.w));
				float2 lerpResult1631 = lerp( lerpResult938 , (appendResult1629*appendResult1628 + appendResult1633) , screenuv1625);
				float3 appendResult866 = (float3(1.0 , _Vector23.z , 0.0));
				float3 appendResult865 = (float3(_Vector23.w , 1.0 , 0.0));
				float2 appendResult679 = (float2(_noisemask_ST.z , _noisemask_ST.w));
				float2 CenteredUV15_g47 = ( IN.ase_texcoord2.xy - float2( 0.5,0.5 ) );
				float2 break17_g47 = CenteredUV15_g47;
				float2 appendResult23_g47 = (float2(( length( CenteredUV15_g47 ) * _noisemask_ST.x * 2.0 ) , ( atan2( break17_g47.x , break17_g47.y ) * ( 1.0 / TWO_PI ) * _noisemask_ST.y )));
				float2 lerpResult688 = lerp( ( mul( float3( lerpResult1631 ,  0.0 ), float3x3(appendResult866, appendResult865, float3(0,0,1)) ).xy + appendResult679 ) , (mul( float3( appendResult23_g47 ,  0.0 ), float3x3(appendResult866, appendResult865, float3(0,0,1)) ).xy*float2( 1,1 ) + appendResult679) , _Float57);
				float2 panner697 = ( 1.0 * _Time.y * appendResult687 + lerpResult688);
				float cos698 = cos( ( _Float74 * PI ) );
				float sin698 = sin( ( _Float74 * PI ) );
				float2 rotator698 = mul( panner697 - float2( 0.5,0.5 ) , float2x2( cos698 , -sin698 , sin698 , cos698 )) + float2( 0.5,0.5 );
				float2 break689 = rotator698;
				float clampResult690 = clamp( break689.x , 0.0 , 1.0 );
				float lerpResult775 = lerp( break689.x , clampResult690 , _Float73);
				float clampResult691 = clamp( break689.y , 0.0 , 1.0 );
				float lerpResult776 = lerp( break689.x , clampResult691 , _Float84);
				float2 appendResult693 = (float2(lerpResult775 , lerpResult776));
				float4 tex1DNode564 = tex1D( _noisemask, appendResult693.x );
				float2 appendResult530 = (float2(_Vector17.x , _Vector17.y));
				float2 uv_noise = IN.ase_texcoord2.xy * _noise_ST.xy + _noise_ST.zw;
				float2 uv2_noise = IN.ase_texcoord4.xy * _noise_ST.xy + _noise_ST.zw;
				float2 lerpResult941 = lerp( uv_noise , uv2_noise , uv2930);
				float2 appendResult1637 = (float2(ase_screenPosNorm.x , ase_screenPosNorm.y));
				float2 appendResult1636 = (float2(_noise_ST.x , _noise_ST.y));
				float2 appendResult1641 = (float2(_noise_ST.z , _noise_ST.w));
				float2 lerpResult1639 = lerp( lerpResult941 , (appendResult1637*appendResult1636 + appendResult1641) , screenuv1625);
				float3 appendResult876 = (float3(1.0 , _Vector17.z , 0.0));
				float3 appendResult878 = (float3(_Vector17.w , 1.0 , 0.0));
				float2 appendResult531 = (float2(_noise_ST.z , _noise_ST.w));
				float2 CenteredUV15_g46 = ( IN.ase_texcoord2.xy - float2( 0.5,0.5 ) );
				float2 break17_g46 = CenteredUV15_g46;
				float2 appendResult23_g46 = (float2(( length( CenteredUV15_g46 ) * _noise_ST.x * 2.0 ) , ( atan2( break17_g46.x , break17_g46.y ) * ( 1.0 / TWO_PI ) * _noise_ST.y )));
				float2 lerpResult539 = lerp( ( mul( float3( lerpResult1639 ,  0.0 ), float3x3(appendResult876, appendResult878, float3(0,0,1)) ).xy + appendResult531 ) , (mul( float3( appendResult23_g46 ,  0.0 ), float3x3(appendResult876, appendResult878, float3(0,0,1)) ).xy*float2( 1,1 ) + appendResult531) , _Float54);
				float2 panner53 = ( 1.0 * _Time.y * appendResult530 + lerpResult539);
				float cos395 = cos( ( _Float44 * PI ) );
				float sin395 = sin( ( _Float44 * PI ) );
				float2 rotator395 = mul( panner53 - float2( 0.5,0.5 ) , float2x2( cos395 , -sin395 , sin395 , cos395 )) + float2( 0.5,0.5 );
				float2 break638 = rotator395;
				float clampResult640 = clamp( break638.x , 0.0 , 1.0 );
				float lerpResult778 = lerp( break638.x , clampResult640 , _Float67);
				float clampResult639 = clamp( break638.y , 0.0 , 1.0 );
				float lerpResult780 = lerp( break638.y , clampResult639 , _Float85);
				float2 appendResult641 = (float2(lerpResult778 , lerpResult780));
				float temp_output_923_0 = (_Vector33.z + (tex2D( _noise, appendResult641 ).r - _Vector33.x) * (_Vector33.w - _Vector33.z) / (_Vector33.y - _Vector33.x));
				float lerpResult701 = lerp( ( tex1DNode564.r * temp_output_923_0 ) , ( tex1DNode564.r + temp_output_923_0 ) , _Float76);
				float noise70 = lerpResult701;
				float4 texCoord1952 = IN.ase_texcoord4;
				texCoord1952.xy = IN.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1936 = clamp( _Custom12 , 0.0 , 1.0 );
				float lerpResult1939 = lerp( texCoord1952.x , texCoord1952.y , clampResult1936);
				float clampResult1940 = clamp( ( _Custom12 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1941 = lerp( lerpResult1939 , texCoord1952.z , clampResult1940);
				float clampResult1942 = clamp( ( _Custom12 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1945 = lerp( lerpResult1941 , texCoord1952.w , clampResult1942);
				float4 texCoord1947 = IN.ase_texcoord5;
				texCoord1947.xy = IN.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1946 = clamp( ( _Custom12 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1953 = lerp( lerpResult1945 , texCoord1947.x , clampResult1946);
				float clampResult1949 = clamp( ( _Custom12 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1954 = lerp( lerpResult1953 , texCoord1947.y , clampResult1949);
				float clampResult1950 = clamp( ( _Custom12 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1955 = lerp( lerpResult1954 , texCoord1947.z , clampResult1950);
				float clampResult1951 = clamp( ( _Custom12 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1956 = lerp( lerpResult1955 , texCoord1947.w , clampResult1951);
				float custom121574 = lerpResult1956;
				float lerpResult1575 = lerp( 1.0 , custom121574 , _Float257);
				float noise_intensity_main67 = ( _Float9 * 0.1 * lerpResult1575 );
				float2 uv_flowmap = IN.ase_texcoord2.xy * _flowmap_ST.xy + _flowmap_ST.zw;
				float4 tex2DNode1982 = tex2D( _flowmap, uv_flowmap );
				float2 appendResult242 = (float2(tex2DNode1982.r , tex2DNode1982.g));
				float2 flowmap285 = appendResult242;
				float flowmap_intensity311 = _Float32;
				float4 texCoord100 = IN.ase_texcoord5;
				texCoord100.xy = IN.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float flpwmap_custom_switch316 = _Float31;
				float lerpResult99 = lerp( flowmap_intensity311 , texCoord100.y , flpwmap_custom_switch316);
				float2 lerpResult283 = lerp( ( lerpResult1099 + ( noise70 * noise_intensity_main67 ) ) , flowmap285 , lerpResult99);
				float cos377 = cos( ( _Float39 * PI ) );
				float sin377 = sin( ( _Float39 * PI ) );
				float2 rotator377 = mul( lerpResult283 - float2( 0.5,0.5 ) , float2x2( cos377 , -sin377 , sin377 , cos377 )) + float2( 0.5,0.5 );
				float2 break603 = rotator377;
				float clampResult604 = clamp( break603.x , 0.0 , 1.0 );
				float lerpResult607 = lerp( break603.x , clampResult604 , _Float62);
				float clampResult605 = clamp( break603.y , 0.0 , 1.0 );
				float lerpResult764 = lerp( break603.y , clampResult605 , _Float71);
				float2 appendResult606 = (float2(lerpResult607 , lerpResult764));
				float4 tex2DNode1 = tex2D( _maintex, appendResult606 );
				float lerpResult402 = lerp( tex2DNode1.a , tex2DNode1.r , _maintex_alpha);
				float clampResult1985 = clamp( ( _maintex_alpha - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1983 = lerp( lerpResult402 , tex2DNode1.g , clampResult1985);
				float clampResult1987 = clamp( ( _maintex_alpha - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1988 = lerp( lerpResult1983 , tex2DNode1.b , clampResult1987);
				float lerpResult374 = lerp( lerpResult1988 , ( pow( abs( lerpResult1988 ) , _Float34 ) * _Float37 ) , _Float36);
				float4 texCoord1869 = IN.ase_texcoord4;
				texCoord1869.xy = IN.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1868 = clamp( _Custom9 , 0.0 , 1.0 );
				float lerpResult1872 = lerp( texCoord1869.x , texCoord1869.y , clampResult1868);
				float clampResult1873 = clamp( ( _Custom9 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1876 = lerp( lerpResult1872 , texCoord1869.z , clampResult1873);
				float clampResult1874 = clamp( ( _Custom9 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1878 = lerp( lerpResult1876 , texCoord1869.w , clampResult1874);
				float4 texCoord1879 = IN.ase_texcoord5;
				texCoord1879.xy = IN.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1880 = clamp( ( _Custom9 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1883 = lerp( lerpResult1878 , texCoord1879.x , clampResult1880);
				float clampResult1881 = clamp( ( _Custom9 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1884 = lerp( lerpResult1883 , texCoord1879.y , clampResult1881);
				float clampResult1886 = clamp( ( _Custom9 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1888 = lerp( lerpResult1884 , texCoord1879.z , clampResult1886);
				float clampResult1887 = clamp( ( _Custom9 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1889 = lerp( lerpResult1888 , texCoord1879.w , clampResult1887);
				float custom91511 = lerpResult1889;
				float lerpResult1090 = lerp( _Float8 , custom91511 , _Float142);
				float2 appendResult501 = (float2(_Vector15.x , _Vector15.y));
				float2 uv_dissolvetex = IN.ase_texcoord2.xy * _dissolvetex_ST.xy + _dissolvetex_ST.zw;
				float2 uv2_dissolvetex = IN.ase_texcoord4.xy * _dissolvetex_ST.xy + _dissolvetex_ST.zw;
				float2 lerpResult952 = lerp( uv_dissolvetex , uv2_dissolvetex , uv2930);
				float2 appendResult1644 = (float2(ase_screenPosNorm.x , ase_screenPosNorm.y));
				float2 appendResult1643 = (float2(_dissolvetex_ST.x , _dissolvetex_ST.y));
				float2 appendResult1648 = (float2(_dissolvetex_ST.z , _dissolvetex_ST.w));
				float2 lerpResult1646 = lerp( lerpResult952 , (appendResult1644*appendResult1643 + appendResult1648) , screenuv1625);
				float3 appendResult810 = (float3(1.0 , _Vector15.z , 0.0));
				float3 appendResult811 = (float3(_Vector15.w , 1.0 , 0.0));
				float2 appendResult502 = (float2(_dissolvetex_ST.z , _dissolvetex_ST.w));
				float4 texCoord1779 = IN.ase_texcoord4;
				texCoord1779.xy = IN.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1777 = clamp( _Custom5 , 0.0 , 1.0 );
				float lerpResult1781 = lerp( texCoord1779.x , texCoord1779.y , clampResult1777);
				float clampResult1782 = clamp( ( _Custom5 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1785 = lerp( lerpResult1781 , texCoord1779.z , clampResult1782);
				float clampResult1783 = clamp( ( _Custom5 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1788 = lerp( lerpResult1785 , texCoord1779.w , clampResult1783);
				float4 texCoord1789 = IN.ase_texcoord5;
				texCoord1789.xy = IN.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1787 = clamp( ( _Custom5 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1791 = lerp( lerpResult1788 , texCoord1789.x , clampResult1787);
				float clampResult1792 = clamp( ( _Custom5 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1793 = lerp( lerpResult1791 , texCoord1789.y , clampResult1792);
				float clampResult1795 = clamp( ( _Custom5 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1796 = lerp( lerpResult1793 , texCoord1789.z , clampResult1795);
				float clampResult1797 = clamp( ( _Custom5 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1798 = lerp( lerpResult1796 , texCoord1789.w , clampResult1797);
				float custom51401 = lerpResult1798;
				float4 texCoord1802 = IN.ase_texcoord4;
				texCoord1802.xy = IN.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1800 = clamp( _Custom6 , 0.0 , 1.0 );
				float lerpResult1804 = lerp( texCoord1802.x , texCoord1802.y , clampResult1800);
				float clampResult1805 = clamp( ( _Custom6 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1808 = lerp( lerpResult1804 , texCoord1802.z , clampResult1805);
				float clampResult1806 = clamp( ( _Custom6 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1811 = lerp( lerpResult1808 , texCoord1802.w , clampResult1806);
				float4 texCoord1812 = IN.ase_texcoord5;
				texCoord1812.xy = IN.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1810 = clamp( ( _Custom6 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1814 = lerp( lerpResult1811 , texCoord1812.x , clampResult1810);
				float clampResult1815 = clamp( ( _Custom6 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1816 = lerp( lerpResult1814 , texCoord1812.y , clampResult1815);
				float clampResult1818 = clamp( ( _Custom6 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1819 = lerp( lerpResult1816 , texCoord1812.z , clampResult1818);
				float clampResult1820 = clamp( ( _Custom6 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1821 = lerp( lerpResult1819 , texCoord1812.w , clampResult1820);
				float custom61420 = lerpResult1821;
				float2 appendResult1425 = (float2(custom51401 , custom61420));
				float2 lerpResult1426 = lerp( appendResult502 , appendResult1425 , _Float50);
				float2 CenteredUV15_g105 = ( IN.ase_texcoord2.xy - float2( 0.5,0.5 ) );
				float2 break17_g105 = CenteredUV15_g105;
				float2 appendResult23_g105 = (float2(( length( CenteredUV15_g105 ) * _dissolvetex_ST.x * 2.0 ) , ( atan2( break17_g105.x , break17_g105.y ) * ( 1.0 / TWO_PI ) * _dissolvetex_ST.y )));
				float2 lerpResult1422 = lerp( appendResult502 , appendResult1425 , _Float50);
				float2 lerpResult511 = lerp( ( mul( float3( lerpResult1646 ,  0.0 ), float3x3(appendResult810, appendResult811, float3(0,0,1)) ).xy + lerpResult1426 ) , (mul( float3( appendResult23_g105 ,  0.0 ), float3x3(appendResult810, appendResult811, float3(0,0,1)) ).xy*float2( 1,1 ) + lerpResult1422) , _Float53);
				float2 panner58 = ( 1.0 * _Time.y * appendResult501 + lerpResult511);
				float noise_intensity_dis733 = ( _Float79 * 0.1 * lerpResult1575 );
				float4 texCoord1846 = IN.ase_texcoord4;
				texCoord1846.xy = IN.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1847 = clamp( _Custom8 , 0.0 , 1.0 );
				float lerpResult1849 = lerp( texCoord1846.x , texCoord1846.y , clampResult1847);
				float clampResult1848 = clamp( ( _Custom8 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1853 = lerp( lerpResult1849 , texCoord1846.z , clampResult1848);
				float clampResult1851 = clamp( ( _Custom8 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1855 = lerp( lerpResult1853 , texCoord1846.w , clampResult1851);
				float4 texCoord1856 = IN.ase_texcoord5;
				texCoord1856.xy = IN.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1857 = clamp( ( _Custom8 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1858 = lerp( lerpResult1855 , texCoord1856.x , clampResult1857);
				float clampResult1859 = clamp( ( _Custom8 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1863 = lerp( lerpResult1858 , texCoord1856.y , clampResult1859);
				float clampResult1861 = clamp( ( _Custom8 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1864 = lerp( lerpResult1863 , texCoord1856.z , clampResult1861);
				float clampResult1865 = clamp( ( _Custom8 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1866 = lerp( lerpResult1864 , texCoord1856.w , clampResult1865);
				float custom81489 = lerpResult1866;
				float lerpResult307 = lerp( flowmap_intensity311 , custom81489 , flpwmap_custom_switch316);
				float2 lerpResult309 = lerp( ( panner58 + ( noise70 * noise_intensity_dis733 ) ) , flowmap285 , lerpResult307);
				float2 dissolveUV92 = lerpResult309;
				float cos386 = cos( ( _Float41 * PI ) );
				float sin386 = sin( ( _Float41 * PI ) );
				float2 rotator386 = mul( dissolveUV92 - float2( 0.5,0.5 ) , float2x2( cos386 , -sin386 , sin386 , cos386 )) + float2( 0.5,0.5 );
				float2 break635 = rotator386;
				float clampResult632 = clamp( break635.x , 0.0 , 1.0 );
				float lerpResult784 = lerp( break635.x , clampResult632 , _Float66);
				float clampResult633 = clamp( break635.y , 0.0 , 1.0 );
				float lerpResult786 = lerp( break635.y , clampResult633 , _Float87);
				float2 appendResult631 = (float2(lerpResult784 , lerpResult786));
				float4 tex2DNode23 = tex2D( _dissolvetex, appendResult631 );
				float lerpResult1623 = lerp( tex2DNode23.a , tex2DNode23.r , _maintex_alpha1);
				float clampResult2004 = clamp( ( _maintex_alpha1 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult2006 = lerp( lerpResult1623 , tex2DNode23.g , clampResult2004);
				float clampResult2005 = clamp( ( _maintex_alpha1 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult2007 = lerp( lerpResult2006 , tex2DNode23.b , clampResult2005);
				float2 uv_TextureSample1 = IN.ase_texcoord2.xy * _TextureSample1_ST.xy + _TextureSample1_ST.zw;
				float2 uv2_TextureSample1 = IN.ase_texcoord4.xy * _TextureSample1_ST.xy + _TextureSample1_ST.zw;
				float2 lerpResult955 = lerp( uv_TextureSample1 , uv2_TextureSample1 , uv2930);
				float2 CenteredUV15_g165 = ( IN.ase_texcoord2.xy - float2( 0.5,0.5 ) );
				float2 break17_g165 = CenteredUV15_g165;
				float2 appendResult23_g165 = (float2(( length( CenteredUV15_g165 ) * 1.0 * 2.0 ) , ( atan2( break17_g165.x , break17_g165.y ) * ( 1.0 / TWO_PI ) * 1.0 )));
				float2 lerpResult568 = lerp( lerpResult955 , appendResult23_g165 , _Float53);
				float cos417 = cos( ( _Float47 * PI ) );
				float sin417 = sin( ( _Float47 * PI ) );
				float2 rotator417 = mul( lerpResult568 - float2( 0.5,0.5 ) , float2x2( cos417 , -sin417 , sin417 , cos417 )) + float2( 0.5,0.5 );
				float3 objToWorld1197 = mul( GetObjectToWorldMatrix(), float4( float3( 0,0,0 ), 1 ) ).xyz;
				float3 appendResult1017 = (float3(_Vector35.x , _Vector35.y , _Vector35.z));
				float3 lerpResult1199 = lerp( ( objToWorld1197 + appendResult1017 ) , appendResult1017 , _Float155);
				float lerpResult1057 = lerp( tex2D( _TextureSample1, rotator417 ).r , distance( lerpResult1199 , WorldPosition ) , _Float139);
				float dis_direction277 = lerpResult1057;
				float lerpResult1060 = lerp( _Float130 , ( 1.0 - _Float130 ) , _Float139);
				float lerpResult916 = lerp( lerpResult2007 , dis_direction277 , lerpResult1060);
				float4 texCoord1824 = IN.ase_texcoord4;
				texCoord1824.xy = IN.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1825 = clamp( _Custom7 , 0.0 , 1.0 );
				float lerpResult1827 = lerp( texCoord1824.x , texCoord1824.y , clampResult1825);
				float clampResult1826 = clamp( ( _Custom7 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1831 = lerp( lerpResult1827 , texCoord1824.z , clampResult1826);
				float clampResult1829 = clamp( ( _Custom7 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1833 = lerp( lerpResult1831 , texCoord1824.w , clampResult1829);
				float4 texCoord1834 = IN.ase_texcoord5;
				texCoord1834.xy = IN.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1835 = clamp( ( _Custom7 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1836 = lerp( lerpResult1833 , texCoord1834.x , clampResult1835);
				float clampResult1837 = clamp( ( _Custom7 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1841 = lerp( lerpResult1836 , texCoord1834.y , clampResult1837);
				float clampResult1839 = clamp( ( _Custom7 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1842 = lerp( lerpResult1841 , texCoord1834.z , clampResult1839);
				float clampResult1843 = clamp( ( _Custom7 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1844 = lerp( lerpResult1842 , texCoord1834.w , clampResult1843);
				float custom71466 = lerpResult1844;
				float lerpResult62 = lerp( _Float6 , custom71466 , _Float11);
				float lerpResult913 = lerp( lerpResult62 , ( 1.0 - IN.ase_color.a ) , _Float129);
				float temp_output_45_0 = saturate( ( ( lerpResult916 + 1.0 ) - ( lerpResult913 * 2.0 ) ) );
				float smoothstepResult32 = smoothstep( lerpResult1090 , ( 1.0 - lerpResult1090 ) , temp_output_45_0);
				float dis_soft122 = smoothstepResult32;
				float dis_tex661 = lerpResult916;
				float temp_output_130_0 = (0.0 + (dis_tex661 - -0.5) * (1.0 - 0.0) / (1.5 - -0.5));
				float temp_output_105_0 = step( lerpResult913 , temp_output_130_0 );
				float dis_bright124 = temp_output_105_0;
				float lerpResult413 = lerp( dis_soft122 , dis_bright124 , _Float25);
				float customEye754 = IN.ase_texcoord2.z;
				float cameraDepthFade754 = (( customEye754 -_ProjectionParams.y - _Float0 ) / _Float55);
				float4 screenPos97 = IN.ase_texcoord9;
				float4 ase_screenPosNorm97 = screenPos97 / screenPos97.w;
				ase_screenPosNorm97.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm97.z : ase_screenPosNorm97.z * 0.5 + 0.5;
				float screenDepth97 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm97.xy ),_ZBufferParams);
				float distanceDepth97 = saturate( abs( ( screenDepth97 - LinearEyeDepth( ase_screenPosNorm97.z,_ZBufferParams ) ) / ( _Float16 ) ) );
				float depthfade_switch334 = _Float5;
				float lerpResult336 = lerp( distanceDepth97 , ( 1.0 - distanceDepth97 ) , depthfade_switch334);
				float depthfade126 = ( saturate( cameraDepthFade754 ) * lerpResult336 );
				float lerpResult338 = lerp( depthfade126 , 1.0 , depthfade_switch334);
				float2 appendResult480 = (float2(_Vector11.x , _Vector11.y));
				float2 uv_Mask = IN.ase_texcoord2.xy * _Mask_ST.xy + _Mask_ST.zw;
				float2 uv2_Mask = IN.ase_texcoord4.xy * _Mask_ST.xy + _Mask_ST.zw;
				float2 lerpResult931 = lerp( uv_Mask , uv2_Mask , uv2930);
				float3 appendResult823 = (float3(1.0 , _Vector11.z , 0.0));
				float3 appendResult824 = (float3(_Vector11.w , 1.0 , 0.0));
				float2 appendResult481 = (float2(_Mask_ST.z , _Mask_ST.w));
				float4 texCoord1750 = IN.ase_texcoord4;
				texCoord1750.xy = IN.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1751 = clamp( _Custom3 , 0.0 , 1.0 );
				float lerpResult1732 = lerp( texCoord1750.x , texCoord1750.y , clampResult1751);
				float clampResult1731 = clamp( ( _Custom3 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1734 = lerp( lerpResult1732 , texCoord1750.z , clampResult1731);
				float clampResult1735 = clamp( ( _Custom3 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1739 = lerp( lerpResult1734 , texCoord1750.w , clampResult1735);
				float4 texCoord1740 = IN.ase_texcoord5;
				texCoord1740.xy = IN.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1737 = clamp( ( _Custom3 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1741 = lerp( lerpResult1739 , texCoord1740.x , clampResult1737);
				float clampResult1742 = clamp( ( _Custom3 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1744 = lerp( lerpResult1741 , texCoord1740.y , clampResult1742);
				float clampResult1745 = clamp( ( _Custom3 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1748 = lerp( lerpResult1744 , texCoord1740.z , clampResult1745);
				float clampResult1747 = clamp( ( _Custom3 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1749 = lerp( lerpResult1748 , texCoord1740.w , clampResult1747);
				float custom31359 = lerpResult1749;
				float4 texCoord1755 = IN.ase_texcoord4;
				texCoord1755.xy = IN.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1754 = clamp( _Custom4 , 0.0 , 1.0 );
				float lerpResult1758 = lerp( texCoord1755.x , texCoord1755.y , clampResult1754);
				float clampResult1757 = clamp( ( _Custom4 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1760 = lerp( lerpResult1758 , texCoord1755.z , clampResult1757);
				float clampResult1761 = clamp( ( _Custom4 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1765 = lerp( lerpResult1760 , texCoord1755.w , clampResult1761);
				float4 texCoord1766 = IN.ase_texcoord5;
				texCoord1766.xy = IN.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1763 = clamp( ( _Custom4 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1768 = lerp( lerpResult1765 , texCoord1766.x , clampResult1763);
				float clampResult1769 = clamp( ( _Custom4 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1770 = lerp( lerpResult1768 , texCoord1766.y , clampResult1769);
				float clampResult1771 = clamp( ( _Custom4 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1773 = lerp( lerpResult1770 , texCoord1766.z , clampResult1771);
				float clampResult1774 = clamp( ( _Custom4 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1775 = lerp( lerpResult1773 , texCoord1766.w , clampResult1774);
				float custom41379 = lerpResult1775;
				float2 appendResult75 = (float2(custom31359 , custom41379));
				float2 lerpResult474 = lerp( appendResult481 , appendResult75 , _Float12);
				float2 CenteredUV15_g164 = ( IN.ase_texcoord2.xy - float2( 0.5,0.5 ) );
				float2 break17_g164 = CenteredUV15_g164;
				float2 appendResult23_g164 = (float2(( length( CenteredUV15_g164 ) * _Mask_ST.x * 2.0 ) , ( atan2( break17_g164.x , break17_g164.y ) * ( 1.0 / TWO_PI ) * _Mask_ST.y )));
				float2 lerpResult471 = lerp( appendResult481 , appendResult75 , _Float12);
				float2 lerpResult467 = lerp( ( mul( float3( lerpResult931 ,  0.0 ), float3x3(appendResult823, appendResult824, float3(0,0,1)) ).xy + lerpResult474 ) , (mul( float3( appendResult23_g164 ,  0.0 ), float3x3(appendResult823, appendResult824, float3(0,0,1)) ).xy*float2( 1,1 ) + lerpResult471) , _Float51);
				float2 panner79 = ( 1.0 * _Time.y * appendResult480 + lerpResult467);
				float noise_intensity_mask736 = ( _Float80 * 0.1 * lerpResult1575 );
				float temp_output_322_0 = ( noise70 * noise_intensity_mask736 );
				float cos392 = cos( ( _Float43 * PI ) );
				float sin392 = sin( ( _Float43 * PI ) );
				float2 rotator392 = mul( ( panner79 + temp_output_322_0 ) - float2( 0.5,0.5 ) , float2x2( cos392 , -sin392 , sin392 , cos392 )) + float2( 0.5,0.5 );
				float2 break617 = rotator392;
				float clampResult618 = clamp( break617.x , 0.0 , 1.0 );
				float lerpResult769 = lerp( break617.x , clampResult618 , _Float64);
				float clampResult619 = clamp( break617.y , 0.0 , 1.0 );
				float lerpResult771 = lerp( break617.y , clampResult619 , _Float82);
				float2 appendResult616 = (float2(lerpResult769 , lerpResult771));
				float4 tex2DNode8 = tex2D( _Mask, appendResult616 );
				float lerpResult406 = lerp( tex2DNode8.a , tex2DNode8.r , _mask01_alpha);
				float clampResult1993 = clamp( ( _mask01_alpha - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1994 = lerp( lerpResult406 , tex2DNode8.g , clampResult1993);
				float clampResult1990 = clamp( ( _mask01_alpha - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1989 = lerp( lerpResult1994 , tex2DNode8.b , clampResult1990);
				float2 appendResult485 = (float2(_Vector13.x , _Vector13.y));
				float2 uv_Mask1 = IN.ase_texcoord2.xy * _Mask1_ST.xy + _Mask1_ST.zw;
				float2 uv2_Mask1 = IN.ase_texcoord4.xy * _Mask1_ST.xy + _Mask1_ST.zw;
				float2 lerpResult934 = lerp( uv_Mask1 , uv2_Mask1 , uv2930);
				float3 appendResult833 = (float3(1.0 , _Vector13.z , 0.0));
				float3 appendResult834 = (float3(_Vector13.w , 1.0 , 0.0));
				float2 appendResult486 = (float2(_Mask1_ST.z , _Mask1_ST.w));
				float2 CenteredUV15_g163 = ( IN.ase_texcoord2.xy - float2( 0.5,0.5 ) );
				float2 break17_g163 = CenteredUV15_g163;
				float2 appendResult23_g163 = (float2(( length( CenteredUV15_g163 ) * _Mask1_ST.x * 2.0 ) , ( atan2( break17_g163.x , break17_g163.y ) * ( 1.0 / TWO_PI ) * _Mask1_ST.y )));
				float2 lerpResult495 = lerp( ( mul( float3( lerpResult934 ,  0.0 ), float3x3(appendResult833, appendResult834, float3(0,0,1)) ).xy + appendResult486 ) , (mul( float3( appendResult23_g163 ,  0.0 ), float3x3(appendResult833, appendResult834, float3(0,0,1)) ).xy*float2( 1,1 ) + appendResult486) , _Float52);
				float2 panner216 = ( 1.0 * _Time.y * appendResult485 + lerpResult495);
				float cos389 = cos( ( _Float42 * PI ) );
				float sin389 = sin( ( _Float42 * PI ) );
				float2 rotator389 = mul( ( temp_output_322_0 + panner216 ) - float2( 0.5,0.5 ) , float2x2( cos389 , -sin389 , sin389 , cos389 )) + float2( 0.5,0.5 );
				float2 break623 = rotator389;
				float clampResult624 = clamp( break623.x , 0.0 , 1.0 );
				float lerpResult772 = lerp( break623.x , clampResult624 , _Float65);
				float clampResult625 = clamp( break623.y , 0.0 , 1.0 );
				float lerpResult773 = lerp( break623.y , clampResult625 , _Float83);
				float2 appendResult622 = (float2(lerpResult772 , lerpResult773));
				float4 tex2DNode218 = tex2D( _Mask1, appendResult622 );
				float lerpResult407 = lerp( tex2DNode218.a , tex2DNode218.r , _mask02_alpha);
				float clampResult2000 = clamp( ( _mask02_alpha - 1.0 ) , 0.0 , 1.0 );
				float lerpResult2001 = lerp( lerpResult407 , tex2DNode218.g , clampResult2000);
				float clampResult1997 = clamp( ( _mask02_alpha - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1996 = lerp( lerpResult2001 , tex2DNode218.b , clampResult1997);
				float Mask82 = ( lerpResult1989 * lerpResult1996 );
				ase_worldViewDir = SafeNormalize( ase_worldViewDir );
				float3 normalizedWorldNormal = normalize( ase_worldNormal );
				float fresnelNdotV139 = dot( normalize( ( normalizedWorldNormal * ase_vface ) ), ase_worldViewDir );
				float fresnelNode139 = ( 0.0 + _power3 * pow( max( 1.0 - fresnelNdotV139 , 0.0001 ), _Float19 ) );
				float temp_output_140_0 = saturate( fresnelNode139 );
				float lerpResult144 = lerp( temp_output_140_0 , ( 1.0 - temp_output_140_0 ) , _Float20);
				float fresnel147 = lerpResult144;
				float lerpResult347 = lerp( 1.0 , fresnel147 , _Float33);
				float temp_output_6_0 = ( lerpResult374 * IN.ase_color.a * _Color0.a * lerpResult413 * _Float15 * lerpResult338 * Mask82 * lerpResult347 );
				float clampResult602 = clamp( temp_output_6_0 , 0.0 , 1.0 );
				float lerpResult656 = lerp( temp_output_6_0 , clampResult602 , _Float70);
				float4 texCoord1975 = IN.ase_texcoord4;
				texCoord1975.xy = IN.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1959 = clamp( _Custom13 , 0.0 , 1.0 );
				float lerpResult1962 = lerp( texCoord1975.x , texCoord1975.y , clampResult1959);
				float clampResult1963 = clamp( ( _Custom13 - 1.0 ) , 0.0 , 1.0 );
				float lerpResult1964 = lerp( lerpResult1962 , texCoord1975.z , clampResult1963);
				float clampResult1965 = clamp( ( _Custom13 - 2.0 ) , 0.0 , 1.0 );
				float lerpResult1968 = lerp( lerpResult1964 , texCoord1975.w , clampResult1965);
				float4 texCoord1970 = IN.ase_texcoord5;
				texCoord1970.xy = IN.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult1969 = clamp( ( _Custom13 - 3.0 ) , 0.0 , 1.0 );
				float lerpResult1976 = lerp( lerpResult1968 , texCoord1970.x , clampResult1969);
				float clampResult1972 = clamp( ( _Custom13 - 4.0 ) , 0.0 , 1.0 );
				float lerpResult1977 = lerp( lerpResult1976 , texCoord1970.y , clampResult1972);
				float clampResult1973 = clamp( ( _Custom13 - 5.0 ) , 0.0 , 1.0 );
				float lerpResult1978 = lerp( lerpResult1977 , texCoord1970.z , clampResult1973);
				float clampResult1974 = clamp( ( _Custom13 - 6.0 ) , 0.0 , 1.0 );
				float lerpResult1979 = lerp( lerpResult1978 , texCoord1970.w , clampResult1974);
				float custom131619 = lerpResult1979;
				float lerpResult1621 = lerp( _Float72 , custom131619 , _Float151);
				clip( dis_tex661 - lerpResult1621);
				
				float Alpha = lerpResult656;
				float AlphaClipThreshold = 0.5;

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif
				return 0;
			}
			ENDHLSL
		}

	
	}
	CustomEditor "LWGUI.LWGUI"
	Fallback "Hidden/InternalErrorShader"
	
}
/*ASEBEGIN
Version=18900
6.4;16.8;2048;1074.2;-2917.861;1460.621;1.133949;True;True
Node;AmplifyShaderEditor.RangedFloatNode;929;-8118.507,-2083.851;Inherit;False;Property;_Float131;启用2u(需关闭customdata);9;1;[Toggle];Create;False;0;0;1;UnityEngine.Rendering.CullMode;True;1;SubToggle(g17,_);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1134;-2296.598,-5692.085;Inherit;False;Property;_Float144;使用屏幕uv;32;0;Create;False;0;0;0;False;1;SubToggle(g1, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;528;-8249.707,-392.0322;Inherit;False;Property;_noise_ST;_noise_ST;102;1;[HideInInspector];Create;True;0;0;0;False;0;False;1,1,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ScreenPosInputsNode;1635;-8034.555,-706.5149;Float;False;0;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;930;-7818.894,-2089.837;Inherit;False;uv2;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;944;-7554,-2866;Inherit;False;3602;2249;扰动;93;701;70;529;531;530;684;863;864;861;862;867;865;866;535;676;868;869;682;679;880;536;540;538;870;683;393;694;539;685;696;394;53;688;687;695;697;395;698;638;640;779;643;639;689;690;691;778;780;777;692;776;775;641;50;693;924;923;564;703;700;731;567;732;733;55;360;734;67;735;736;680;940;873;872;871;874;876;878;875;877;879;938;939;941;942;51;943;1575;1576;1577;1631;1632;1639;扰动;1,1,1,1;0;0
Node;AmplifyShaderEditor.GetLocalVarNode;942;-7264,-1424;Inherit;False;930;uv2;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;676;-7440,-1984;Inherit;False;Property;_noisemask_ST;_noisemask_ST;110;1;[HideInInspector];Create;True;0;0;0;False;0;False;1,1,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;1637;-7782.092,-692.427;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;51;-7456.003,-1628.444;Inherit;False;0;50;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;1641;-7820.25,-320.5547;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;872;-7504,-1312;Inherit;False;Constant;_Float114;Float 114;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;873;-7488,-1232;Inherit;False;Constant;_Float115;Float 115;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;874;-7504,-1136;Inherit;False;Constant;_Float116;Float 116;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ScreenPosInputsNode;1627;-8249.621,-3214.31;Float;False;0;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;1636;-7917.917,-493.7676;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;871;-7504,-1360;Inherit;False;Constant;_Float113;Float 113;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;943;-7459.56,-1496.888;Inherit;False;1;50;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector4Node;529;-7440,-848;Inherit;False;Property;_Vector17;扰动流动&斜切;103;0;Create;False;0;0;0;False;1;Sub(g4);False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;1625;-2088.075,-5687.888;Inherit;False;screenuv;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;680;-7447,-2820;Inherit;False;0;564;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector3Node;875;-7504,-1056;Inherit;False;Constant;_Vector27;Vector 27;138;0;Create;True;0;0;0;False;0;False;0,0,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.ScaleAndOffsetNode;1638;-7612.088,-605.7009;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;1,0;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;864;-7472,-2400;Inherit;False;Constant;_Float112;Float 112;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;876;-7248,-1344;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;861;-7488,-2224;Inherit;False;Constant;_Float109;Float 109;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;1640;-7561.225,-432.6714;Inherit;False;1625;screenuv;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;1628;-8013.186,-3027.42;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;862;-7456,-2320;Inherit;False;Constant;_Float110;Float 110;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;1629;-7997.157,-3200.222;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;939;-7360,-2530;Inherit;False;930;uv2;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;940;-7447,-2676;Inherit;False;1;564;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;878;-7232,-1232;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;863;-7488,-2448;Inherit;False;Constant;_Float111;Float 111;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;684;-7440,-1792;Inherit;False;Property;_Vector23;扰动遮罩流动&斜切;111;0;Create;False;0;0;0;False;1;Sub(g4);False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;941;-7072,-1536;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;1633;-8050.517,-2906.207;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;1639;-6898.503,-1443.815;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;866;-7266.18,-2428.202;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.FunctionNode;535;-6912,-1632;Inherit;False;Polar Coordinates;-1;;46;7dab8e02884cf104ebefaa2e788e4162;0;4;1;FLOAT2;0,0;False;2;FLOAT2;0.5,0.5;False;3;FLOAT;1;False;4;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector3Node;867;-7488,-2144;Inherit;False;Constant;_Vector26;Vector 26;138;0;Create;True;0;0;0;False;0;False;0,0,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.ScaleAndOffsetNode;1630;-7827.154,-3113.496;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;1,0;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;1632;-6952.104,-2551.464;Inherit;False;1625;screenuv;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.MatrixFromVectors;877;-7029.906,-1175.26;Inherit;False;FLOAT3x3;True;4;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3x3;0
Node;AmplifyShaderEditor.LerpOp;938;-7138,-2672;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;865;-7281.651,-2270.629;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;1631;-6769.084,-2799.651;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;1421;-958.1697,11025.57;Inherit;False;Property;_Custom6;溶解y轴;181;1;[Enum];Create;False;0;8;custom1x;0;custom1y;1;custom1z;2;custom1w;3;custom2x;4;custom2y;5;custom2z;6;custom2w;7;0;True;1;SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.MatrixFromVectors;868;-7008,-2336;Inherit;False;FLOAT3x3;True;4;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3x3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;880;-6656,-1600;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;531;-6955.771,-886.0676;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;879;-6707.425,-1271.273;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;1384;-1740.364,8018.193;Inherit;False;Property;_Custom5;溶解x轴;180;1;[Enum];Create;False;0;8;custom1x;0;custom1y;1;custom1z;2;custom1w;3;custom2x;4;custom2y;5;custom2z;6;custom2w;7;0;True;1;SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;682;-6508.3,-2849.129;Inherit;False;Polar Coordinates;-1;;47;7dab8e02884cf104ebefaa2e788e4162;0;4;1;FLOAT2;0,0;False;2;FLOAT2;0.5,0.5;False;3;FLOAT;1;False;4;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;1563;7448.747,9854.938;Inherit;False;Property;_Custom12;自定义扰动强度;192;1;[Enum];Create;False;0;8;custom1x;0;custom1y;1;custom1z;2;custom1w;3;custom2x;4;custom2y;5;custom2z;6;custom2w;7;0;True;1;SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w);False;4;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1284;-7188.302,9013.869;Inherit;False;Property;_Custom1;主贴图x轴;174;1;[Enum];Create;False;0;8;custom1x;0;custom1y;1;custom1z;2;custom1w;3;custom2x;4;custom2y;5;custom2z;6;custom2w;7;0;True;1;SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1335;-7557.565,10740.75;Inherit;False;Property;_Custom2;主贴图y轴;175;1;[Enum];Create;False;0;8;custom1x;0;custom1y;1;custom1z;2;custom1w;3;custom2x;4;custom2y;5;custom2z;6;custom2w;7;0;True;1;SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1708;-6734.486,10654.47;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;536;-6504.002,-1288.443;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1684;-6532.739,8175.458;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;869;-6707.539,-2346.486;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1779;-1830.786,6666.772;Inherit;False;1;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1778;-1439.45,7311.662;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1937;7607.161,9116.063;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1952;7384.373,8487.225;Inherit;False;1;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;1281;-6924.075,7530.568;Inherit;False;1;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;1728;-7125.822,10009.58;Inherit;False;1;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;679;-6990.459,-1953.58;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;540;-6592,-954;Inherit;False;Property;_Float54;扰动极坐标（竖向贴图）;100;0;Create;False;0;0;0;False;1;SubToggle(g4, _);False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1694;-6583.401,7882.191;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;870;-6640,-2496;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;1936;7556.5,8822.795;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1802;-1195.343,9728.043;Inherit;False;1;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ClampOpNode;1729;-6785.148,10361.2;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1777;-1490.112,7018.395;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1801;-804.0072,10372.93;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1800;-854.6693,10079.67;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;538;-6464,-1616;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;1,1;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1803;-734.4132,10517.26;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1782;-1244.836,7209.625;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1804;-645.4823,9786.376;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;-3.28;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1805;-609.3932,10270.9;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;683;-6562.586,-2156.872;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1780;-1369.856,7455.988;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1692;-6338.126,8073.421;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;393;-6320,-1136;Inherit;False;Property;_Float44;扰动贴图旋转;101;0;Create;False;0;0;0;False;1;Sub(g4);False;0;0;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;685;-6608,-1920;Inherit;False;Property;_Float57;扰动遮罩极坐标;108;0;Create;False;0;0;0;False;1;SubToggle(g4, _);False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1711;-6664.893,10798.8;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1709;-6539.873,10552.43;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1710;-6575.962,10067.91;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;-3.28;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;530;-6976,-752;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1938;7676.756,9260.387;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1940;7801.776,9014.025;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;539;-6144,-1456;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;694;-6480,-2512;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;1,1;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;1939;7765.686,8529.505;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;-3.28;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1781;-1280.925,6725.105;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;-3.28;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1690;-6463.146,8319.784;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1681;-6374.215,7588.901;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;-3.28;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1693;-6217.016,8320.313;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PiNode;394;-6048,-1248;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1943;7902.259,9544.898;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;3;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1941;7998.271,8785.803;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;687;-6993.543,-1795.621;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;1713;-6418.763,10799.33;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1712;-6343.378,10324.21;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1683;-6141.631,7845.201;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;696;-6336,-2032;Inherit;False;Property;_Float74;扰动遮罩旋转;109;0;Create;False;0;0;0;False;1;Sub(g4);False;0;0;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1942;7922.887,9260.918;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1807;-508.9113,10801.77;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;3;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1696;-6237.644,8604.294;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;3;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1806;-488.2831,10517.79;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1784;-1144.354,7740.499;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;3;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1783;-1123.726,7456.518;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;53;-5856,-1424;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;1808;-412.8983,10042.68;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;688;-6176,-2352;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1714;-6439.391,11083.31;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;3;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1785;-1048.341,6981.405;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1946;8119.665,9541.321;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1786;-883.7617,8023.315;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;4;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1717;-6152.054,10655.45;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1947;7629.413,10302.95;Inherit;False;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;1945;8189.596,9117.04;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1698;-6606.615,9179.978;Inherit;False;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RotatorNode;395;-5648,-1392;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;1689;-5950.307,8176.436;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1787;-926.9468,7736.923;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1788;-857.0166,7312.64;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1718;-6909.691,11719.16;Inherit;False;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1944;8162.85,9827.715;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;4;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1809;-248.3192,11084.59;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;4;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;697;-5872,-2320;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;1695;-6020.237,8600.719;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1701;-5977.052,8887.11;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;4;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1810;-291.5043,10798.19;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1789;-1394.857,8570.042;Inherit;False;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1716;-6178.799,11366.12;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;4;False;1;FLOAT;0
Node;AmplifyShaderEditor.PiNode;695;-6064,-2144;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1811;-221.5741,10373.91;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1812;-877.8832,11377.45;Inherit;False;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ClampOpNode;1715;-6221.984,11079.73;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1720;-5961.392,11362.55;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1790;-589.4707,8286.584;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1470;1806.302,9689.6;Inherit;False;Property;_Custom8;自定义flowmap扭曲;190;1;[Enum];Create;False;0;8;custom1x;0;custom1y;1;custom1z;2;custom1w;3;custom2x;4;custom2y;5;custom2z;6;custom2w;7;0;True;1;SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w);False;4;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1791;-557.6587,7597.151;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1699;-5759.645,8883.535;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1948;8457.141,10090.98;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1953;8403.342,9380.146;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1721;-5909.697,11595.21;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1949;8380.258,9824.139;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;638;-6016,-1104;Inherit;False;FLOAT2;1;0;FLOAT2;0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.LerpOp;1697;-5650.949,8460.946;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1792;-666.3547,8019.74;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1703;-5682.761,9150.38;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.RotatorNode;698;-5664,-2288;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;1719;-5852.696,10939.96;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1814;77.78382,10658.42;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1815;-30.91223,11081.01;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1813;45.97182,11347.86;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1793;-297.0667,7879.967;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1957;8511.306,10360.89;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;6;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1724;-5751.242,11886.7;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;6;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1950;8674.549,10087.41;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1700;-5390.357,8743.763;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1707;-5518.908,9420.287;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;6;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1702;-5465.354,9146.805;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1722;-5592.104,11222.77;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;643;-5568,-1504;Inherit;False;Property;_Float67;扰动贴图x轴Clamp;98;0;Create;False;0;0;0;True;1;SubToggle(g4, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;689;-6032,-2000;Inherit;False;FLOAT2;1;0;FLOAT2;0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.ClampOpNode;1723;-5667.101,11625.82;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1954;8586.348,9695.066;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1845;2211.283,9188.104;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;779;-5584,-928;Inherit;False;Property;_Float85;扰动贴图y轴Clamp;99;0;Create;False;0;0;0;True;1;SubToggle(g4, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1846;1819.947,8543.213;Inherit;False;1;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ClampOpNode;1818;263.3788,11344.28;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1817;209.8248,11617.76;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;6;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1795;-372.0637,8283.01;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1847;2160.621,8894.836;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1794;-425.6177,8556.492;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;6;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1816;338.3759,10941.24;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;640;-5744,-1184;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;639;-5776,-1024;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1726;-5347.715,11482.48;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;780;-5392,-1040;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1796;-2.775635,8143.237;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1705;-5301.5,9416.712;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1797;-208.2097,8552.916;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;512;-7099.938,4466.086;Inherit;False;2803.434;1805.293;Comment;42;502;57;506;509;510;507;500;511;501;58;92;314;317;302;304;306;307;309;303;738;737;807;808;809;810;811;812;813;814;815;816;951;952;953;1422;1423;1424;1425;1426;1427;1490;499;溶解uv;1,1,1,1;0;0
Node;AmplifyShaderEditor.LerpOp;778;-5472,-1280;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1820;427.2328,11614.19;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1725;-5503.247,11895.72;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;817;-3436.151,-5587.112;Inherit;False;2519.616;1473.895;主贴图;28;439;802;801;798;796;803;799;804;431;39;42;433;795;60;793;59;443;43;806;449;446;445;444;440;36;161;1338;1339;主贴图;1,1,1,1;0;0
Node;AmplifyShaderEditor.LerpOp;1704;-5096.066,9007.032;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;690;-5728,-1984;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1951;8838.402,10357.32;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;777;-5808,-1680;Inherit;False;Property;_Float84;扰动遮罩y轴Clamp;107;0;Create;False;0;0;0;True;1;SubToggle(g4, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1848;2405.897,9086.066;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1850;2280.877,9332.428;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1955;8877.964,9939.609;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;691;-5776,-1808;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1849;2369.808,8601.546;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;-3.28;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1819;632.6669,11204.51;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;692;-5760,-2064;Inherit;False;Property;_Float73;扰动遮罩x轴Clamp;106;0;Create;False;0;0;0;True;1;SubToggle(g4, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1368;-4133.585,11106.85;Inherit;False;Property;_Custom4;Masky轴;178;1;[Enum];Create;False;0;8;custom1x;0;custom1y;1;custom1z;2;custom1w;3;custom2x;4;custom2y;5;custom2z;6;custom2w;7;0;True;1;SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;641;-5296,-1328;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;776;-5504,-1760;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1821;796.5209,11474.42;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1706;-4932.212,9276.939;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;499;-7010.582,5493.68;Inherit;False;Property;_dissolvetex_ST;_dissolvetex_ST;80;1;[HideInInspector];Create;True;0;0;0;False;0;False;1,1,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;1348;-4471.788,8519.661;Inherit;False;Property;_Custom3;Maskx轴;177;1;[Enum];Create;False;0;8;custom1x;0;custom1y;1;custom1z;2;custom1w;3;custom2x;4;custom2y;5;custom2z;6;custom2w;7;0;True;1;SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1727;-5133.959,11755.95;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScreenPosInputsNode;1642;-7144.087,4007.132;Float;False;0;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ScreenPosInputsNode;1128;-3359.286,-6060.497;Float;True;0;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1852;2506.38,9616.939;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;3;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1956;9071.247,10220.22;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1853;2602.393,8857.845;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1798;161.0784,8413.145;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1851;2527.008,9332.959;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;431;-3028.323,-4777.736;Inherit;False;Property;_maintex_ST;主贴图tilling&offset;39;1;[HideInInspector];Create;False;0;0;0;False;0;False;1,1,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;775;-5456,-2112;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1856;2255.876,10446.48;Inherit;False;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;814;-7000.918,4956.575;Inherit;False;Constant;_Float90;Float 90;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;953;-7055.457,4686.631;Inherit;False;1;23;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;808;-7005.3,5037.587;Inherit;False;Constant;_Float92;Float 92;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;807;-7004.14,5196.008;Inherit;False;Constant;_Float95;Float 95;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;50;-5184,-1648;Inherit;True;Property;_noise;扰动贴图;97;0;Create;False;0;0;0;False;1;Sub(g4);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;1648;-6929.781,4393.092;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;57;-7049.938,4545.794;Inherit;False;0;23;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector4Node;924;-5072,-1456;Inherit;False;Property;_Vector33;扰动贴图remap;112;0;Create;False;0;0;0;False;1;Sub(g4);False;0,1,0,1;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;801;-3269.093,-5029.263;Inherit;False;Constant;_Float93;Float 93;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;796;-3201.203,-5412.148;Inherit;False;Constant;_Float13;Float 13;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;1401;649.1306,8447.95;Inherit;False;custom5;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;1337;-4817.379,11766.65;Inherit;False;custom2;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;1131;-2877.044,-5844.651;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;1130;-2755.223,-5706.827;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;1336;-4643.178,9436.251;Inherit;False;custom1;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1854;2766.972,9899.756;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;4;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;802;-3239.448,-5134.948;Inherit;False;Constant;_Float94;Float 94;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;693;-5312,-1936;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;1420;1174.35,11420.1;Inherit;False;custom6;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1857;2723.787,9613.363;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;809;-7006.597,5116.734;Inherit;False;Constant;_Float96;Float 96;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;1644;-6891.624,4021.22;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;1855;2793.717,9189.081;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;500;-6988.023,5728.638;Inherit;False;Property;_Vector15;溶解流动&斜切;81;0;Create;False;0;0;0;False;1;Sub(g3);False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;951;-6901.749,4838.882;Inherit;False;930;uv2;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;1679;-2928.788,-6045.825;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;798;-3227.035,-5274.781;Inherit;False;Constant;_Float91;Float 91;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;439;-3316.591,-4580.434;Inherit;False;Property;_Vector0;主贴图流动&斜切;45;0;Create;False;0;0;0;False;1;Sub(g1);False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1756;-3840.109,10414.21;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1755;-4231.446,9769.314;Inherit;False;1;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;1750;-4503.461,7284.329;Inherit;False;1;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1730;-4112.125,7929.219;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;1574;9544.953,10229.24;Inherit;False;custom12;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1754;-3890.772,10120.94;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;1643;-7027.45,4219.879;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;1751;-4162.787,7635.952;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;1423;-6490.43,5183.409;Inherit;False;1401;custom5;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1733;-4042.531,8073.545;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;952;-6661.457,4653.631;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;1576;-4902.91,-1110.768;Inherit;False;1574;custom12;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1577;-4822.536,-945.9539;Inherit;False;Property;_Float257;custom控制扰动总强度;191;0;Create;False;0;0;0;True;3;SubToggle(g7, _);Space;Space;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1758;-3681.585,9827.647;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;-3.28;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1859;2984.379,9896.181;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1759;-3770.517,10558.53;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;811;-6778.965,5067.933;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;1732;-3953.6,7342.662;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;-3.28;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1858;3093.075,9473.591;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;1647;-6670.758,4280.975;Inherit;False;1625;screenuv;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;1424;-6498.086,5258.427;Inherit;False;1420;custom6;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode;923;-4688,-1648;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;564;-5136,-1984;Inherit;True;Property;_noisemask;扰动遮罩;105;0;Create;False;0;0;0;False;1;Sub(g4);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture1D;8;0;SAMPLER1D;;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ScaleAndOffsetNode;1645;-6721.621,4107.946;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;1,0;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;1626;-2480.023,-5763.765;Inherit;False;1625;screenuv;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1731;-3917.511,7827.182;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;810;-6774.274,4950.791;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;1133;-2578.393,-6009.458;Inherit;True;3;0;FLOAT2;0,0;False;1;FLOAT2;1,0;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;803;-2980.828,-5171.457;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Vector3Node;812;-7004.092,5281.997;Inherit;False;Constant;_Vector2;Vector 2;138;0;Create;True;0;0;0;False;0;False;0,0,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.TextureCoordinatesNode;35;-3018.212,-6276.304;Inherit;False;0;1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;799;-2969.437,-5363.44;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;1339;-2727.703,-4343.889;Inherit;False;1337;custom2;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1757;-3645.497,10312.17;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1860;3061.263,10163.03;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector3Node;804;-3263.981,-4906.542;Inherit;False;Constant;_Vector1;Vector 1;138;0;Create;True;0;0;0;False;0;False;0,0,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.GetLocalVarNode;1338;-2738.875,-4436.459;Inherit;False;1336;custom1;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;731;-5045.2,-927.9001;Inherit;False;Property;_Float79;溶解扰动强度;115;0;Create;False;0;0;0;False;1;Sub(g4);False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1736;-3817.029,8358.056;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;3;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1760;-3449.001,10083.95;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1646;-6347.567,4139.22;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;1735;-3796.401,8074.075;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;506;-6419.332,4526.875;Inherit;False;Polar Coordinates;-1;;105;7dab8e02884cf104ebefaa2e788e4162;0;4;1;FLOAT2;0,0;False;2;FLOAT2;0.5,0.5;False;3;FLOAT;1;False;4;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;502;-6613.739,5533.312;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;1863;3353.667,9756.406;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1427;-6322.462,5400.813;Inherit;False;Property;_Float50;溶解自定义偏移;179;0;Create;False;0;0;0;True;3;SubToggle(g7, _);Space;Space;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1734;-3721.016,7598.962;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1862;3225.116,10432.93;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;6;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1761;-3524.387,10559.06;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;1425;-6295.405,5219.151;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;700;-4480,-1966;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;60;-2162.941,-4370.634;Inherit;False;Property;_Float10;主贴图自定义偏移;173;0;Create;False;0;0;0;True;1;SubToggle(g7, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.MatrixFromVectors;813;-6592.797,5026.306;Inherit;False;FLOAT3x3;True;4;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3x3;0
Node;AmplifyShaderEditor.RangedFloatNode;703;-4797,-1357;Inherit;False;Property;_Float76;扰动遮罩/双重扰动（add为双重扰动）;104;1;[Enum];Create;False;0;2;multiply;0;add;1;0;True;1;KWEnum(g4,multiply,_0,add,_1);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.MatrixFromVectors;795;-2752.952,-5108.408;Inherit;False;FLOAT3x3;True;4;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3x3;0
Node;AmplifyShaderEditor.DynamicAppendNode;42;-2443.154,-4429.736;Inherit;True;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;433;-2453.469,-4780.213;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;1575;-4682.111,-1165.668;Inherit;False;3;0;FLOAT;1;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1861;3278.67,10159.45;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;567;-4448,-1728;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1127;-2142.009,-5946.699;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode;443;-2566.542,-5351.202;Inherit;False;Polar Coordinates;-1;;104;7dab8e02884cf104ebefaa2e788e4162;0;4;1;FLOAT2;0,0;False;2;FLOAT2;0.5,0.5;False;3;FLOAT;1;False;4;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1762;-3545.015,10843.04;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;3;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1426;-6038.052,5312.617;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;1763;-3327.607,10839.46;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;701;-4256,-1824;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1740;-4186.001,8933.739;Inherit;False;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ClampOpNode;1737;-3599.622,8354.48;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1864;3647.958,10019.68;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1865;3442.524,10429.36;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1764;-3284.422,11125.86;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;4;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;816;-6132.315,4641.277;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;59;-2178.678,-4777.515;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1738;-3556.437,8640.872;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;4;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1422;-6102.359,4962.002;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;815;-6279.47,4838.089;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;1739;-3529.692,7930.197;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;806;-2216.55,-5291.314;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;449;-2015.384,-5158.836;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;732;-4427.401,-878.5001;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0.1;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;312;-9099.795,-3866.515;Inherit;False;1094.708;330.5801;flowmap;7;242;285;310;311;305;316;1982;flowmap;1,1,1,1;0;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1766;-4046.854,11412.22;Inherit;False;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;793;-2353.075,-5119.673;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;1765;-3257.678,10415.18;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;305;-8518.148,-3713.141;Inherit;False;Property;_Float31;custom控制flowmap扭曲;189;0;Create;False;0;0;0;True;3;SubToggle(g7, _);Space;Space;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;70;-4176,-1584;Inherit;False;noise;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1743;-3262.146,8904.142;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1742;-3339.03,8637.297;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;1982;-9030.304,-3799.366;Inherit;True;Property;_flowmap;flowmapTex;117;0;Create;False;0;0;0;False;1;Sub(g14);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;1768;-2958.32,10699.69;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1767;-2990.132,11389.13;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;310;-8537.455,-3618.126;Inherit;False;Property;_Float32;flowmap扰动;118;0;Create;False;0;0;0;False;1;Sub(g14);False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;43;-1892.892,-4914.449;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;1866;3811.812,10289.58;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;507;-5882.689,4579.836;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;1,1;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;510;-5703.289,5085.664;Inherit;False;Property;_Float53;溶解极坐标（竖向贴图）;78;0;Create;False;0;0;0;False;1;SubToggle(g3, _);False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1741;-3230.334,8214.708;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1769;-3067.016,11122.28;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;446;-1884.304,-5308.937;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;1,1;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;733;-4250.401,-857.7;Inherit;False;noise_intensity_dis;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;445;-1719.982,-4678.461;Inherit;False;Property;_Float49;主贴图极坐标（竖向贴图）;37;0;Create;False;0;0;0;False;1;SubToggle(g1, _);False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;509;-5880.8,5079.665;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;304;-5628.349,5620.964;Inherit;False;70;noise;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;501;-6587.124,5737.68;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;511;-5533.591,4668.26;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;302;-5621.904,5755.349;Inherit;False;733;noise_intensity_dis;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;242;-8641.455,-3815.126;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;316;-8232.265,-3733.154;Inherit;False;flpwmap_custom_switch;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;1489;4180.227,10238.5;Inherit;False;custom8;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;311;-8216.004,-3618.563;Inherit;False;flowmap_intensity;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;440;-2597.618,-4561.853;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;1771;-2772.725,11385.55;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1744;-2969.742,8497.524;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1745;-3044.739,8900.566;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1746;-3098.293,9174.049;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;6;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1770;-2697.728,10982.51;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;444;-1705.038,-5172.987;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1772;-2826.279,11659.03;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;6;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;314;-5512.011,5892.948;Inherit;False;311;flowmap_intensity;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;1490;-5394.773,5996.486;Inherit;False;1489;custom8;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1773;-2403.437,11245.78;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1748;-2675.451,8760.794;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;738;-5298.685,5539.158;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;58;-5158.785,4911.808;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;1747;-2880.885,9170.474;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;317;-5337.241,6132.879;Inherit;False;316;flpwmap_custom_switch;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;285;-8333.42,-3828.74;Inherit;False;flowmap;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode;1204;3936.073,-4862.643;Inherit;False;1112.73;708.7222;视差;8;1095;1096;1121;1104;1103;1094;1092;1097;视差;1,1,1,1;0;0
Node;AmplifyShaderEditor.ClampOpNode;1774;-2608.871,11655.46;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;36;-1540.054,-4877.719;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;1749;-2511.597,9030.701;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;161;-1168.652,-4864.328;Inherit;False;maintexUV_00;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;737;-5036.836,5456.622;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;1095;4049.456,-4442.891;Inherit;False;Property;_Float38;视差缩放;170;0;Create;False;0;0;0;False;1;Sub(g15);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;307;-5046.755,5915.552;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;306;-5341.993,5815.078;Inherit;False;285;flowmap;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode;498;-7011.592,-5547.575;Inherit;False;3316.701;2494.345;Comment;94;478;74;73;75;481;483;486;474;77;217;477;469;471;491;497;479;496;468;472;321;323;484;494;322;485;495;467;480;79;390;216;387;319;388;320;391;392;389;408;405;218;8;406;407;220;620;622;623;624;625;626;616;769;770;771;772;774;773;818;819;820;821;822;823;824;825;826;827;830;835;834;833;836;837;828;931;932;933;934;935;936;1380;1381;1990;1991;1992;1993;1994;1989;1997;1998;1999;2000;2001;Mask;1,1,1,1;0;0
Node;AmplifyShaderEditor.LerpOp;1775;-2239.583,11515.69;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;831;-6970.522,-3875.013;Inherit;False;Constant;_Float104;Float 104;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;1379;-1844.63,11591.29;Inherit;False;custom4;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;830;-6968.703,-4128.217;Inherit;False;Constant;_Float103;Float 103;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;1103;4316.197,-4525.853;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;1359;-2202.517,9029.662;Inherit;False;custom3;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexturePropertyNode;1094;4000.179,-4650.874;Inherit;True;Property;_parallax;视差贴图;169;0;Create;False;0;0;0;False;1;Sub(g15);False;None;None;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.RangedFloatNode;818;-7000.641,-4975.674;Inherit;False;Constant;_Float97;Float 97;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;484;-6928,-3280;Inherit;False;Property;_Vector13;遮罩02流动&斜切;72;0;Create;False;0;0;0;False;1;Sub(g2);False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;821;-6994.715,-5063.575;Inherit;False;Constant;_Float100;Float 100;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;636;-3862.247,-1551.912;Inherit;False;2648.927;985.3096;Comment;44;385;386;635;633;632;634;631;29;281;49;23;25;62;31;24;30;26;33;45;34;32;122;61;384;93;661;784;785;786;912;913;914;915;916;918;1060;1062;1089;1090;1091;1262;1269;1469;1512;软溶解;1,1,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;1104;4358.416,-4320.314;Inherit;False;Property;_refplane;refplane(0黑色下沉,1白色抬高);171;0;Create;False;0;0;0;False;1;Sub(g15);False;1;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;819;-6970.603,-5133.207;Inherit;False;Constant;_Float98;Float 98;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;829;-6958.109,-4014.846;Inherit;False;Constant;_Float102;Float 102;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;1096;4032.951,-4341.921;Inherit;False;Tangent;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;828;-6999.883,-3777.416;Inherit;False;Constant;_Float101;Float 101;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;55;-5056,-1184;Inherit;False;Property;_Float9;主贴图扰动强度;113;0;Create;False;0;0;0;False;1;Sub(g4);False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;820;-6958.883,-5227.491;Inherit;False;Constant;_Float99;Float 99;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1435;1622.345,7498.39;Inherit;False;Property;_Custom7;自定义溶解;185;1;[Enum];Create;False;0;8;custom1x;0;custom1y;1;custom1z;2;custom1w;3;custom2x;4;custom2y;5;custom2z;6;custom2w;7;0;True;1;SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w);False;4;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;309;-4865.985,5570.581;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;1121;3986.073,-4812.643;Inherit;False;161;maintexUV_00;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector4Node;479;-6983.609,-4667.555;Inherit;False;Property;_Vector11;遮罩01流动速度&斜切;64;0;Create;False;0;0;0;False;1;Sub(g2);False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;933;-6830.65,-5338.782;Inherit;False;930;uv2;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector3Node;832;-7014.055,-3695.607;Inherit;False;Constant;_Vector4;Vector 4;138;0;Create;True;0;0;0;False;0;False;0,0,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.DynamicAppendNode;824;-6633.684,-5178.038;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Vector3Node;822;-6956.994,-4859.457;Inherit;False;Constant;_Vector3;Vector 3;138;0;Create;True;0;0;0;False;0;False;0,0,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.TextureCoordinatesNode;1824;1608.937,6144.143;Inherit;False;1;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;384;-3836.248,-1350.417;Inherit;False;Property;_Float41;溶解贴图旋转;79;0;Create;False;0;0;0;False;1;Sub(g3);False;0;0;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.ParallaxOcclusionMappingNode;1092;4540.363,-4659.027;Inherit;False;0;128;False;-1;128;False;-1;10;0.02;1;False;1,1;False;0,0;8;0;FLOAT2;0,0;False;1;SAMPLER2D;;False;7;SAMPLERSTATE;;False;2;FLOAT;0.02;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;5;FLOAT2;0,0;False;6;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;834;-6736.532,-3898.222;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Vector4Node;483;-6991.586,-3518.17;Inherit;False;Property;_Mask1_ST;_Mask1_ST;71;1;[HideInInspector];Create;True;0;0;0;False;0;False;1,1,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;360;-4455.5,-1258.5;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0.1;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;478;-6635.028,-4956.565;Inherit;False;Property;_Mask_ST;_Mask_ST;63;1;[HideInInspector];Create;True;0;0;0;False;0;False;1,1,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;1380;-6430.081,-4540.629;Inherit;False;1359;custom3;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;935;-6793.533,-4185.381;Inherit;False;930;uv2;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1823;2000.273,6789.033;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;936;-6989.049,-4266.879;Inherit;False;1;218;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;77;-6986.136,-5532.217;Inherit;False;0;8;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;92;-4520.506,4822.985;Inherit;False;dissolveUV;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;833;-6765.604,-4094.595;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DynamicAppendNode;823;-6625.031,-5318.283;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;932;-7012.682,-5407.778;Inherit;False;1;8;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;1381;-6421.323,-4431.144;Inherit;False;1379;custom4;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;217;-6970.399,-4404.645;Inherit;False;0;218;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ClampOpNode;1825;1949.611,6495.766;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;75;-6200.814,-4552.459;Inherit;True;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1828;2069.867,6933.358;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;1097;4824.803,-4636.033;Inherit;False;parallax;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.MatrixFromVectors;835;-6594.048,-3903.101;Inherit;False;FLOAT3x3;True;4;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3x3;0
Node;AmplifyShaderEditor.DynamicAppendNode;481;-6351.458,-4939.401;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;93;-3639.004,-1501.912;Inherit;False;92;dissolveUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.MatrixFromVectors;825;-6472.706,-5224.272;Inherit;False;FLOAT3x3;True;4;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3x3;0
Node;AmplifyShaderEditor.RangedFloatNode;734;-5049.1,-1033;Inherit;False;Property;_Float80;mask扰动强度;114;0;Create;False;0;0;0;False;1;Sub(g4);False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;73;-5845.026,-4463.095;Inherit;False;Property;_Float12;mask01自定义偏移;176;0;Create;False;0;0;0;True;3;SubToggle(g7, _);Space;Space;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1826;2194.887,6686.996;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;497;-6209.446,-4202.971;Inherit;False;Polar Coordinates;-1;;163;7dab8e02884cf104ebefaa2e788e4162;0;4;1;FLOAT2;0,0;False;2;FLOAT2;0.5,0.5;False;3;FLOAT;1;False;4;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;67;-4213,-1269.3;Inherit;False;noise_intensity_main;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1827;2158.798,6202.476;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;-3.28;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;931;-6622.682,-5476.778;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;934;-6594.565,-4286.377;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode;477;-6130.515,-5509.042;Inherit;False;Polar Coordinates;-1;;164;7dab8e02884cf104ebefaa2e788e4162;0;4;1;FLOAT2;0,0;False;2;FLOAT2;0.5,0.5;False;3;FLOAT;1;False;4;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PiNode;385;-3549.538,-1253.424;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;72;-1355.318,-2039.176;Inherit;False;67;noise_intensity_main;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;837;-6078.913,-3944.78;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;162;-1347.398,-2411.018;Inherit;False;161;maintexUV_00;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;471;-5883.976,-5237.453;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode;278;-3229.496,771.2501;Inherit;False;2403.086;1048.659;溶解方向+开洞l;23;277;415;416;417;418;568;571;954;955;956;1027;1035;1015;1017;1057;1059;1197;1199;1200;1203;1255;1256;1257;溶解方向+开洞;1,1,1,1;0;0
Node;AmplifyShaderEditor.GetLocalVarNode;71;-1314.833,-2132.345;Inherit;False;70;noise;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1831;2391.383,6458.775;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1500;4266.208,7053.174;Inherit;False;Property;_Custom9;自定义溶解软硬;186;1;[Enum];Create;False;0;8;custom1x;0;custom1y;1;custom1z;2;custom1w;3;custom2x;4;custom2y;5;custom2z;6;custom2w;7;0;True;1;SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w);False;4;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1829;2315.998,6933.889;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1830;2295.37,7217.87;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;3;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;1100;-1425.389,-2325.209;Inherit;False;1097;parallax;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;827;-5865.49,-5417.849;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;735;-4433.9,-1063.7;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0.1;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;836;-6430.828,-4089.915;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RotatorNode;386;-3405.465,-1421.732;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;474;-6092.26,-4945.602;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;1101;-1563.714,-2246.726;Inherit;False;Property;_Float143;开启视差映射(mesh模式下使用）;168;1;[Enum];Create;False;0;2;off;0;on;1;0;False;1;KWEnum(g15,off,_0,on,_1);False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;826;-6298.804,-5417.229;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;486;-6540,-3344.619;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;494;-5987.206,-3319.945;Inherit;False;Property;_Float52;遮罩02极坐标（竖向贴图）;69;0;Create;False;0;0;0;False;1;SubToggle(g2, _);False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;353;-1096.038,-2121.206;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;418;-3198.437,915.9819;Inherit;False;0;1257;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ScaleAndOffsetNode;496;-5934.142,-3907.305;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;1,1;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;318;-992.1891,-1647.137;Inherit;False;316;flpwmap_custom_switch;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1835;2512.777,7214.293;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;1015;-2407.97,1178.731;Inherit;False;Property;_Vector35;开洞坐标;94;0;Create;False;0;0;0;False;1;Sub(g3);False;0,0,0,0;-0.15,1.8,-0.41,0.4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;1099;-1063.338,-2335.242;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;313;-1017.744,-1870.048;Inherit;False;311;flowmap_intensity;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1832;2555.962,7500.686;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;4;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;100;-1229.3,-1804.712;Inherit;False;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;1869;4279.816,5904.018;Inherit;False;1;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.BreakToComponentsNode;635;-3323.935,-1293.86;Inherit;False;FLOAT2;1;0;FLOAT2;0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1870;4671.152,6548.908;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1868;4620.49,6255.641;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;954;-3172.57,1169.743;Inherit;False;930;uv2;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;956;-3198.492,1051.38;Inherit;False;1;1257;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;491;-6054.449,-3572.763;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;469;-5733.47,-5008.931;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;736;-4229.1,-1125.2;Inherit;False;noise_intensity_mask;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;468;-5619.626,-4864.986;Inherit;False;Property;_Float51;遮罩01极坐标（竖向贴图）;61;0;Create;False;0;0;0;False;1;SubToggle(g2, _);False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1834;2044.866,8047.413;Inherit;False;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ScaleAndOffsetNode;472;-5622.681,-5444.526;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;1,1;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;1833;2582.707,6790.011;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TransformPositionNode;1197;-2250.743,1376.547;Inherit;False;Object;World;False;Fast;True;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.FunctionNode;571;-2842.621,842.5984;Inherit;False;Polar Coordinates;-1;;165;7dab8e02884cf104ebefaa2e788e4162;0;4;1;FLOAT2;0,0;False;2;FLOAT2;0.5,0.5;False;3;FLOAT;1;False;4;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;633;-3116.146,-1221.938;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;785;-3257.257,-1093.851;Inherit;False;Property;_Float87;溶解贴图y轴Clamp;77;0;Create;False;0;0;0;True;1;SubToggle(g3, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1836;2882.065,7074.521;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;485;-6531.846,-3217.787;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;284;-992.467,-2010.128;Inherit;False;285;flowmap;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode;730;-8714.083,633.6453;Inherit;False;3930.691;2494.765;顶点偏移;85;706;544;707;173;710;546;551;711;712;549;714;555;716;715;554;552;728;718;719;556;557;396;168;729;397;725;398;726;644;720;721;722;645;646;649;647;723;727;178;167;179;705;169;176;175;172;171;181;787;788;789;790;791;792;881;882;883;884;885;886;887;888;889;890;891;892;893;894;895;896;897;898;899;921;922;945;946;947;948;949;950;990;993;994;1554;顶点偏移;1,1,1,1;0;0
Node;AmplifyShaderEditor.DynamicAppendNode;1017;-2159.614,1206.869;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1871;4740.746,6693.232;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;415;-2794.843,1480.791;Inherit;False;Property;_Float47;溶解方向旋转;83;0;Create;False;0;0;0;False;1;Sub(g3);False;0;0;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;467;-5403.192,-5152.456;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;632;-3139.666,-1342.1;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;321;-5456.687,-3892.311;Inherit;False;70;noise;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;480;-6304,-4736;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;1872;4829.677,5962.351;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;-3.28;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;955;-2926.77,1032.643;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;354;-848.9753,-2201.414;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1838;2850.253,7763.955;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1873;4865.766,6446.871;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;99;-697.2449,-1831.5;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;380;-1023.201,-2469.299;Inherit;False;Property;_Float39;主贴图旋转;38;0;Create;False;0;0;0;False;1;Sub(g1);False;0;0;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1837;2773.369,7497.111;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;634;-3115.571,-1462.119;Inherit;False;Property;_Float66;溶解贴图x轴Clamp;76;0;Create;False;0;0;0;True;1;SubToggle(g3, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;323;-5520.148,-3768.018;Inherit;False;736;noise_intensity_mask;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;495;-5651.359,-3604.686;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;1841;3142.657,7357.337;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1874;4986.877,6693.764;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;892;-8684.934,2006.185;Inherit;False;Constant;_Float123;Float 123;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;893;-8706.43,2097.995;Inherit;False;Constant;_Float124;Float 124;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;786;-2902.257,-1167.851;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;79;-5052.572,-5043.696;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1875;4966.249,6977.745;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;3;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1876;5062.262,6218.649;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;784;-2908.75,-1385.345;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;891;-8697.299,1927.639;Inherit;False;Constant;_Float122;Float 122;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;390;-5236.398,-4824.823;Inherit;False;Property;_Float43;遮罩01旋转;62;0;Create;False;0;0;0;False;1;Sub(g2);False;0;0;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1840;3014.106,8033.863;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;6;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;1203;-1999.555,1302.004;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.PiNode;378;-692.8785,-2441.443;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;216;-5287.117,-3556.391;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PiNode;416;-2509.583,1483.961;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;555;-8581.451,2680.061;Inherit;False;Property;_Vector19;顶点偏移流动&斜切;128;0;Create;False;0;0;0;False;1;Sub(g5);False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;568;-2507.247,913.2547;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;890;-8707.965,1870.067;Inherit;False;Constant;_Float121;Float 121;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;283;-507.6321,-2131.392;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;387;-5172.797,-3638.586;Inherit;False;Property;_Float42;遮罩02旋转;70;0;Create;False;0;0;0;False;1;Sub(g2);False;0;0;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1839;3067.66,7760.38;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1200;-2103.651,1511.521;Inherit;False;Property;_Float155;local/world;93;1;[Enum];Create;False;0;2;local;0;world;1;0;True;1;KWEnum(g3,local,_0,world,_1);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;322;-5180.346,-3817.024;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;173;-8633.986,1655.241;Inherit;False;0;169;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;320;-4856.229,-3799.028;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;1199;-1789.654,1239.106;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ClampOpNode;1843;3231.514,8030.287;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;895;-8449.609,1893.482;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;1624;-2593.523,-2057.003;Inherit;False;Property;_maintex_alpha1;溶解图通道;75;1;[Enum];Create;False;0;4;A;0;R;1;G;2;B;3;0;False;1;KWEnum(g3,A,_0,R,_1,G,_2,B,_3);False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;948;-8467.366,1845.149;Inherit;False;930;uv2;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1879;4693.404,7735.797;Inherit;False;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RotatorNode;417;-2124.709,910.199;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;1880;5183.656,6974.167;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.WorldPosInputsNode;1027;-1976.355,1604.836;Inherit;True;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleAddOpNode;319;-5024.052,-4601.649;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector3Node;894;-8706.748,2178.215;Inherit;False;Constant;_Vector29;Vector 29;138;0;Create;True;0;0;0;False;0;False;0,0,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.LerpOp;1842;3436.948,7620.607;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1878;5253.586,6549.886;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RotatorNode;377;-440.2984,-2471.626;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;950;-8627.366,1765.149;Inherit;False;1;169;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;897;-8431.951,1999.968;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DynamicAppendNode;631;-2754.53,-1272.834;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PiNode;391;-4952.867,-4744.793;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PiNode;388;-5348.449,-3642.73;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1877;5226.841,7260.561;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;4;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;2002;-2442.888,-1877.516;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;881;-8689.632,868.4754;Inherit;False;Constant;_Float117;Float 117;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DistanceOpNode;1035;-1587.19,1244.4;Inherit;True;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1881;5444.248,7256.985;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;1257;-1639.416,830.1862;Inherit;True;Property;_TextureSample1;溶解方向贴图（渐变）;82;0;Create;False;0;0;0;False;1;Ramp(g3);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;1883;5552.944,6834.396;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1882;5521.132,7523.83;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.RotatorNode;389;-4786.38,-3667.767;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;23;-2625.563,-1231.509;Inherit;True;Property;_dissolvetex;溶解贴图;74;0;Create;False;0;0;0;False;1;Sub(g3);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;1059;-1550.654,1576.436;Inherit;False;Property;_Float139;开洞（开启后方向失效）;92;0;Create;False;0;0;0;True;1;SubToggle(g3, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;949;-8275.366,1733.149;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector4Node;716;-8466.573,1474.328;Inherit;False;Property;_Vector25;顶点偏移遮罩流动＆斜切;136;0;Create;False;0;0;0;False;1;Sub(g5);False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;1844;3600.802,7890.515;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;882;-8678.968,926.0474;Inherit;False;Constant;_Float118;Float 118;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RotatorNode;392;-4771.551,-4688.157;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector4Node;544;-8685.137,2405.465;Inherit;False;Property;_vertextex_ST;_vertextex_ST;127;1;[HideInInspector];Create;True;0;0;0;False;0;False;1,1,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.MatrixFromVectors;896;-8274.712,2091.249;Inherit;False;FLOAT3x3;True;4;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3x3;0
Node;AmplifyShaderEditor.BreakToComponentsNode;603;-285.3804,-2352.52;Inherit;False;FLOAT2;1;0;FLOAT2;0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.RangedFloatNode;883;-8666.603,1004.593;Inherit;False;Constant;_Float119;Float 119;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;884;-8688.097,1096.403;Inherit;False;Constant;_Float120;Float 120;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1884;5813.536,7117.211;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;605;15.1352,-2200.634;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;888;-8413.619,998.3764;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;918;-2806.76,-1519.165;Inherit;False;Property;_Float130;混合溶解强度;84;0;Create;False;0;0;0;False;1;Sub(g3);False;0;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1537;6610.599,6945.145;Inherit;False;Property;_Custom11;自定义顶点偏移强度;196;1;[Enum];Create;False;0;8;custom1x;0;custom1y;1;custom1z;2;custom1w;3;custom2x;4;custom2y;5;custom2z;6;custom2w;7;0;True;1;SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w);False;4;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1886;5738.539,7520.254;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;763;-214.7163,-2119.484;Inherit;False;Property;_Float71;主贴图y轴clamp;36;0;Create;False;0;0;0;True;1;SubToggle(g1, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;898;-7913.26,2041.444;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;707;-8518.483,633.5099;Inherit;False;0;705;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.FunctionNode;549;-7961.628,1766.015;Inherit;False;Polar Coordinates;-1;;166;7dab8e02884cf104ebefaa2e788e4162;0;4;1;FLOAT2;0,0;False;2;FLOAT2;0.5,0.5;False;3;FLOAT;1;False;4;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;604;58.46643,-2350.786;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;2004;-2279.3,-1860.689;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;706;-8520.824,1235.588;Inherit;False;Property;_vertextex1_ST;_vertextex1_ST;135;1;[HideInInspector];Create;True;0;0;0;False;0;False;1,1,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.BreakToComponentsNode;623;-5246.948,-3303.893;Inherit;False;FLOAT2;1;0;FLOAT2;0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.DynamicAppendNode;886;-8391.942,873.0101;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;1057;-1285.17,1063.989;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;945;-8362.157,842.2756;Inherit;False;930;uv2;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector3Node;885;-8688.416,1176.623;Inherit;False;Constant;_Vector28;Vector 28;138;0;Create;True;0;0;0;False;0;False;0,0,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleSubtractOpNode;2003;-2428.929,-1728.418;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;608;-304.5597,-2581.715;Inherit;False;Property;_Float62;主贴图x轴clamp;35;0;Create;False;0;0;0;True;1;SubToggle(g1, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;947;-8522.157,762.2756;Inherit;False;1;705;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;1466;3962.463,7849.291;Inherit;False;custom7;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1885;5684.985,7793.737;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;6;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;617;-5164.772,-4251.176;Inherit;False;FLOAT2;1;0;FLOAT2;0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.LerpOp;1623;-2299.956,-2044.482;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;146;-3141.72,3012.516;Inherit;False;1475.065;723.4756;菲尼尔;15;135;137;138;139;140;141;142;144;147;136;352;351;2108;2109;2110;菲尼尔;1,1,1,1;0;0
Node;AmplifyShaderEditor.ClampOpNode;1914;6977.069,6273.821;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;624;-5062.679,-3354.133;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;1469;-3135.603,-909.5148;Inherit;False;1466;custom7;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;764;199.2137,-2219.356;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;2006;-2115.006,-1876.895;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;546;-8207.091,2615.418;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode;712;-7832.597,680.8583;Inherit;False;Polar Coordinates;-1;;167;7dab8e02884cf104ebefaa2e788e4162;0;4;1;FLOAT2;0,0;False;2;FLOAT2;0.5,0.5;False;3;FLOAT;1;False;4;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;625;-5067.159,-3221.971;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;774;-4936.102,-3140.36;Inherit;False;Property;_Float83;遮罩02y轴Clamp;68;0;Create;False;0;0;0;True;1;SubToggle(g2, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;912;-3044.244,-791.8413;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ClampOpNode;619;-4984.984,-4169.254;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;626;-5040,-3504;Inherit;False;Property;_Float65;遮罩02x轴Clamp;67;0;Create;False;0;0;0;True;1;SubToggle(g2, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1888;6107.827,7380.481;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;900;-7642.331,1881.517;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;607;206.037,-2481.101;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1915;6804.942,5938.25;Inherit;False;1;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;277;-1017.08,968.086;Inherit;False;dis_direction;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;620;-5005.937,-4399.004;Inherit;False;Property;_Float64;遮罩01x轴Clamp;59;0;Create;False;0;0;0;True;1;SubToggle(g2, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1887;5902.393,7790.162;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;946;-8170.157,730.2756;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;61;-3290.452,-765.5207;Inherit;False;Property;_Float11;custom控制溶解;182;0;Create;False;0;0;0;False;3;SubToggle(g7, _);Space;Space;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;1062;-2611.686,-1412.892;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;618;-4980.503,-4301.416;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;770;-4993.379,-4019.428;Inherit;False;Property;_Float82;遮罩01y轴Clamp;60;0;Create;False;0;0;0;True;1;SubToggle(g2, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1916;7027.73,6567.088;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;2005;-2202.692,-1726.781;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;29;-3044.951,-1005.381;Inherit;False;Property;_Float6;溶解;85;0;Create;False;0;0;0;False;1;Sub(g3);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.MatrixFromVectors;887;-8223.322,956.2864;Inherit;False;FLOAT3x3;True;4;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3x3;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;554;-7599.292,2049.103;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;1,1;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;710;-7982.148,1414.472;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;552;-7757.952,2678.026;Inherit;False;Property;_Float56;顶点偏移极坐标（竖向贴图）;125;0;Create;False;0;0;0;False;1;SubToggle(g5, _);False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1600;9928.541,7514.763;Inherit;False;Property;_Custom13;自定义alphaclip溶解;188;1;[Enum];Create;False;0;8;custom1x;0;custom1y;1;custom1z;2;custom1w;3;custom2x;4;custom2y;5;custom2z;6;custom2w;7;0;True;1;SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w);False;4;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;606;359.7148,-2367.942;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;889;-7978.904,838.1533;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;771;-4764.663,-4077.637;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;899;-7603.787,803.4982;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;2007;-1848.658,-1718.061;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1060;-2421.996,-1513.499;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1917;7097.325,6711.412;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;62;-2740.221,-984.6865;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1919;7222.345,6465.051;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;915;-2758.661,-830.8226;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;758;-7016.301,-395.5196;Inherit;False;1519.266;836.9044;软粒子;15;96;98;327;333;334;337;97;126;752;756;753;757;336;754;755;软粒子;1,1,1,1;0;0
Node;AmplifyShaderEditor.LerpOp;1889;6271.681,7650.39;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FaceVariableNode;352;-3012.43,3185.981;Inherit;False;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;551;-7817.1,2444.744;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;914;-2806.012,-689.4841;Inherit;False;Property;_Float129;粒子alpha控制溶解（溶解拖尾使用）;184;1;[Enum];Create;False;0;2;off;0;20;1;0;False;1;KWEnum(g7,off,_0,on,_1);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;773;-4737.102,-3251.36;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;403;418.2101,-1918.378;Inherit;False;Property;_maintex_alpha;主贴图通道;34;1;[Enum];Create;False;0;4;A;0;R;1;G;2;B;3;0;False;1;KWEnum(g1,A,_0,R,_1,G,_2,B,_3);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.WorldNormalVector;136;-3118.165,3055.876;Inherit;False;True;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.GetLocalVarNode;281;-2603.653,-1327.848;Inherit;False;277;dis_direction;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1918;7186.255,5980.531;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;-3.28;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;772;-4800,-3424;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;769;-4761.483,-4387.489;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1975;10037.18,6507.798;Inherit;False;1;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;408;-4551.815,-3248.005;Inherit;False;Property;_mask02_alpha;遮罩02通道;66;1;[Enum];Create;False;0;4;A;0;R;1;G;2;B;3;0;False;1;KWEnum(g2,A,_0,R,_1,G,_2,B,_3);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;25;-1966.678,-1141.035;Inherit;False;Constant;_Float3;Float 3;11;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;916;-2072.991,-1299.599;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1984;685.0712,-2009.248;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;96;-6851.671,253.7125;Inherit;False;Property;_Float16;软粒子（羽化边缘）;18;0;Create;False;0;0;0;False;1;Sub(g9);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;1511;6531.986,7664.06;Inherit;False;custom9;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;913;-2478.706,-938.4984;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;405;-4637.969,-3928.128;Inherit;False;Property;_mask01_alpha;遮罩01通道;58;1;[Enum];Create;False;0;4;A;0;R;1;G;2;B;3;0;False;1;KWEnum(g2,A,_0,R,_1,G,_2,B,_3);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1960;10259.97,7136.636;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;333;-6745.693,325.3848;Inherit;False;Property;_Float5;反向软粒子(强化边缘）;19;0;Create;False;0;0;0;True;1;SubToggle(g9, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;31;-2115.32,-914.2172;Inherit;False;Constant;_Float7;Float 7;11;0;Create;True;0;0;0;False;0;False;2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;714;-7577.378,1355.553;Inherit;False;Property;_Float75;顶点偏移遮罩极坐标;133;0;Create;False;0;0;0;False;1;SubToggle(g5, _);False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1959;10209.31,6843.369;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PosVertexDataNode;98;-6836.29,91.33076;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;557;-8191.453,2741.061;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;396;-7422.272,2480.826;Inherit;False;Property;_Float45;顶点贴图旋转;126;0;Create;False;0;0;0;False;1;Sub(g5);False;0;0;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;711;-7635.226,1120.972;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;616;-4591.438,-4366.689;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;138;-3076.711,3571.987;Inherit;False;Property;_Float19;菲尼尔软硬;29;0;Create;False;0;0;0;False;1;Sub(g8);False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;135;-3045.824,3461.325;Inherit;False;Property;_power3;菲尼尔范围;27;0;Create;False;0;0;0;False;1;Sub(g8);False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;715;-7421.063,733.3063;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;1,1;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;1921;7343.456,6711.944;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;1;473.1632,-2257.324;Inherit;True;Property;_maintex;主贴图;33;0;Create;False;0;0;0;False;1;Sub(g1);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;137;-3052.719,3269.034;Inherit;False;World;True;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.LerpOp;1920;7418.841,6236.829;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;351;-2795.43,3142.981;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1922;7322.828,6995.925;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;3;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;622;-4619.498,-3376.833;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;556;-7324.799,2186.685;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;1925;7540.234,6992.347;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1091;-2005.488,-651.1614;Inherit;False;Property;_Float142;custom控制溶解软硬;183;0;Create;False;0;0;0;True;1;SubToggle(g7, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FresnelNode;139;-2742.485,3324.646;Inherit;False;Standard;WorldNormal;ViewDir;True;True;5;0;FLOAT3;0,0,1;False;4;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;8;-4405.445,-4388.446;Inherit;True;Property;_Mask;遮罩01;57;0;Create;False;0;0;0;False;1;Sub(g2);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;33;-2228.09,-821.8804;Inherit;False;Property;_Float8;软硬;86;0;Create;False;0;0;0;False;1;Sub(g3);False;0.5;1;0;0.5;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;30;-1758.533,-962.4669;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;168;-7070.666,2215.753;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PiNode;397;-7252.301,2544.294;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1999;-4325.188,-3273.501;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1985;836.6595,-2035.42;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;1512;-2049.588,-732.6893;Inherit;False;1511;custom9;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;719;-7945.774,1552.875;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;334;-6483.376,320.6759;Inherit;False;depthfade_switch;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PosVertexDataNode;752;-6964.098,-345.5196;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;718;-7409.397,1203.054;Inherit;False;Property;_Float77;顶点遮罩旋转;134;0;Create;False;0;0;0;False;1;Sub(g5);False;0;0;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;24;-1851.546,-1230.554;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;661;-1609.694,-1468.531;Inherit;False;dis_tex;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1926;7049.982,7753.977;Inherit;False;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1986;730.0305,-1910.149;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;728;-7142.924,862.9124;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1992;-4374.318,-4031.236;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1963;10454.58,7034.599;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;114;-3227.833,-169.203;Inherit;False;1936.036;770.2162; 亮边溶解;16;107;109;105;106;124;130;133;1053;1238;1239;1240;1241;1263;1264;1266;1267;亮边溶解;1,1,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;753;-6951.867,-80.19434;Inherit;False;Property;_Float0;相机软粒子（贴脸羽化）位置;23;0;Create;False;0;0;0;False;1;Sub(g9);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1924;7610.165,6568.066;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;218;-4446.376,-3619.329;Inherit;True;Property;_Mask1;遮罩02;65;0;Create;False;0;0;0;False;1;Sub(g2);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DepthFade;97;-6604.836,90.75565;Inherit;False;True;True;True;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;756;-6966.301,-175.2479;Inherit;False;Property;_Float55;相机软粒子（贴脸羽化）距离;22;0;Create;False;0;0;0;False;1;Sub(g9);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1962;10418.49,6550.079;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;-3.28;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1961;10329.56,7280.96;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;402;855.8163,-2231.02;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1923;7583.419,7278.741;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;4;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1964;10651.08,6806.377;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;140;-2475.707,3323.377;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;327;-6383.158,172.6414;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;725;-6901.163,887.341;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;1983;991.5583,-2112.194;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;337;-6259.045,305.1179;Inherit;False;334;depthfade_switch;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1090;-1835.93,-803.7596;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1991;-4329.359,-3932.138;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1987;913.2662,-1901.513;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1929;7823.911,6831.173;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1993;-4196.73,-4073.409;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;26;-1693.398,-1246.431;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RotatorNode;398;-6992.47,2384.011;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;2000;-4173.601,-3299.673;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1927;7877.71,7542.01;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;1053;-2893.244,-45.45695;Inherit;False;661;dis_tex;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;407;-4052.96,-3555.235;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1966;10555.07,7565.473;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;3;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;406;-4041.868,-4304.307;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1928;7800.827,7275.165;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1965;10575.7,7281.492;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.CameraDepthFade;754;-6607.409,-272.9024;Inherit;False;3;2;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1998;-4280.229,-3174.402;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.PiNode;729;-7070.427,1220.521;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;45;-1529.516,-1159.214;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1994;-3887.757,-4120.158;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;755;-6313.52,-267.9716;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;2001;-3917.092,-3356.915;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1968;10842.4,7137.614;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode;130;-2553.58,-13.45383;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;-0.5;False;2;FLOAT;1.5;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;34;-1637.713,-791.3764;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;644;-7470.53,2730.82;Inherit;False;FLOAT2;1;0;FLOAT2;0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.LerpOp;1988;1129.49,-1997.621;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1997;-4096.994,-3165.765;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1970;10282.22,8323.525;Inherit;False;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ClampOpNode;1990;-4120.122,-3939.501;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;141;-2458.977,3437.984;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;336;-6100.238,75.26977;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1932;7931.875,7811.917;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;6;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;142;-2560.026,3559.448;Inherit;False;Property;_Float20;反向菲尼尔（虚化边缘）;30;0;Create;False;0;0;0;True;1;SubToggle(g8, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1931;8095.118,7538.434;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1969;10772.47,7561.896;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RotatorNode;726;-6810.596,1060.239;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1967;10815.66,7848.29;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;4;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1930;8006.917,7146.092;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1996;-3823.229,-3200.152;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1972;11033.07,7844.713;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;646;-7236.695,2773.42;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.AbsOpNode;1195;1255.768,-1801.208;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;757;-5952.993,-148.1963;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;32;-1538.659,-954.8051;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.StepOpNode;105;-2164.216,-46.14251;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;792;-7124.727,3010.527;Inherit;False;Property;_Float89;顶点偏移贴图y轴Clamp;124;0;Create;False;0;0;0;True;1;SubToggle(g5, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1989;-3846.357,-3973.888;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;720;-7275.088,1513.887;Inherit;False;FLOAT2;1;0;FLOAT2;0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.RangedFloatNode;649;-7337.818,2667.41;Inherit;False;Property;_Float68;顶点偏移贴图x轴Clamp;123;0;Create;False;0;0;0;True;1;SubToggle(g5, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;365;700.4532,-1792.539;Inherit;False;Property;_Float34;主贴图细节对比度;41;0;Create;False;0;0;0;False;1;Sub(g1);False;1;4.74;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;144;-2259.633,3341.743;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;645;-7277.174,2921.583;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1933;8258.972,7808.342;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1976;11056.15,7400.721;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1971;11109.95,8111.558;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1934;8298.533,7390.635;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;126;-5721.036,65.64495;Inherit;False;depthfade;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1935;8491.816,7671.245;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;124;-1655.204,-111.5786;Inherit;False;dis_bright;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1977;11239.16,7715.64;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;727;-6902.836,1372.223;Inherit;False;Property;_Float78;顶点偏移遮罩x轴Clamp;131;0;Create;False;0;0;0;True;1;SubToggle(g5, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;791;-6944.545,2871.763;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;722;-6966.713,1728.636;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;790;-7037.636,2635.646;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;721;-6959.221,1543.474;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1980;11164.11,8381.465;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;6;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;372;1394.108,-1728.11;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;788;-6926.462,1866.367;Inherit;False;Property;_Float88;顶点偏移遮罩y轴Clamp;132;0;Create;False;0;0;0;True;1;SubToggle(g5, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;220;-3928.95,-3804.159;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1973;11327.36,8107.982;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;370;707.0606,-1734.717;Inherit;False;Property;_Float37;主贴图细节提亮;42;0;Create;False;0;0;0;False;1;Sub(g1);False;1;6.18;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;122;-1366.799,-1143.595;Inherit;False;dis_soft;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;147;-2063.799,3307.87;Inherit;False;fresnel;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;647;-6801.513,2590.72;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;1553;8762.682,7725.67;Inherit;False;custom11;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;414;1022.056,99.78564;Inherit;False;Property;_Float25;亮边溶解（默认关闭，勾上开启）;87;0;Create;False;0;0;0;True;1;SubToggle(g3, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;128;2832.193,-16.70406;Inherit;False;126;depthfade;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;123;1162.871,-150.069;Inherit;False;122;dis_soft;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1978;11530.77,7960.183;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1974;11491.21,8377.891;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;789;-6633.962,1793.554;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;340;2800.193,191.2959;Inherit;False;334;depthfade_switch;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;367;683.2443,-1664.549;Inherit;False;Property;_Float36;细节平滑过渡;43;0;Create;False;0;0;0;False;1;Sub(g1);False;1;0.903;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;373;1555.251,-1664.675;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;82;-3669.433,-3754.208;Inherit;False;Mask;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;209;2959.577,-756.0779;Inherit;False;Constant;_Float27;Float 27;43;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;350;2949.694,-422.6489;Inherit;False;Property;_Float33;单独菲尼尔开关;25;1;[Enum];Create;False;0;2;off;0;on;1;0;False;1;KWEnum(g8,off,_0,on,_1);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;150;2947.484,-564.2902;Inherit;False;147;fresnel;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;787;-6626.899,1464.961;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;125;1170.851,-66.47853;Inherit;False;124;dis_bright;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;338;3216.194,15.29595;Inherit;False;3;0;FLOAT;1;False;1;FLOAT;1;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;994;-6053.791,2340.632;Inherit;False;Constant;_Float136;Float 136;151;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;178;-6306.146,2674.208;Inherit;False;Constant;_Float21;Float 21;37;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;179;-6505.771,3000.662;Inherit;False;Property;_Float22;custom控制顶点偏移强度;195;0;Create;False;0;0;0;False;3;SubToggle(g7, _);Space;Space;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;374;1734.453,-1636.737;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;990;-6065.756,2457.289;Inherit;False;Property;_Float135;顶点法线;120;1;[Enum];Create;False;0;2;off;0;on;1;0;True;1;KWEnum(g5,off,_0,on,_1);False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;221;3264.194,159.2959;Inherit;False;82;Mask;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;347;3292.271,-677.6849;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;169;-6513.891,2001.706;Inherit;True;Property;_vertextex;顶点偏移贴图;121;0;Create;False;0;0;0;False;1;Sub(g5);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;723;-6464.121,1596.634;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector4Node;922;-6311.267,2194.702;Inherit;False;Property;_Vector32;顶点偏移贴图remap;122;0;Create;False;0;0;0;False;1;Sub(g5);False;0,1,0,1;0,1,0,1;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;413;1502.056,-108.2144;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;4;1086.056,-860.2144;Inherit;False;Property;_Color0;颜色;11;1;[HDR];Create;False;0;0;0;False;1;Sub(g10);False;1,1,1,1;0,0.6419505,2.270603,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.VertexColorNode;3;1246.056,-1116.214;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;1979;11724.05,8240.793;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.NormalVertexDataNode;172;-6107.594,2204.864;Inherit;False;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;95;3296.194,-80.70406;Inherit;False;Property;_Float15;alpha强度;15;0;Create;False;0;0;0;False;1;Sub(g10);False;1;10;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;1554;-6224.732,2790.292;Inherit;False;1553;custom11;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;176;-5977.17,2740.609;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode;921;-5983.323,2018.482;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;6;3936.194,-336.704;Inherit;False;8;8;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;705;-6324.812,1746.057;Inherit;True;Property;_vertextex1;顶点偏移遮罩;130;0;Create;False;0;0;0;False;1;Sub(g5);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;993;-5799.169,2186.453;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Vector3Node;175;-6037.788,2568.436;Inherit;False;Property;_Vector5;顶点偏移xyz强度;129;0;Create;False;0;0;0;False;1;Sub(g5);False;0,0,0;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RegisterLocalVarNode;1619;12232.91,8351.622;Inherit;False;custom13;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;657;5503.037,-138.1591;Inherit;False;Property;_Float70;限制alpha值为0-1;16;0;Create;False;0;0;0;True;1;SubToggle(g10, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1622;5727.038,165.8409;Inherit;False;Property;_Float151;custom控制alphaclip;187;0;Create;False;0;0;0;True;3;SubToggle(g7, _);Space;Space;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;1620;5711.038,37.84088;Inherit;False;1619;custom13;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;171;-5495.304,1970.702;Inherit;False;5;5;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;673;5647.038,-58.15916;Inherit;False;Property;_Float72;alphaclip溶解（层级2000以下使用）;95;0;Create;False;0;0;0;False;1;Sub(g3);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;602;5535.038,-330.1591;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;656;5759.038,-410.1591;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1621;6015.038,-58.15916;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;672;5759.038,-202.1591;Inherit;False;661;dis_tex;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;588;-3244.336,1907.054;Inherit;False;3124.205;1004.605;Comment;45;194;193;201;188;191;190;202;185;183;196;576;580;582;584;585;579;186;195;192;575;581;586;589;590;591;650;651;652;653;655;781;782;783;901;902;903;904;905;906;907;908;909;910;1533;2142;折射;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;1205;3072.943,-6560.758;Inherit;False;3294.75;979.6411;matcap&cubemap;23;1064;1065;1066;1067;1068;1069;1063;1073;1072;1074;1209;1210;1578;1579;1580;1583;1584;1585;1587;1591;1593;1599;1597;matcap&cubemap;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;856;-3364.765,-3725.929;Inherit;False;2282.703;1348.265;ramptex;30;849;848;847;846;850;852;851;853;459;226;460;456;457;854;455;454;855;458;463;461;453;464;451;229;231;1261;2072;2090;2091;2092;ramptex;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;181;-5049.569,1738.24;Inherit;False;vertexoffset;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode;1206;-618.7476,-6700.138;Inherit;False;2870.985;1564.313;阴影;39;1169;1171;1170;1180;1168;1173;1174;1172;1179;1175;1176;1178;1177;995;996;1002;1149;1001;1147;957;958;1011;960;1009;959;1008;963;962;961;965;964;968;1000;997;1013;1012;1007;971;1598;阴影;1,1,1,1;0;0
Node;AmplifyShaderEditor.DynamicAppendNode;7;6145.214,-1083.626;Inherit;False;FLOAT4;4;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.DesaturateOpNode;2130;1340.74,-2423.125;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;330;1813.327,-2508.759;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;2042;1642.331,-2110.043;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1269;-1325.649,-1023.673;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;1578;4536.337,-5888.917;Inherit;False;World;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;904;-3186.555,2268.514;Inherit;False;Constant;_Float128;Float 128;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;346;1788.676,-2335.679;Inherit;False;Property;_Float30;边缘收窄;21;0;Create;False;0;0;0;False;1;Sub(g9);False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;39;-3028.452,-4419.989;Inherit;True;1;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleSubtractOpNode;2125;2476.66,-1653.366;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;3;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;1270;-1152.271,-895.2566;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;1243;3016.217,-1111.807;Inherit;False;1241;dis_edge2;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1893;5065.239,9003.798;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;2080;2903.75,-1419.471;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;595;-8082.98,-1300.832;Inherit;False;Property;_Ztestmode1;stencil_comp;6;0;Create;False;0;0;1;UnityEngine.Rendering.CompareFunction;True;1;SubEnum(g17,UnityEngine.Rendering.CompareFunction);False;0;4;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;2122;3255.242,-2625.627;Inherit;True;Property;_TextureSample2;Texture Sample 2;48;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Instance;212;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;2136;2736.835,-2056.055;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;1169;-435.7715,-6650.138;Inherit;False;Constant;_Float148;Float 148;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;341;2063.196,-2607.802;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;74;-6707.094,-4562.272;Inherit;True;1;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ClampOpNode;610;75.59692,-1579.011;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1011;1186.795,-5775.913;Inherit;False;Property;_Float137;切换为假点光（默认为平行光）;157;0;Create;False;0;0;0;True;1;SubToggle(g12, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GrabScreenPosition;202;-998.173,2010.73;Inherit;False;0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;226;-3114.721,-3675.929;Inherit;False;0;212;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;2037;-328.3605,-1212.263;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;2092;-2379.526,-2462.415;Inherit;False;ramp_offset;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;1242;3444.389,-1351.924;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;1102;-8058.011,-2288.211;Inherit;False;Property;_Maintex_Group5;视差;167;0;Create;False;0;0;0;True;1;Main(g15, _, off, off);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1258;-8480.2,-2311.889;Inherit;False;Property;_Color_Group3;基础设置;0;0;Create;False;0;0;0;True;1;Main(g17, _, off, off);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;977;5207.052,-1342.874;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode;132;2665.591,-1199.48;Inherit;False;Property;_Color1;一层亮边颜色;90;1;[HDR];Create;False;0;0;0;False;1;Sub(g3);False;1,1,1,1;1,1,1,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PannerNode;188;-1789.108,2149.545;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;1256;-2547.024,1077.807;Inherit;False;Constant;_Vector18;Vector 18;179;0;Create;True;0;0;0;False;0;False;0.5,0.5;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.GetLocalVarNode;1136;1582.056,-508.2144;Inherit;False;147;fresnel;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;611;27.9866,-1420.893;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;963;1650.76,-6109.127;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;191;-1137.447,2633.402;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;901;-3188.09,2040.586;Inherit;False;Constant;_Float125;Float 125;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;426;-8391.188,-1231.882;Inherit;False;Property;_Fresnel_Group2;菲尼尔;24;0;Create;False;0;0;0;True;1;Main(g8, _, off, off);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StepOpNode;107;-2215.584,180.9771;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;359;1358.056,-764.2144;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;1081;-8079.145,-1853.214;Inherit;False;Property;_Maintex_Group3;Matcap&Cubemap;160;0;Create;False;0;0;0;True;1;Main(g13, _, off, off);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;597;-8087.689,-1123.75;Inherit;False;Property;_Float46;stencil_reference;8;0;Create;False;0;0;0;True;1;Sub(g17);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;584;-2171.536,1998.739;Inherit;False;3;0;FLOAT2;0,1;False;1;FLOAT2;1,1;False;2;FLOAT2;0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;768;359.3894,-1581.854;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.WorldNormalVector;957;1123.602,-6086.494;Inherit;False;True;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.ColorNode;1244;2918.452,-1624.586;Inherit;False;Property;_Color7;二层亮边颜色（软边颜色）;91;1;[HDR];Create;False;0;0;0;False;1;Sub(g3);False;1,1,1,1;1,1,1,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;133;-1629.695,71.82189;Inherit;False;dis_edge;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WorldNormalVector;1064;4842.099,-6228.99;Inherit;False;False;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;959;1428.072,-5962.867;Inherit;False;Property;_Float132;阴影范围;154;0;Create;False;0;0;0;False;1;Sub(g12);False;0.5;0.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;457;-2516.021,-2777.428;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;1241;-1560.105,248.8564;Inherit;False;dis_edge2;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;969;4901.304,-1638.843;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.DynamicAppendNode;454;-2340.95,-2994.7;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PowerNode;2111;-1702.744,3398.674;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;358;1230.056,-364.2143;Inherit;False;Property;_Float35;双面颜色（默认关闭，勾上开启）;13;0;Create;False;0;0;0;True;1;SubToggle(g10, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;21;-8077.943,-1574.899;Inherit;False;Property;_Ztestmode;深度测试;4;0;Create;False;0;0;0;True;1;SubEnum(g17,UnityEngine.Rendering.CompareFunction);False;4;4;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;965;1831.671,-6068.604;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WorldSpaceLightDirHlpNode;958;1079.013,-6273.463;Inherit;False;False;1;0;FLOAT;0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.TextureCoordinatesNode;1089;-2270.936,-749.8066;Inherit;False;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;1088;-8062.813,-2179.41;Inherit;False;Property;_Maintex_Group4;Flowmap;116;0;Create;False;0;0;0;True;1;Main(g14, _, off, off);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;409;-8385.255,-1756.505;Inherit;False;Property;_Disolove_Group;溶解;73;0;Create;False;0;0;0;True;1;Main(g3, _, off, off);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;1077;4845.002,-1126.822;Inherit;False;1074;matcap;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;962;1487.493,-5805.321;Inherit;False;Property;_Float133;阴影软硬;153;0;Create;False;0;0;0;False;1;Sub(g12);False;0.5;0.5;0.5;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;185;-699.3252,2097.102;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;1069;5446.203,-6339.315;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;979;-8073.319,-1928.804;Inherit;False;Property;_Maintex_Group2;阴影;148;0;Create;False;0;0;0;True;1;Main(g12, _, off, off);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;1178;104.7235,-5916.098;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ComponentMaskNode;1067;5155.086,-6474.872;Inherit;False;True;True;False;True;1;0;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;1007;1960.999,-5554.02;Inherit;False;pointlight;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;598;-8081.473,-1473.006;Inherit;False;Property;_Float60;colormask;5;0;Create;False;0;0;1;UnityEngine.Rendering.ColorWriteMask;True;1;SubEnum(g17,UnityEngine.Rendering.ColorWriteMask);False;15;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;22;-8072.098,-1379.883;Inherit;False;Property;_Float2;双面模式;2;0;Create;False;0;0;1;UnityEngine.Rendering.CullMode;True;1;SubEnum(g17,UnityEngine.Rendering.CullMode);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ReflectOpNode;1580;5019.922,-5900.456;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;1135;1870.057,-908.2144;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;2135;2198.408,-1773.111;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;903;-3165.06,2176.704;Inherit;False;Constant;_Float127;Float 127;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1263;-2071.25,117.8709;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;357;1089.456,-593.4144;Inherit;False;Property;_Color3;颜色2;14;1;[HDR];Create;False;0;0;0;False;1;Sub(g10);False;1,1,1,1;1,1,1,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;2072;-2527.037,-2678.429;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;49;-3374.072,-964.5662;Inherit;False;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;1012;1339.792,-5535.062;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;428;-8434.165,-2171.328;Inherit;False;Property;_Color_Group2;颜色;10;0;Create;False;0;0;0;True;1;Main(g10, _, off, off);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;1207;5566.853,-1172.782;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;2083;2065.599,-1875.862;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;759;3920.194,-928.704;Inherit;False;3;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.DistanceOpNode;1001;1600.581,-5570.061;Inherit;True;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;1271;-1016.86,-789.223;Inherit;False;dis_soft_edge;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;585;-1942.312,2106.268;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;94;5578.911,-952.3163;Inherit;False;Property;_Float14;整体颜色强度;12;0;Create;False;0;0;0;False;1;Sub(g10);False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1149;454.9354,-5808.885;Inherit;False;Property;_Float146;法线强度;152;0;Create;False;0;0;0;False;1;Sub(g12);False;0;0.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;596;-8085.98,-1205.832;Inherit;False;Property;_Ztestmode2;stencil_pass;7;0;Create;False;0;0;1;UnityEngine.Rendering.StencilOp;True;1;SubEnum(g17,UnityEngine.Rendering.StencilOp);False;0;4;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1585;5312.365,-5670.556;Inherit;False;Constant;_Float259;Float 259;196;0;Create;True;0;0;0;False;0;False;0.1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;964;1842.893,-5865.257;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;1255;-2326.024,1016.807;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;1593;5942.591,-5880.852;Inherit;False;cubemap;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.Vector4Node;463;-3000.783,-2608.419;Inherit;False;Property;_Vector7;ramp图流动速度;53;0;Create;False;0;0;0;False;1;Sub(g11);False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;2127;3427.781,-2782.387;Inherit;False;Constant;_Float29;Float 29;197;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;186;-3139.38,1953.014;Inherit;False;0;190;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;199;3685.194,-939.704;Inherit;False;196;ref;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;2096;113.2145,-1161.548;Inherit;False;2091;ramp_tilling;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;5;3788.426,-1986.939;Inherit;False;5;5;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;FLOAT;0;False;4;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;1013;1058.646,-5391.43;Inherit;False;Property;_Float138;开启世界坐标;159;0;Create;False;0;0;0;False;1;SubToggle(g12, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;765;213.7417,-1737.048;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;419;-8390.636,-1666.76;Inherit;False;Property;_Noise_Group;扰动;96;0;Create;False;0;0;0;True;1;Main(g4, _, off, off);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;459;-3035.352,-2858.703;Inherit;False;Property;_Vector9;ramp图tilling&offset;52;0;Create;False;0;0;0;False;1;Sub(g11);False;1,1,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;1583;5733.783,-5847.451;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.Vector3Node;850;-3275.955,-2999.158;Inherit;False;Constant;_Vector6;Vector 6;138;0;Create;True;0;0;0;False;0;False;0,0,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.ClampOpNode;2084;2279.031,-1934.833;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;1177;442.0487,-6076.574;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.MatrixFromVectors;853;-2773.873,-3281.284;Inherit;False;FLOAT3x3;True;4;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3x3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;1238;-2496.109,360.1773;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;2095;1668.238,-1269.716;Inherit;False;2090;ramp_speed;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;2094;1479.238,-1323.716;Inherit;False;2092;ramp_offset;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PannerNode;2088;1894.431,-1401.9;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;2093;1390.238,-1399.716;Inherit;False;2091;ramp_tilling;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;2082;1391.877,-1495.053;Inherit;False;2059;ramp_distex;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;2144;-487.1955,-1317.885;Inherit;False;ramp_rotater;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;2145;2840.679,-2424.214;Inherit;False;2144;ramp_rotater;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;381;-994.8384,-1404.813;Inherit;False;Property;_Float40;ramp图旋转;51;0;Create;False;0;0;0;False;1;Sub(g11);False;0;0;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.PiNode;382;-713.3931,-1416.958;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;2043;1601.694,-1828.406;Inherit;False;Property;_Float154;ramp映射;54;1;[Enum];Create;False;0;5;Multiply;0;Maintex;1;Dissolove;2;Off;3;Fresnel;4;0;True;1;SubKeywordEnumDrawer(g11,Multiply,Maintex,Dissolove,Off,Fresnel);False;3;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;194;-1965.401,2529.374;Inherit;True;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;1078;5229.021,-1148.814;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;978;4893.002,-1318.822;Inherit;False;Property;_Float134;阴影开关;149;1;[Enum];Create;False;0;2;off;0;20;1;0;True;1;KWEnum(g12,off,_0,on,_1);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;2119;2529.017,-2458.233;Inherit;False;2090;ramp_speed;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;2117;2319.782,-2641.617;Inherit;False;2091;ramp_tilling;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;2118;2386.323,-2757.41;Inherit;False;2114;ramp_fre;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;329;2500.565,-2335.371;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;2114;-1222.599,3404.113;Inherit;False;ramp_fre;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TransformPositionNode;1000;614.6118,-5464.272;Inherit;False;Object;World;False;Fast;True;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.ClampOpNode;2124;2722.733,-1752.01;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1908;6226.886,10100.73;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;464;-2329.715,-2560.028;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;20;-8074.956,-1669.507;Inherit;False;Property;_Float4;深度写入;3;0;Create;False;0;0;1;UnityEngine.Rendering.CullMode;True;1;SubToggle(g17,_);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;422;4272.194,-1024.704;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;2099;3233.361,-1503.099;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;1597;6171.667,-5992.443;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.DynamicAppendNode;1172;-204.0067,-6601.43;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;425;-8395.455,-1341.458;Inherit;False;Property;_Custom_Group1;custom控制;172;0;Create;False;0;0;0;True;1;Main(g7, _, off, off);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;2116;2424.807,-2549.447;Inherit;False;2092;ramp_offset;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;972;4693.305,-1414.843;Inherit;False;971;lit;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1260;-7922.651,-1987.868;Inherit;False;Property;_Ramp;Ramp;46;0;Create;False;0;0;0;True;1;Main(g11, _, off, off);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;967;4707.438,-1670.781;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ClampOpNode;1907;6063.032,9830.819;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;1074;6188.272,-6149.159;Inherit;False;matcap;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;902;-3177.425,2098.158;Inherit;False;Constant;_Float126;Float 126;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;976;4229.305,-1542.843;Inherit;False;Property;_Color5;亮部颜色;155;1;[HDR];Create;False;0;0;0;False;1;Sub(g12);False;1,1,1,1;0.6132076,0.2690014,0.2690014,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;1584;5320.241,-5750.698;Inherit;False;Property;_Float258;cube强度;166;0;Create;False;0;0;0;False;1;Sub(g13);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;591;-1991.792,2336.677;Inherit;False;Property;_Float59;折射贴图旋转;143;0;Create;False;0;0;0;False;1;Sub(g6);False;1;0;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;760;3666.194,-787.704;Inherit;False;Property;_Color2;折射颜色;146;1;[HDR];Create;False;0;0;0;False;1;Sub(g6);False;1,1,1,1;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;2090;-2086.59,-2523.757;Inherit;False;ramp_speed;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;846;-3269.412,-3123.404;Inherit;False;Constant;_Float105;Float 105;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PiNode;590;-1685.244,2354.179;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;961;1602.372,-5949.066;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;404;-8387.01,-1846.811;Inherit;False;Property;_Mask_Group;遮罩;56;0;Create;False;0;0;0;True;1;Main(g2, _, off, off);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1179;-143.1956,-6105.099;Inherit;False;0;1147;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;973;4716.838,-1521.199;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;782;-1331.228,2109.378;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1208;4941.002,-1222.822;Inherit;False;Constant;_Float156;Float 156;175;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;1063;5579.651,-6394.174;Inherit;True;Property;_matcap;matcap;162;0;Create;False;0;0;0;False;1;Sub(g13);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;1272;3579.486,-1093.275;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;1229;375.127,-3025.969;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;1073;5581.222,-6075.475;Inherit;False;Property;_Float140;matcap强度;164;0;Create;False;0;0;0;False;1;Sub(g13);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1890;4944.983,8566.207;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1080;4941.002,-982.8218;Inherit;False;Property;_Float141;反射开关;161;1;[Enum];Create;False;0;2;off;0;on;1;0;True;1;KWEnum(g13,off,_0,on,_1);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;1173;-215.3965,-6409.447;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;1126;572.0652,-2425.413;Inherit;False;maintexuv;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;182;6161.214,-939.6256;Inherit;False;181;vertexoffset;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;13;-8090.629,-1763.14;Inherit;False;Property;_Float1;材质模式;1;0;Create;False;0;2;blend;10;add;1;0;True;2;SubEnum(g17,UnityEngine.Rendering.BlendMode);SubEnum(g17,add,1,blend,10);False;10;10;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;2097;151.0629,-1047.595;Inherit;False;2092;ramp_offset;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector3Node;1174;-514.5505,-6109.532;Inherit;False;Constant;_Vector8;Vector 8;138;0;Create;True;0;0;0;False;0;False;0,0,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.LerpOp;1905;6138.029,9427.776;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;2089;1635.795,-1481.446;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;1,0;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;2120;2621.038,-2683.819;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;1,0;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;427;-8382.333,-2040.575;Inherit;False;Property;_Depthfade_Group1;Depthfade;17;0;Create;False;0;0;0;True;1;Main(g9, _, off, off);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;997;1029.241,-5511.873;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;1909;6432.32,9691.046;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;2054;-3313.905,-1893.872;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;456;-2516.028,-3009.449;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;1176;198.3375,-6225.29;Inherit;False;2;2;0;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;451;-1729.025,-3108.424;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;849;-3250.307,-3221.214;Inherit;False;Constant;_Float108;Float 108;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;167;-6594.58,2699.5;Inherit;True;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;1072;5961.61,-6131.731;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.FunctionNode;460;-2413.342,-3578.787;Inherit;False;Polar Coordinates;-1;;170;7dab8e02884cf104ebefaa2e788e4162;0;4;1;FLOAT2;0,0;False;2;FLOAT2;0.5,0.5;False;3;FLOAT;1;False;4;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1891;4604.309,8214.585;Inherit;False;1;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;401;-8390.813,-1934.699;Inherit;False;Property;_Maintex_Group;主贴图;31;0;Create;False;0;0;0;True;1;Main(g1, _, off, off);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;229;-1498.935,-3046.488;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SmoothstepOpNode;968;2030.494,-5846.112;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;852;-3014.983,-3255.047;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DynamicAppendNode;851;-2954.724,-3437.085;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;2039;-373.5292,-931.7617;Inherit;False;2040;mapping_toggle;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;2113;-1389.42,3415.329;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector3Node;905;-3186.874,2348.734;Inherit;False;Constant;_Vector30;Vector 30;138;0;Create;True;0;0;0;False;0;False;0,0,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;928;5834.911,-1096.317;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.ClampOpNode;2051;1802.001,-2002.121;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1895;5190.259,8757.437;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;345;1997.769,-2467.928;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;2036;1350.739,-2866.517;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;1,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DynamicAppendNode;1228;1100.231,-2579.384;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;1209;5511.886,-6171.244;Inherit;False;Property;_Float157;matcap去色;163;0;Create;False;0;0;0;False;1;Sub(g13);False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;453;-2109.892,-2994.504;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;235;-599.927,-1195.649;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;233;-911.2366,-1018.684;Inherit;False;67;noise_intensity_main;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;581;-3147.44,2700.61;Inherit;False;Property;_Vector21;折射流动&斜切;145;0;Create;False;0;0;0;False;1;Sub(g6);False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;1598;813.3359,-6370.22;Inherit;False;normal_111;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DesaturateOpNode;1210;5953.866,-6341.064;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.NegateNode;1579;4784.33,-5880.727;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;847;-3229.639,-3358.295;Inherit;False;Constant;_Float106;Float 106;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;652;-1631.342,1933.092;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1532;4542.33,9254.287;Inherit;False;Property;_Custom10;自定义折射;194;1;[Enum];Create;False;0;8;custom1x;0;custom1y;1;custom1z;2;custom1w;3;custom2x;4;custom2y;5;custom2z;6;custom2w;7;0;True;1;SubKeywordEnumDrawer(g7,custom1x,custom1y,custom1z,custom1w,custom2x,custom2y,custom2z,custom2w);False;4;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClipNode;671;6047.038,-490.159;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;234;-879.0126,-1240.143;Inherit;False;231;Gradienttex;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;1239;-3092.695,413.1902;Inherit;False;Property;_Float160;二层亮边宽度;89;0;Create;False;0;0;0;False;1;Sub(g3);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;458;-2264.886,-2811.176;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;2035;2312.764,-2891.508;Inherit;False;ramp_maintex;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;854;-2583.405,-3410.126;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.OneMinusNode;2033;1915.185,-2975.64;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;855;-2188.006,-3362.838;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;599;1504.561,-2251.626;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.DynamicAppendNode;2034;2108.879,-2878.254;Inherit;False;FLOAT2;4;0;FLOAT2;0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1892;4995.645,8859.474;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;1531;7010.029,10266.94;Inherit;False;custom10;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;134;3197.352,-1004.2;Inherit;False;133;dis_edge;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;2057;1633.875,-2707.694;Inherit;False;ramppower;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1894;5154.17,8272.918;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;-3.28;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;908;-2912.077,2170.487;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1903;5845.625,9834.396;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;2091;-2323.194,-2655.204;Inherit;False;ramp_tilling;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;461;-1991.686,-3258.543;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;1,1;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;420;-8383.59,-1562.18;Inherit;False;Property;_Vertex_Group;顶点偏移;119;0;Create;False;0;0;0;True;1;Main(g5, _, off, off);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1900;5578.079,8860.452;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;1912;5017.897,10046.36;Inherit;False;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector4Node;575;-3206.392,2529.498;Inherit;False;Property;_reftex_ST;_reftex_ST;144;1;[HideInInspector];Create;True;0;0;0;False;0;False;1,1,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;1170;-461.6045,-6512.771;Inherit;False;Constant;_Float149;Float 149;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;195;-1641.269,2795.659;Inherit;False;Property;_Float24;custom控制折射;193;0;Create;False;0;0;0;True;3;SubToggle(g7, _);Space;Space;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;609;-136.5722,-1558.172;Inherit;False;FLOAT2;1;0;FLOAT2;0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.PannerNode;2121;2829.39,-2646.944;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RotatorNode;2146;2177.216,-1308.674;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;231;-1306.062,-3047.576;Inherit;False;Gradienttex;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.OneMinusNode;2031;1517.817,-2969.657;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Vector4Node;1213;40.08984,-2883.879;Inherit;False;Property;_Vector16;色散;44;0;Create;False;0;0;0;False;1;Sub(g1);False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PowerNode;2032;1738.667,-2878.143;Inherit;False;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;1;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DynamicAppendNode;906;-2929.735,2064.001;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;1068;5307.213,-6383.365;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;1,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;910;-2312.665,1978.759;Inherit;False;2;2;0;FLOAT2;0,1;False;1;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode;576;-2640.736,1954.054;Inherit;False;Polar Coordinates;-1;;169;7dab8e02884cf104ebefaa2e788e4162;0;4;1;FLOAT2;0,0;False;2;FLOAT2;0.5,0.5;False;3;FLOAT;1;False;4;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;655;-1482.538,2038.624;Inherit;False;Property;_Float69;折射贴图x轴Clamp;140;0;Create;False;0;0;0;True;1;SubToggle(g6, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;2056;-3000.581,-1877.217;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PannerNode;2060;555.4571,-1144.124;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;1267;-2943.18,248.7166;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;971;2028.237,-6006.439;Inherit;False;lit;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.MatrixFromVectors;1175;-22.63062,-6393.672;Inherit;False;FLOAT3x3;True;4;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3x3;0
Node;AmplifyShaderEditor.BreakToComponentsNode;650;-1819.611,1958.332;Inherit;False;FLOAT2;1;0;FLOAT2;0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.RegisterLocalVarNode;2040;2008.542,-2066.722;Inherit;False;mapping_toggle;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;579;-2779.336,2565.054;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;1896;5311.37,9004.329;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;2030;1417.995,-2711.203;Inherit;False;Property;_Float152;映射power;55;0;Create;False;0;0;0;False;1;Sub(g11);False;2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;909;-2500.665,2142.259;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT3x3;0,0,0,1,1,1,1,0,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;1262;-1378.545,-1260.126;Inherit;False;dis_00;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1901;5508.149,9284.732;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1904;5877.437,9144.962;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;1223;767.2168,-2753.049;Inherit;True;Property;_maintex1;主贴图;33;0;Create;False;0;0;0;False;0;False;212;None;None;True;0;False;white;Auto;False;Instance;1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleSubtractOpNode;455;-2523.359,-3128.364;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;212;1003.84,-1548.612;Inherit;True;Property;_Ramptex;Ramp贴图;48;0;Create;False;0;0;0;False;1;Ramp(g11);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;2038;-591.3917,-1031.101;Inherit;False;2035;ramp_maintex;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;106;-3187.887,198.0487;Inherit;False;Property;_Float17;一层亮边宽度;88;0;Create;False;0;0;0;False;1;Sub(g3);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;1066;5057.868,-6341.431;Inherit;False;2;2;0;FLOAT4x4;0,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;2058;-3477.915,-1756.516;Inherit;False;2057;ramppower;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1146;1870.057,-716.2144;Inherit;False;Property;_Float145;开启外边缘;26;0;Create;False;0;0;0;False;1;SubToggle(g8, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1008;1509.587,-6124.007;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;192;-1585.979,2465.205;Inherit;False;Property;_Float23;折射强度;147;0;Create;False;0;0;0;False;1;Sub(g6);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;1273;3287.701,-1123.091;Inherit;False;1271;dis_soft_edge;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;1009;1270.072,-5906.697;Inherit;False;1007;pointlight;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;1533;-1649.501,2674.632;Inherit;False;1531;custom10;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;653;-1144.161,1993.392;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;332;1549.109,-2551.261;Inherit;False;126;depthfade;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1899;5551.334,9571.126;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;4;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;196;-344.1315,2114.849;Inherit;False;ref;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;1230;104.127,-3063.969;Inherit;False;Constant;_Float158;Float 158;178;0;Create;True;0;0;0;False;0;False;0.01;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;343;1762.36,-2611.725;Inherit;False;Property;_Float28;边缘强度;20;0;Create;False;0;0;0;False;1;Sub(g9);False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;2128;758.4372,-1466.346;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;335;1544.042,-2454.097;Inherit;False;334;depthfade_switch;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;781;-1290.998,1938.46;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;1587;5279.597,-5941.925;Inherit;True;Property;_cubemap;cubemap;165;1;[NoScaleOffset];Create;True;0;0;0;False;1;Sub(g13);False;-1;None;None;True;0;False;white;LockedToCube;False;Object;-1;Auto;Cube;8;0;SAMPLERCUBE;;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;3;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;2081;2412.417,-1381.617;Inherit;True;Property;_TextureSample0;Texture Sample 0;48;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Instance;212;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;2126;3751.818,-2565.653;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.OneMinusNode;2109;-1865.637,3409.435;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;966;4373.305,-1734.843;Inherit;False;Property;_Color4;暗部颜色;156;1;[HDR];Create;False;0;0;0;False;1;Sub(g12);False;0.490566,0.490566,0.490566,1;0.6132076,0.2690014,0.2690014,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;421;-8392.288,-1444.528;Inherit;False;Property;_Ref_Group;折射;137;0;Create;False;0;0;0;True;1;Main(g6, _, off, off);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1898;5386.755,8529.215;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;2112;-1556.104,3416.044;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;612;-159.2173,-1795.018;Inherit;False;Property;_Float63;ramp图x轴Clamp;49;0;Create;False;0;0;0;True;1;SubToggle(g11, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;848;-3171.032,-3485.935;Inherit;False;Constant;_Float107;Float 107;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1171;-474.0176,-6372.938;Inherit;False;Constant;_Float150;Float 150;138;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SwitchByFaceNode;356;1518.056,-924.2145;Inherit;False;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.DynamicAppendNode;996;943.8885,-5683.873;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;1168;-503.6626,-6267.252;Inherit;False;Constant;_Float147;Float 147;138;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StepOpNode;1240;-2147.242,331.1862;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;1142;1571.056,-718.2144;Inherit;False;Property;_Color6;外边缘颜色;28;1;[HDR];Create;False;0;0;0;False;1;Sub(g8);False;1,1,1,1;0,0.6419505,2.270603,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RotatorNode;383;-299.9258,-1427.118;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1264;-1808.942,239.2805;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;995;563.2158,-5675.33;Inherit;False;Property;_Vector34;假点光坐标;158;0;Create;False;0;0;0;False;1;Sub(g12);False;0,0,0,0;-0.15,1.8,-0.41,0.4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1897;5290.742,9288.311;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;3;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;2134;2470.276,-1814.323;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScreenColorNode;183;-542.5228,2070.272;Inherit;False;Global;_GrabScreen0;Grab Screen 0;1;0;Create;True;0;0;0;False;0;False;Object;-1;False;False;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;1145;2012.404,-1004.214;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;2085;2571.246,-1468.345;Inherit;False;Constant;_Float18;Float 18;198;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;2142;-555.7626,2333.708;Inherit;True;Global;_PandaGrabTex;_PandaGrabTex;197;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;2133;984.6409,-2387.52;Inherit;False;Property;_Float61;主贴图去色;40;0;Create;False;0;0;0;False;1;Sub(g1);False;0;4.74;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.MatrixFromVectors;907;-2748.884,2167.934;Inherit;False;FLOAT3x3;True;4;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3x3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;2078;3107.646,-1300.497;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;783;-1532.768,2168.542;Inherit;False;Property;_Float86;折射贴图y轴Clamp;141;0;Create;False;0;0;0;True;1;SubToggle(g6, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;109;-2674.31,238.7258;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;1226;831.442,-3082.568;Inherit;True;Property;_maintex2;主贴图;33;0;Create;False;0;0;0;False;0;False;212;None;None;True;0;False;white;Auto;False;Instance;1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ClampOpNode;2052;-3621.027,-1879.451;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;580;-2763.697,2690.697;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;1225;242.4564,-2876.727;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;582;-2389.344,2394.38;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;424;4048.194,-832.704;Inherit;False;Property;_Float48;折射开关;138;1;[Enum];Create;False;0;2;off;0;on;1;0;True;1;KWEnum(g6,off,_0,on,_1);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;767;199.5411,-1469.15;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;2059;-2833.76,-1888.433;Inherit;False;ramp_distex;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;303;-5883.209,5961.582;Inherit;True;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.OneMinusNode;2053;-3476.798,-1881.872;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1906;6009.478,10104.3;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;6;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;232;-876.2811,-1133.452;Inherit;False;70;noise;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;586;-2330.196,2627.662;Inherit;False;Property;_Float58;折射极坐标（竖向贴图）;142;0;Create;False;0;0;0;False;1;SubToggle(g6, _);False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;1902;5768.741,9567.55;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;1180;-524.8001,-5901.105;Inherit;False;Property;_Vector10;法线流动&斜切;151;0;Create;False;0;0;0;False;1;Sub(g12);False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.WorldPosInputsNode;1002;1291.197,-5360.825;Inherit;True;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleAddOpNode;1224;496.2668,-2773.436;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;131;3760.194,-1360.704;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;2110;-1866.754,3534.791;Inherit;False;2057;ramppower;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;1227;616.1271,-3069.172;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;2108;-2009.866,3411.856;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DotProductOpNode;960;1297.528,-6246.394;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RotatorNode;2143;3051.424,-2499.782;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;2087;2326.732,-2290.966;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.ClampOpNode;651;-1639.822,2042.254;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;193;-1364.284,2656.537;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;2129;526.081,-1368.253;Inherit;False;2040;mapping_toggle;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RotatorNode;589;-1479.219,2228.388;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;1147;656.7075,-5998.508;Inherit;True;Property;_normallight;法线;150;0;Create;False;0;0;0;False;1;Sub(g12);False;-1;None;None;True;0;False;white;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;201;-1380.826,2430.654;Inherit;False;Constant;_Float26;Float 26;43;0;Create;True;0;0;0;False;0;False;0.01;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;1910;6596.174,9960.956;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WorldNormalVector;1591;4806.087,-5765.469;Inherit;False;False;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.GetLocalVarNode;2147;1915.544,-1205.625;Inherit;False;2144;ramp_rotater;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;2098;188.1606,-939.1063;Inherit;False;2090;ramp_speed;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ViewMatrixNode;1065;4867.248,-6431.993;Inherit;False;0;1;FLOAT4x4;0
Node;AmplifyShaderEditor.OneMinusNode;2055;-3167.265,-1876.502;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;2068;348.0318,-1275.268;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;1,0;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;1261;-2205.505,-2715.717;Inherit;False;Property;_Float159;ramp图极坐标（竖向贴图）;47;0;Create;False;0;0;0;True;1;SubToggle(g11, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;1599;4501.518,-6203.774;Inherit;False;1598;normal_111;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SamplerNode;190;-1030.067,2242.761;Inherit;True;Property;_reftex; 折射贴图（法线）;139;0;Create;False;0;0;0;False;1;Sub(g6);False;-1;None;None;True;0;False;white;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;766;-159.9981,-1286.526;Inherit;False;Property;_Float81;ramp图y轴Clamp;50;0;Create;False;0;0;0;True;1;SubToggle(g11, _);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;1266;-2783.489,413.5929;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;-0.1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;2140;6519.638,-1097.423;Float;False;False;-1;2;UnityEditor.ShaderGraph.PBRMasterGUI;0;1;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;DepthOnly;0;3;DepthOnly;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;0;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;False;False;True;False;False;False;False;0;False;-1;False;False;False;False;False;False;False;False;False;True;1;False;-1;False;False;True;1;LightMode=DepthOnly;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;2137;6519.638,-1097.423;Float;False;False;-1;2;UnityEditor.ShaderGraph.PBRMasterGUI;0;1;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;ExtraPrePass;0;0;ExtraPrePass;5;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;0;0;False;True;1;1;False;-1;0;False;-1;0;1;False;-1;0;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;True;True;True;True;0;False;-1;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;True;1;False;-1;True;3;False;-1;True;True;0;False;-1;0;False;-1;True;0;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;2138;6519.638,-1097.423;Float;False;True;-1;2;LWGUI.LWGUI;0;3;Hotwater/2024/UrpAll_GUI_1119;2992e84f91cbeb14eab234972e07ea9d;True;Forward;0;1;Forward;8;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;True;True;2;True;22;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;True;4;0;True;True;1;5;False;-1;10;True;13;0;5;False;-1;10;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;True;0;True;598;False;False;False;False;False;False;True;True;True;255;True;597;255;False;-1;255;False;-1;7;True;595;1;True;596;1;False;-1;1;False;-1;7;True;595;1;True;596;1;False;-1;1;False;-1;True;True;2;True;20;True;3;True;21;True;True;0;False;-1;0;False;-1;True;1;LightMode=UniversalForward;False;0;Hidden/InternalErrorShader;0;0;Standard;22;Surface;1;  Blend;0;Two Sided;0;Cast Shadows;0;  Use Shadow Threshold;0;Receive Shadows;0;GPU Instancing;1;LOD CrossFade;0;Built-in Fog;0;DOTS Instancing;0;Meta Pass;0;Extra Pre Pass;0;Tessellation;0;  Phong;0;  Strength;0.5,False,-1;  Type;0;  Tess;16,False,-1;  Min;10,False,-1;  Max;25,False,-1;  Edge Length;16,False,-1;  Max Displacement;25,False,-1;Vertex Position,InvertActionOnDeselection;1;0;5;False;True;False;True;False;False;;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;2141;6519.638,-1097.423;Float;False;False;-1;2;UnityEditor.ShaderGraph.PBRMasterGUI;0;1;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;Meta;0;4;Meta;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;0;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Meta;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;2139;6519.638,-1097.423;Float;False;False;-1;2;UnityEditor.ShaderGraph.PBRMasterGUI;0;1;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;ShadowCaster;0;2;ShadowCaster;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;0;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;False;-1;True;3;False;-1;False;True;1;LightMode=ShadowCaster;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
WireConnection;930;0;929;0
WireConnection;1637;0;1635;1
WireConnection;1637;1;1635;2
WireConnection;1641;0;528;3
WireConnection;1641;1;528;4
WireConnection;1636;0;528;1
WireConnection;1636;1;528;2
WireConnection;1625;0;1134;0
WireConnection;1638;0;1637;0
WireConnection;1638;1;1636;0
WireConnection;1638;2;1641;0
WireConnection;876;0;871;0
WireConnection;876;1;529;3
WireConnection;876;2;872;0
WireConnection;1628;0;676;1
WireConnection;1628;1;676;2
WireConnection;1629;0;1627;1
WireConnection;1629;1;1627;2
WireConnection;878;0;529;4
WireConnection;878;1;873;0
WireConnection;878;2;874;0
WireConnection;941;0;51;0
WireConnection;941;1;943;0
WireConnection;941;2;942;0
WireConnection;1633;0;676;3
WireConnection;1633;1;676;4
WireConnection;1639;0;941;0
WireConnection;1639;1;1638;0
WireConnection;1639;2;1640;0
WireConnection;866;0;863;0
WireConnection;866;1;684;3
WireConnection;866;2;864;0
WireConnection;535;3;528;1
WireConnection;535;4;528;2
WireConnection;1630;0;1629;0
WireConnection;1630;1;1628;0
WireConnection;1630;2;1633;0
WireConnection;877;0;876;0
WireConnection;877;1;878;0
WireConnection;877;2;875;0
WireConnection;938;0;680;0
WireConnection;938;1;940;0
WireConnection;938;2;939;0
WireConnection;865;0;684;4
WireConnection;865;1;862;0
WireConnection;865;2;861;0
WireConnection;1631;0;938;0
WireConnection;1631;1;1630;0
WireConnection;1631;2;1632;0
WireConnection;868;0;866;0
WireConnection;868;1;865;0
WireConnection;868;2;867;0
WireConnection;880;0;535;0
WireConnection;880;1;877;0
WireConnection;531;0;528;3
WireConnection;531;1;528;4
WireConnection;879;0;1639;0
WireConnection;879;1;877;0
WireConnection;682;3;676;1
WireConnection;682;4;676;2
WireConnection;1708;0;1335;0
WireConnection;536;0;879;0
WireConnection;536;1;531;0
WireConnection;1684;0;1284;0
WireConnection;869;0;1631;0
WireConnection;869;1;868;0
WireConnection;1778;0;1384;0
WireConnection;1937;0;1563;0
WireConnection;679;0;676;3
WireConnection;679;1;676;4
WireConnection;1694;0;1284;0
WireConnection;870;0;682;0
WireConnection;870;1;868;0
WireConnection;1936;0;1563;0
WireConnection;1729;0;1335;0
WireConnection;1777;0;1384;0
WireConnection;1801;0;1421;0
WireConnection;1800;0;1421;0
WireConnection;538;0;880;0
WireConnection;538;2;531;0
WireConnection;1803;0;1421;0
WireConnection;1782;0;1778;0
WireConnection;1804;0;1802;1
WireConnection;1804;1;1802;2
WireConnection;1804;2;1800;0
WireConnection;1805;0;1801;0
WireConnection;683;0;869;0
WireConnection;683;1;679;0
WireConnection;1780;0;1384;0
WireConnection;1692;0;1684;0
WireConnection;1711;0;1335;0
WireConnection;1709;0;1708;0
WireConnection;1710;0;1728;1
WireConnection;1710;1;1728;2
WireConnection;1710;2;1729;0
WireConnection;530;0;529;1
WireConnection;530;1;529;2
WireConnection;1938;0;1563;0
WireConnection;1940;0;1937;0
WireConnection;539;0;536;0
WireConnection;539;1;538;0
WireConnection;539;2;540;0
WireConnection;694;0;870;0
WireConnection;694;2;679;0
WireConnection;1939;0;1952;1
WireConnection;1939;1;1952;2
WireConnection;1939;2;1936;0
WireConnection;1781;0;1779;1
WireConnection;1781;1;1779;2
WireConnection;1781;2;1777;0
WireConnection;1690;0;1284;0
WireConnection;1681;0;1281;1
WireConnection;1681;1;1281;2
WireConnection;1681;2;1694;0
WireConnection;1693;0;1690;0
WireConnection;394;0;393;0
WireConnection;1943;0;1563;0
WireConnection;1941;0;1939;0
WireConnection;1941;1;1952;3
WireConnection;1941;2;1940;0
WireConnection;687;0;684;1
WireConnection;687;1;684;2
WireConnection;1713;0;1711;0
WireConnection;1712;0;1710;0
WireConnection;1712;1;1728;3
WireConnection;1712;2;1709;0
WireConnection;1683;0;1681;0
WireConnection;1683;1;1281;3
WireConnection;1683;2;1692;0
WireConnection;1942;0;1938;0
WireConnection;1807;0;1421;0
WireConnection;1696;0;1284;0
WireConnection;1806;0;1803;0
WireConnection;1784;0;1384;0
WireConnection;1783;0;1780;0
WireConnection;53;0;539;0
WireConnection;53;2;530;0
WireConnection;1808;0;1804;0
WireConnection;1808;1;1802;3
WireConnection;1808;2;1805;0
WireConnection;688;0;683;0
WireConnection;688;1;694;0
WireConnection;688;2;685;0
WireConnection;1714;0;1335;0
WireConnection;1785;0;1781;0
WireConnection;1785;1;1779;3
WireConnection;1785;2;1782;0
WireConnection;1946;0;1943;0
WireConnection;1786;0;1384;0
WireConnection;1717;0;1712;0
WireConnection;1717;1;1728;4
WireConnection;1717;2;1713;0
WireConnection;1945;0;1941;0
WireConnection;1945;1;1952;4
WireConnection;1945;2;1942;0
WireConnection;395;0;53;0
WireConnection;395;2;394;0
WireConnection;1689;0;1683;0
WireConnection;1689;1;1281;4
WireConnection;1689;2;1693;0
WireConnection;1787;0;1784;0
WireConnection;1788;0;1785;0
WireConnection;1788;1;1779;4
WireConnection;1788;2;1783;0
WireConnection;1944;0;1563;0
WireConnection;1809;0;1421;0
WireConnection;697;0;688;0
WireConnection;697;2;687;0
WireConnection;1695;0;1696;0
WireConnection;1701;0;1284;0
WireConnection;1810;0;1807;0
WireConnection;1716;0;1335;0
WireConnection;695;0;696;0
WireConnection;1811;0;1808;0
WireConnection;1811;1;1802;4
WireConnection;1811;2;1806;0
WireConnection;1715;0;1714;0
WireConnection;1720;0;1716;0
WireConnection;1790;0;1384;0
WireConnection;1791;0;1788;0
WireConnection;1791;1;1789;1
WireConnection;1791;2;1787;0
WireConnection;1699;0;1701;0
WireConnection;1948;0;1563;0
WireConnection;1953;0;1945;0
WireConnection;1953;1;1947;1
WireConnection;1953;2;1946;0
WireConnection;1721;0;1335;0
WireConnection;1949;0;1944;0
WireConnection;638;0;395;0
WireConnection;1697;0;1689;0
WireConnection;1697;1;1698;1
WireConnection;1697;2;1695;0
WireConnection;1792;0;1786;0
WireConnection;1703;0;1284;0
WireConnection;698;0;697;0
WireConnection;698;2;695;0
WireConnection;1719;0;1717;0
WireConnection;1719;1;1718;1
WireConnection;1719;2;1715;0
WireConnection;1814;0;1811;0
WireConnection;1814;1;1812;1
WireConnection;1814;2;1810;0
WireConnection;1815;0;1809;0
WireConnection;1813;0;1421;0
WireConnection;1793;0;1791;0
WireConnection;1793;1;1789;2
WireConnection;1793;2;1792;0
WireConnection;1957;0;1563;0
WireConnection;1724;0;1335;0
WireConnection;1950;0;1948;0
WireConnection;1700;0;1697;0
WireConnection;1700;1;1698;2
WireConnection;1700;2;1699;0
WireConnection;1707;0;1284;0
WireConnection;1702;0;1703;0
WireConnection;1722;0;1719;0
WireConnection;1722;1;1718;2
WireConnection;1722;2;1720;0
WireConnection;689;0;698;0
WireConnection;1723;0;1721;0
WireConnection;1954;0;1953;0
WireConnection;1954;1;1947;2
WireConnection;1954;2;1949;0
WireConnection;1845;0;1470;0
WireConnection;1818;0;1813;0
WireConnection;1817;0;1421;0
WireConnection;1795;0;1790;0
WireConnection;1847;0;1470;0
WireConnection;1794;0;1384;0
WireConnection;1816;0;1814;0
WireConnection;1816;1;1812;2
WireConnection;1816;2;1815;0
WireConnection;640;0;638;0
WireConnection;639;0;638;1
WireConnection;1726;0;1722;0
WireConnection;1726;1;1718;3
WireConnection;1726;2;1723;0
WireConnection;780;0;638;1
WireConnection;780;1;639;0
WireConnection;780;2;779;0
WireConnection;1796;0;1793;0
WireConnection;1796;1;1789;3
WireConnection;1796;2;1795;0
WireConnection;1705;0;1707;0
WireConnection;1797;0;1794;0
WireConnection;778;0;638;0
WireConnection;778;1;640;0
WireConnection;778;2;643;0
WireConnection;1820;0;1817;0
WireConnection;1725;0;1724;0
WireConnection;1704;0;1700;0
WireConnection;1704;1;1698;3
WireConnection;1704;2;1702;0
WireConnection;690;0;689;0
WireConnection;1951;0;1957;0
WireConnection;1848;0;1845;0
WireConnection;1850;0;1470;0
WireConnection;1955;0;1954;0
WireConnection;1955;1;1947;3
WireConnection;1955;2;1950;0
WireConnection;691;0;689;1
WireConnection;1849;0;1846;1
WireConnection;1849;1;1846;2
WireConnection;1849;2;1847;0
WireConnection;1819;0;1816;0
WireConnection;1819;1;1812;3
WireConnection;1819;2;1818;0
WireConnection;641;0;778;0
WireConnection;641;1;780;0
WireConnection;776;0;689;0
WireConnection;776;1;691;0
WireConnection;776;2;777;0
WireConnection;1821;0;1819;0
WireConnection;1821;1;1812;4
WireConnection;1821;2;1820;0
WireConnection;1706;0;1704;0
WireConnection;1706;1;1698;4
WireConnection;1706;2;1705;0
WireConnection;1727;0;1726;0
WireConnection;1727;1;1718;4
WireConnection;1727;2;1725;0
WireConnection;1852;0;1470;0
WireConnection;1956;0;1955;0
WireConnection;1956;1;1947;4
WireConnection;1956;2;1951;0
WireConnection;1853;0;1849;0
WireConnection;1853;1;1846;3
WireConnection;1853;2;1848;0
WireConnection;1798;0;1796;0
WireConnection;1798;1;1789;4
WireConnection;1798;2;1797;0
WireConnection;1851;0;1850;0
WireConnection;775;0;689;0
WireConnection;775;1;690;0
WireConnection;775;2;692;0
WireConnection;50;1;641;0
WireConnection;1648;0;499;3
WireConnection;1648;1;499;4
WireConnection;1401;0;1798;0
WireConnection;1337;0;1727;0
WireConnection;1131;0;431;1
WireConnection;1131;1;431;2
WireConnection;1130;0;431;3
WireConnection;1130;1;431;4
WireConnection;1336;0;1706;0
WireConnection;1854;0;1470;0
WireConnection;693;0;775;0
WireConnection;693;1;776;0
WireConnection;1420;0;1821;0
WireConnection;1857;0;1852;0
WireConnection;1644;0;1642;1
WireConnection;1644;1;1642;2
WireConnection;1855;0;1853;0
WireConnection;1855;1;1846;4
WireConnection;1855;2;1851;0
WireConnection;1679;0;1128;1
WireConnection;1679;1;1128;2
WireConnection;1756;0;1368;0
WireConnection;1730;0;1348;0
WireConnection;1574;0;1956;0
WireConnection;1754;0;1368;0
WireConnection;1643;0;499;1
WireConnection;1643;1;499;2
WireConnection;1751;0;1348;0
WireConnection;1733;0;1348;0
WireConnection;952;0;57;0
WireConnection;952;1;953;0
WireConnection;952;2;951;0
WireConnection;1758;0;1755;1
WireConnection;1758;1;1755;2
WireConnection;1758;2;1754;0
WireConnection;1859;0;1854;0
WireConnection;1759;0;1368;0
WireConnection;811;0;500;4
WireConnection;811;1;809;0
WireConnection;811;2;807;0
WireConnection;1732;0;1750;1
WireConnection;1732;1;1750;2
WireConnection;1732;2;1751;0
WireConnection;1858;0;1855;0
WireConnection;1858;1;1856;1
WireConnection;1858;2;1857;0
WireConnection;923;0;50;1
WireConnection;923;1;924;1
WireConnection;923;2;924;2
WireConnection;923;3;924;3
WireConnection;923;4;924;4
WireConnection;564;1;693;0
WireConnection;1645;0;1644;0
WireConnection;1645;1;1643;0
WireConnection;1645;2;1648;0
WireConnection;1731;0;1730;0
WireConnection;810;0;814;0
WireConnection;810;1;500;3
WireConnection;810;2;808;0
WireConnection;1133;0;1679;0
WireConnection;1133;1;1131;0
WireConnection;1133;2;1130;0
WireConnection;803;0;439;4
WireConnection;803;1;802;0
WireConnection;803;2;801;0
WireConnection;799;0;796;0
WireConnection;799;1;439;3
WireConnection;799;2;798;0
WireConnection;1757;0;1756;0
WireConnection;1860;0;1470;0
WireConnection;1736;0;1348;0
WireConnection;1760;0;1758;0
WireConnection;1760;1;1755;3
WireConnection;1760;2;1757;0
WireConnection;1646;0;952;0
WireConnection;1646;1;1645;0
WireConnection;1646;2;1647;0
WireConnection;1735;0;1733;0
WireConnection;506;3;499;1
WireConnection;506;4;499;2
WireConnection;502;0;499;3
WireConnection;502;1;499;4
WireConnection;1863;0;1858;0
WireConnection;1863;1;1856;2
WireConnection;1863;2;1859;0
WireConnection;1734;0;1732;0
WireConnection;1734;1;1750;3
WireConnection;1734;2;1731;0
WireConnection;1862;0;1470;0
WireConnection;1761;0;1759;0
WireConnection;1425;0;1423;0
WireConnection;1425;1;1424;0
WireConnection;700;0;564;1
WireConnection;700;1;923;0
WireConnection;813;0;810;0
WireConnection;813;1;811;0
WireConnection;813;2;812;0
WireConnection;795;0;799;0
WireConnection;795;1;803;0
WireConnection;795;2;804;0
WireConnection;42;0;1338;0
WireConnection;42;1;1339;0
WireConnection;433;0;431;3
WireConnection;433;1;431;4
WireConnection;1575;1;1576;0
WireConnection;1575;2;1577;0
WireConnection;1861;0;1860;0
WireConnection;567;0;564;1
WireConnection;567;1;923;0
WireConnection;1127;0;35;0
WireConnection;1127;1;1133;0
WireConnection;1127;2;1626;0
WireConnection;443;3;431;1
WireConnection;443;4;431;2
WireConnection;1762;0;1368;0
WireConnection;1426;0;502;0
WireConnection;1426;1;1425;0
WireConnection;1426;2;1427;0
WireConnection;1763;0;1762;0
WireConnection;701;0;567;0
WireConnection;701;1;700;0
WireConnection;701;2;703;0
WireConnection;1737;0;1736;0
WireConnection;1864;0;1863;0
WireConnection;1864;1;1856;3
WireConnection;1864;2;1861;0
WireConnection;1865;0;1862;0
WireConnection;1764;0;1368;0
WireConnection;816;0;506;0
WireConnection;816;1;813;0
WireConnection;59;0;433;0
WireConnection;59;1;42;0
WireConnection;59;2;60;0
WireConnection;1738;0;1348;0
WireConnection;1422;0;502;0
WireConnection;1422;1;1425;0
WireConnection;1422;2;1427;0
WireConnection;815;0;1646;0
WireConnection;815;1;813;0
WireConnection;1739;0;1734;0
WireConnection;1739;1;1750;4
WireConnection;1739;2;1735;0
WireConnection;806;0;443;0
WireConnection;806;1;795;0
WireConnection;449;0;433;0
WireConnection;449;1;42;0
WireConnection;449;2;60;0
WireConnection;732;0;731;0
WireConnection;732;2;1575;0
WireConnection;793;0;1127;0
WireConnection;793;1;795;0
WireConnection;1765;0;1760;0
WireConnection;1765;1;1755;4
WireConnection;1765;2;1761;0
WireConnection;70;0;701;0
WireConnection;1743;0;1348;0
WireConnection;1742;0;1738;0
WireConnection;1768;0;1765;0
WireConnection;1768;1;1766;1
WireConnection;1768;2;1763;0
WireConnection;1767;0;1368;0
WireConnection;43;0;793;0
WireConnection;43;1;59;0
WireConnection;1866;0;1864;0
WireConnection;1866;1;1856;4
WireConnection;1866;2;1865;0
WireConnection;507;0;816;0
WireConnection;507;2;1422;0
WireConnection;1741;0;1739;0
WireConnection;1741;1;1740;1
WireConnection;1741;2;1737;0
WireConnection;1769;0;1764;0
WireConnection;446;0;806;0
WireConnection;446;2;449;0
WireConnection;733;0;732;0
WireConnection;509;0;815;0
WireConnection;509;1;1426;0
WireConnection;501;0;500;1
WireConnection;501;1;500;2
WireConnection;511;0;509;0
WireConnection;511;1;507;0
WireConnection;511;2;510;0
WireConnection;242;0;1982;1
WireConnection;242;1;1982;2
WireConnection;316;0;305;0
WireConnection;1489;0;1866;0
WireConnection;311;0;310;0
WireConnection;440;0;439;1
WireConnection;440;1;439;2
WireConnection;1771;0;1767;0
WireConnection;1744;0;1741;0
WireConnection;1744;1;1740;2
WireConnection;1744;2;1742;0
WireConnection;1745;0;1743;0
WireConnection;1746;0;1348;0
WireConnection;1770;0;1768;0
WireConnection;1770;1;1766;2
WireConnection;1770;2;1769;0
WireConnection;444;0;43;0
WireConnection;444;1;446;0
WireConnection;444;2;445;0
WireConnection;1772;0;1368;0
WireConnection;1773;0;1770;0
WireConnection;1773;1;1766;3
WireConnection;1773;2;1771;0
WireConnection;1748;0;1744;0
WireConnection;1748;1;1740;3
WireConnection;1748;2;1745;0
WireConnection;738;0;304;0
WireConnection;738;1;302;0
WireConnection;58;0;511;0
WireConnection;58;2;501;0
WireConnection;1747;0;1746;0
WireConnection;285;0;242;0
WireConnection;1774;0;1772;0
WireConnection;36;0;444;0
WireConnection;36;2;440;0
WireConnection;1749;0;1748;0
WireConnection;1749;1;1740;4
WireConnection;1749;2;1747;0
WireConnection;161;0;36;0
WireConnection;737;0;58;0
WireConnection;737;1;738;0
WireConnection;307;0;314;0
WireConnection;307;1;1490;0
WireConnection;307;2;317;0
WireConnection;1775;0;1773;0
WireConnection;1775;1;1766;4
WireConnection;1775;2;1774;0
WireConnection;1379;0;1775;0
WireConnection;1103;0;1095;0
WireConnection;1359;0;1749;0
WireConnection;309;0;737;0
WireConnection;309;1;306;0
WireConnection;309;2;307;0
WireConnection;824;0;479;4
WireConnection;824;1;821;0
WireConnection;824;2;818;0
WireConnection;1092;0;1121;0
WireConnection;1092;1;1094;0
WireConnection;1092;2;1103;0
WireConnection;1092;3;1096;0
WireConnection;1092;4;1104;0
WireConnection;834;0;484;4
WireConnection;834;1;831;0
WireConnection;834;2;828;0
WireConnection;360;0;55;0
WireConnection;360;2;1575;0
WireConnection;1823;0;1435;0
WireConnection;92;0;309;0
WireConnection;833;0;830;0
WireConnection;833;1;484;3
WireConnection;833;2;829;0
WireConnection;823;0;820;0
WireConnection;823;1;479;3
WireConnection;823;2;819;0
WireConnection;1825;0;1435;0
WireConnection;75;0;1380;0
WireConnection;75;1;1381;0
WireConnection;1828;0;1435;0
WireConnection;1097;0;1092;0
WireConnection;835;0;833;0
WireConnection;835;1;834;0
WireConnection;835;2;832;0
WireConnection;481;0;478;3
WireConnection;481;1;478;4
WireConnection;825;0;823;0
WireConnection;825;1;824;0
WireConnection;825;2;822;0
WireConnection;1826;0;1823;0
WireConnection;497;3;483;1
WireConnection;497;4;483;2
WireConnection;67;0;360;0
WireConnection;1827;0;1824;1
WireConnection;1827;1;1824;2
WireConnection;1827;2;1825;0
WireConnection;931;0;77;0
WireConnection;931;1;932;0
WireConnection;931;2;933;0
WireConnection;934;0;217;0
WireConnection;934;1;936;0
WireConnection;934;2;935;0
WireConnection;477;3;478;1
WireConnection;477;4;478;2
WireConnection;385;0;384;0
WireConnection;837;0;497;0
WireConnection;837;1;835;0
WireConnection;471;0;481;0
WireConnection;471;1;75;0
WireConnection;471;2;73;0
WireConnection;1831;0;1827;0
WireConnection;1831;1;1824;3
WireConnection;1831;2;1826;0
WireConnection;1829;0;1828;0
WireConnection;1830;0;1435;0
WireConnection;827;0;477;0
WireConnection;827;1;825;0
WireConnection;735;0;734;0
WireConnection;735;2;1575;0
WireConnection;836;0;934;0
WireConnection;836;1;835;0
WireConnection;386;0;93;0
WireConnection;386;2;385;0
WireConnection;474;0;481;0
WireConnection;474;1;75;0
WireConnection;474;2;73;0
WireConnection;826;0;931;0
WireConnection;826;1;825;0
WireConnection;486;0;483;3
WireConnection;486;1;483;4
WireConnection;353;0;71;0
WireConnection;353;1;72;0
WireConnection;496;0;837;0
WireConnection;496;2;486;0
WireConnection;1835;0;1830;0
WireConnection;1099;0;162;0
WireConnection;1099;1;1100;0
WireConnection;1099;2;1101;0
WireConnection;1832;0;1435;0
WireConnection;635;0;386;0
WireConnection;1870;0;1500;0
WireConnection;1868;0;1500;0
WireConnection;491;0;836;0
WireConnection;491;1;486;0
WireConnection;469;0;826;0
WireConnection;469;1;474;0
WireConnection;736;0;735;0
WireConnection;472;0;827;0
WireConnection;472;2;471;0
WireConnection;1833;0;1831;0
WireConnection;1833;1;1824;4
WireConnection;1833;2;1829;0
WireConnection;633;0;635;1
WireConnection;1836;0;1833;0
WireConnection;1836;1;1834;1
WireConnection;1836;2;1835;0
WireConnection;485;0;484;1
WireConnection;485;1;484;2
WireConnection;1017;0;1015;1
WireConnection;1017;1;1015;2
WireConnection;1017;2;1015;3
WireConnection;1871;0;1500;0
WireConnection;467;0;469;0
WireConnection;467;1;472;0
WireConnection;467;2;468;0
WireConnection;632;0;635;0
WireConnection;480;0;479;1
WireConnection;480;1;479;2
WireConnection;1872;0;1869;1
WireConnection;1872;1;1869;2
WireConnection;1872;2;1868;0
WireConnection;955;0;418;0
WireConnection;955;1;956;0
WireConnection;955;2;954;0
WireConnection;354;0;1099;0
WireConnection;354;1;353;0
WireConnection;1838;0;1435;0
WireConnection;1873;0;1870;0
WireConnection;99;0;313;0
WireConnection;99;1;100;2
WireConnection;99;2;318;0
WireConnection;1837;0;1832;0
WireConnection;495;0;491;0
WireConnection;495;1;496;0
WireConnection;495;2;494;0
WireConnection;1841;0;1836;0
WireConnection;1841;1;1834;2
WireConnection;1841;2;1837;0
WireConnection;1874;0;1871;0
WireConnection;786;0;635;1
WireConnection;786;1;633;0
WireConnection;786;2;785;0
WireConnection;79;0;467;0
WireConnection;79;2;480;0
WireConnection;1875;0;1500;0
WireConnection;1876;0;1872;0
WireConnection;1876;1;1869;3
WireConnection;1876;2;1873;0
WireConnection;784;0;635;0
WireConnection;784;1;632;0
WireConnection;784;2;634;0
WireConnection;1840;0;1435;0
WireConnection;1203;0;1197;0
WireConnection;1203;1;1017;0
WireConnection;378;0;380;0
WireConnection;216;0;495;0
WireConnection;216;2;485;0
WireConnection;416;0;415;0
WireConnection;568;0;955;0
WireConnection;568;1;571;0
WireConnection;568;2;510;0
WireConnection;283;0;354;0
WireConnection;283;1;284;0
WireConnection;283;2;99;0
WireConnection;1839;0;1838;0
WireConnection;322;0;321;0
WireConnection;322;1;323;0
WireConnection;320;0;322;0
WireConnection;320;1;216;0
WireConnection;1199;0;1203;0
WireConnection;1199;1;1017;0
WireConnection;1199;2;1200;0
WireConnection;1843;0;1840;0
WireConnection;895;0;890;0
WireConnection;895;1;555;3
WireConnection;895;2;891;0
WireConnection;417;0;568;0
WireConnection;417;2;416;0
WireConnection;1880;0;1875;0
WireConnection;319;0;79;0
WireConnection;319;1;322;0
WireConnection;1842;0;1841;0
WireConnection;1842;1;1834;3
WireConnection;1842;2;1839;0
WireConnection;1878;0;1876;0
WireConnection;1878;1;1869;4
WireConnection;1878;2;1874;0
WireConnection;377;0;283;0
WireConnection;377;2;378;0
WireConnection;897;0;555;4
WireConnection;897;1;892;0
WireConnection;897;2;893;0
WireConnection;631;0;784;0
WireConnection;631;1;786;0
WireConnection;391;0;390;0
WireConnection;388;0;387;0
WireConnection;1877;0;1500;0
WireConnection;2002;0;1624;0
WireConnection;1035;0;1199;0
WireConnection;1035;1;1027;0
WireConnection;1881;0;1877;0
WireConnection;1257;1;417;0
WireConnection;1883;0;1878;0
WireConnection;1883;1;1879;1
WireConnection;1883;2;1880;0
WireConnection;1882;0;1500;0
WireConnection;389;0;320;0
WireConnection;389;2;388;0
WireConnection;23;1;631;0
WireConnection;949;0;173;0
WireConnection;949;1;950;0
WireConnection;949;2;948;0
WireConnection;1844;0;1842;0
WireConnection;1844;1;1834;4
WireConnection;1844;2;1843;0
WireConnection;392;0;319;0
WireConnection;392;2;391;0
WireConnection;896;0;895;0
WireConnection;896;1;897;0
WireConnection;896;2;894;0
WireConnection;603;0;377;0
WireConnection;1884;0;1883;0
WireConnection;1884;1;1879;2
WireConnection;1884;2;1881;0
WireConnection;605;0;603;1
WireConnection;888;0;716;4
WireConnection;888;1;883;0
WireConnection;888;2;884;0
WireConnection;1886;0;1882;0
WireConnection;898;0;949;0
WireConnection;898;1;896;0
WireConnection;549;3;544;1
WireConnection;549;4;544;2
WireConnection;604;0;603;0
WireConnection;2004;0;2002;0
WireConnection;623;0;389;0
WireConnection;886;0;881;0
WireConnection;886;1;716;3
WireConnection;886;2;882;0
WireConnection;1057;0;1257;1
WireConnection;1057;1;1035;0
WireConnection;1057;2;1059;0
WireConnection;2003;0;1624;0
WireConnection;1466;0;1844;0
WireConnection;1885;0;1500;0
WireConnection;617;0;392;0
WireConnection;1623;0;23;4
WireConnection;1623;1;23;1
WireConnection;1623;2;1624;0
WireConnection;1914;0;1537;0
WireConnection;624;0;623;0
WireConnection;764;0;603;1
WireConnection;764;1;605;0
WireConnection;764;2;763;0
WireConnection;2006;0;1623;0
WireConnection;2006;1;23;2
WireConnection;2006;2;2004;0
WireConnection;546;0;544;3
WireConnection;546;1;544;4
WireConnection;712;3;706;1
WireConnection;712;4;706;2
WireConnection;625;0;623;1
WireConnection;619;0;617;1
WireConnection;1888;0;1884;0
WireConnection;1888;1;1879;3
WireConnection;1888;2;1886;0
WireConnection;900;0;549;0
WireConnection;900;1;898;0
WireConnection;607;0;603;0
WireConnection;607;1;604;0
WireConnection;607;2;608;0
WireConnection;277;0;1057;0
WireConnection;1887;0;1885;0
WireConnection;946;0;707;0
WireConnection;946;1;947;0
WireConnection;946;2;945;0
WireConnection;1062;0;918;0
WireConnection;618;0;617;0
WireConnection;1916;0;1537;0
WireConnection;2005;0;2003;0
WireConnection;887;0;886;0
WireConnection;887;1;888;0
WireConnection;887;2;885;0
WireConnection;554;0;900;0
WireConnection;554;2;546;0
WireConnection;710;0;706;3
WireConnection;710;1;706;4
WireConnection;606;0;607;0
WireConnection;606;1;764;0
WireConnection;889;0;946;0
WireConnection;889;1;887;0
WireConnection;771;0;617;1
WireConnection;771;1;619;0
WireConnection;771;2;770;0
WireConnection;899;0;712;0
WireConnection;899;1;887;0
WireConnection;2007;0;2006;0
WireConnection;2007;1;23;3
WireConnection;2007;2;2005;0
WireConnection;1060;0;918;0
WireConnection;1060;1;1062;0
WireConnection;1060;2;1059;0
WireConnection;1917;0;1537;0
WireConnection;62;0;29;0
WireConnection;62;1;1469;0
WireConnection;62;2;61;0
WireConnection;1919;0;1916;0
WireConnection;915;0;912;4
WireConnection;1889;0;1888;0
WireConnection;1889;1;1879;4
WireConnection;1889;2;1887;0
WireConnection;551;0;898;0
WireConnection;551;1;546;0
WireConnection;773;0;623;1
WireConnection;773;1;625;0
WireConnection;773;2;774;0
WireConnection;1918;0;1915;1
WireConnection;1918;1;1915;2
WireConnection;1918;2;1914;0
WireConnection;772;0;623;0
WireConnection;772;1;624;0
WireConnection;772;2;626;0
WireConnection;769;0;617;0
WireConnection;769;1;618;0
WireConnection;769;2;620;0
WireConnection;916;0;2007;0
WireConnection;916;1;281;0
WireConnection;916;2;1060;0
WireConnection;1984;0;403;0
WireConnection;1511;0;1889;0
WireConnection;913;0;62;0
WireConnection;913;1;915;0
WireConnection;913;2;914;0
WireConnection;1960;0;1600;0
WireConnection;1959;0;1600;0
WireConnection;557;0;555;1
WireConnection;557;1;555;2
WireConnection;711;0;889;0
WireConnection;711;1;710;0
WireConnection;616;0;769;0
WireConnection;616;1;771;0
WireConnection;715;0;899;0
WireConnection;715;2;710;0
WireConnection;1921;0;1917;0
WireConnection;1;1;606;0
WireConnection;1920;0;1918;0
WireConnection;1920;1;1915;3
WireConnection;1920;2;1919;0
WireConnection;351;0;136;0
WireConnection;351;1;352;0
WireConnection;1922;0;1537;0
WireConnection;622;0;772;0
WireConnection;622;1;773;0
WireConnection;556;0;551;0
WireConnection;556;1;554;0
WireConnection;556;2;552;0
WireConnection;1925;0;1922;0
WireConnection;139;0;351;0
WireConnection;139;4;137;0
WireConnection;139;2;135;0
WireConnection;139;3;138;0
WireConnection;8;1;616;0
WireConnection;30;0;913;0
WireConnection;30;1;31;0
WireConnection;168;0;556;0
WireConnection;168;2;557;0
WireConnection;397;0;396;0
WireConnection;1999;0;408;0
WireConnection;1985;0;1984;0
WireConnection;719;0;716;1
WireConnection;719;1;716;2
WireConnection;334;0;333;0
WireConnection;24;0;916;0
WireConnection;24;1;25;0
WireConnection;661;0;916;0
WireConnection;1986;0;403;0
WireConnection;728;0;711;0
WireConnection;728;1;715;0
WireConnection;728;2;714;0
WireConnection;1992;0;405;0
WireConnection;1963;0;1960;0
WireConnection;1924;0;1920;0
WireConnection;1924;1;1915;4
WireConnection;1924;2;1921;0
WireConnection;218;1;622;0
WireConnection;97;1;98;0
WireConnection;97;0;96;0
WireConnection;1962;0;1975;1
WireConnection;1962;1;1975;2
WireConnection;1962;2;1959;0
WireConnection;1961;0;1600;0
WireConnection;402;0;1;4
WireConnection;402;1;1;1
WireConnection;402;2;403;0
WireConnection;1923;0;1537;0
WireConnection;1964;0;1962;0
WireConnection;1964;1;1975;3
WireConnection;1964;2;1963;0
WireConnection;140;0;139;0
WireConnection;327;0;97;0
WireConnection;725;0;728;0
WireConnection;725;2;719;0
WireConnection;1983;0;402;0
WireConnection;1983;1;1;2
WireConnection;1983;2;1985;0
WireConnection;1090;0;33;0
WireConnection;1090;1;1512;0
WireConnection;1090;2;1091;0
WireConnection;1991;0;405;0
WireConnection;1987;0;1986;0
WireConnection;1929;0;1924;0
WireConnection;1929;1;1926;1
WireConnection;1929;2;1925;0
WireConnection;1993;0;1992;0
WireConnection;26;0;24;0
WireConnection;26;1;30;0
WireConnection;398;0;168;0
WireConnection;398;2;397;0
WireConnection;2000;0;1999;0
WireConnection;1927;0;1537;0
WireConnection;407;0;218;4
WireConnection;407;1;218;1
WireConnection;407;2;408;0
WireConnection;1966;0;1600;0
WireConnection;406;0;8;4
WireConnection;406;1;8;1
WireConnection;406;2;405;0
WireConnection;1928;0;1923;0
WireConnection;1965;0;1961;0
WireConnection;754;2;752;0
WireConnection;754;0;756;0
WireConnection;754;1;753;0
WireConnection;1998;0;408;0
WireConnection;729;0;718;0
WireConnection;45;0;26;0
WireConnection;1994;0;406;0
WireConnection;1994;1;8;2
WireConnection;1994;2;1993;0
WireConnection;755;0;754;0
WireConnection;2001;0;407;0
WireConnection;2001;1;218;2
WireConnection;2001;2;2000;0
WireConnection;1968;0;1964;0
WireConnection;1968;1;1975;4
WireConnection;1968;2;1965;0
WireConnection;130;0;1053;0
WireConnection;34;0;1090;0
WireConnection;644;0;398;0
WireConnection;1988;0;1983;0
WireConnection;1988;1;1;3
WireConnection;1988;2;1987;0
WireConnection;1997;0;1998;0
WireConnection;1990;0;1991;0
WireConnection;141;0;140;0
WireConnection;336;0;97;0
WireConnection;336;1;327;0
WireConnection;336;2;337;0
WireConnection;1932;0;1537;0
WireConnection;1931;0;1927;0
WireConnection;1969;0;1966;0
WireConnection;726;0;725;0
WireConnection;726;2;729;0
WireConnection;1967;0;1600;0
WireConnection;1930;0;1929;0
WireConnection;1930;1;1926;2
WireConnection;1930;2;1928;0
WireConnection;1996;0;2001;0
WireConnection;1996;1;218;3
WireConnection;1996;2;1997;0
WireConnection;1972;0;1967;0
WireConnection;646;0;644;0
WireConnection;1195;0;1988;0
WireConnection;757;0;755;0
WireConnection;757;1;336;0
WireConnection;32;0;45;0
WireConnection;32;1;1090;0
WireConnection;32;2;34;0
WireConnection;105;0;913;0
WireConnection;105;1;130;0
WireConnection;1989;0;1994;0
WireConnection;1989;1;8;3
WireConnection;1989;2;1990;0
WireConnection;720;0;726;0
WireConnection;144;0;140;0
WireConnection;144;1;141;0
WireConnection;144;2;142;0
WireConnection;645;0;644;1
WireConnection;1933;0;1932;0
WireConnection;1976;0;1968;0
WireConnection;1976;1;1970;1
WireConnection;1976;2;1969;0
WireConnection;1971;0;1600;0
WireConnection;1934;0;1930;0
WireConnection;1934;1;1926;3
WireConnection;1934;2;1931;0
WireConnection;126;0;757;0
WireConnection;1935;0;1934;0
WireConnection;1935;1;1926;4
WireConnection;1935;2;1933;0
WireConnection;124;0;105;0
WireConnection;1977;0;1976;0
WireConnection;1977;1;1970;2
WireConnection;1977;2;1972;0
WireConnection;791;0;644;1
WireConnection;791;1;645;0
WireConnection;791;2;792;0
WireConnection;722;0;720;1
WireConnection;790;0;644;0
WireConnection;790;1;646;0
WireConnection;790;2;649;0
WireConnection;721;0;720;0
WireConnection;1980;0;1600;0
WireConnection;372;0;1195;0
WireConnection;372;1;365;0
WireConnection;220;0;1989;0
WireConnection;220;1;1996;0
WireConnection;1973;0;1971;0
WireConnection;122;0;32;0
WireConnection;147;0;144;0
WireConnection;647;0;790;0
WireConnection;647;1;791;0
WireConnection;1553;0;1935;0
WireConnection;1978;0;1977;0
WireConnection;1978;1;1970;3
WireConnection;1978;2;1973;0
WireConnection;1974;0;1980;0
WireConnection;789;0;720;1
WireConnection;789;1;722;0
WireConnection;789;2;788;0
WireConnection;373;0;372;0
WireConnection;373;1;370;0
WireConnection;82;0;220;0
WireConnection;787;0;720;0
WireConnection;787;1;721;0
WireConnection;787;2;727;0
WireConnection;338;0;128;0
WireConnection;338;2;340;0
WireConnection;374;0;1988;0
WireConnection;374;1;373;0
WireConnection;374;2;367;0
WireConnection;347;0;209;0
WireConnection;347;1;150;0
WireConnection;347;2;350;0
WireConnection;169;1;647;0
WireConnection;723;0;787;0
WireConnection;723;1;789;0
WireConnection;413;0;123;0
WireConnection;413;1;125;0
WireConnection;413;2;414;0
WireConnection;1979;0;1978;0
WireConnection;1979;1;1970;4
WireConnection;1979;2;1974;0
WireConnection;176;0;178;0
WireConnection;176;1;1554;0
WireConnection;176;2;179;0
WireConnection;921;0;169;1
WireConnection;921;1;922;1
WireConnection;921;2;922;2
WireConnection;921;3;922;3
WireConnection;921;4;922;4
WireConnection;6;0;374;0
WireConnection;6;1;3;4
WireConnection;6;2;4;4
WireConnection;6;3;413;0
WireConnection;6;4;95;0
WireConnection;6;5;338;0
WireConnection;6;6;221;0
WireConnection;6;7;347;0
WireConnection;705;1;723;0
WireConnection;993;0;994;0
WireConnection;993;1;172;0
WireConnection;993;2;990;0
WireConnection;1619;0;1979;0
WireConnection;171;0;921;0
WireConnection;171;1;993;0
WireConnection;171;2;175;0
WireConnection;171;3;176;0
WireConnection;171;4;705;1
WireConnection;602;0;6;0
WireConnection;656;0;6;0
WireConnection;656;1;602;0
WireConnection;656;2;657;0
WireConnection;1621;0;673;0
WireConnection;1621;1;1620;0
WireConnection;1621;2;1622;0
WireConnection;181;0;171;0
WireConnection;7;0;928;0
WireConnection;7;3;671;0
WireConnection;2130;0;1228;0
WireConnection;2130;1;2133;0
WireConnection;330;1;332;0
WireConnection;330;2;335;0
WireConnection;2042;0;599;0
WireConnection;2042;1;212;0
WireConnection;2042;2;2043;0
WireConnection;1269;0;45;0
WireConnection;1269;1;32;0
WireConnection;2125;0;2043;0
WireConnection;1270;0;1269;0
WireConnection;1893;0;1532;0
WireConnection;2080;0;2085;0
WireConnection;2080;1;2081;0
WireConnection;2080;2;2084;0
WireConnection;2122;1;2143;0
WireConnection;2136;0;2087;0
WireConnection;2136;1;2130;0
WireConnection;2136;2;2134;0
WireConnection;341;0;343;0
WireConnection;341;1;345;0
WireConnection;610;0;609;0
WireConnection;2037;0;235;0
WireConnection;2037;1;2038;0
WireConnection;2037;2;2039;0
WireConnection;2092;0;457;0
WireConnection;1242;0;2099;0
WireConnection;1242;1;2078;0
WireConnection;1242;2;1243;0
WireConnection;977;0;422;0
WireConnection;977;1;969;0
WireConnection;977;2;978;0
WireConnection;188;0;585;0
WireConnection;188;2;580;0
WireConnection;611;0;609;1
WireConnection;963;0;1008;0
WireConnection;191;0;201;0
WireConnection;191;1;193;0
WireConnection;107;0;109;0
WireConnection;107;1;130;0
WireConnection;359;0;4;0
WireConnection;359;1;357;0
WireConnection;359;2;358;0
WireConnection;584;0;910;0
WireConnection;584;2;579;0
WireConnection;768;0;765;0
WireConnection;768;1;767;0
WireConnection;957;0;1147;0
WireConnection;133;0;1263;0
WireConnection;1064;0;1599;0
WireConnection;457;0;459;3
WireConnection;457;1;459;4
WireConnection;1241;0;1264;0
WireConnection;969;0;967;0
WireConnection;969;1;973;0
WireConnection;969;2;972;0
WireConnection;454;0;455;0
WireConnection;454;1;456;0
WireConnection;2111;0;2109;0
WireConnection;2111;1;2110;0
WireConnection;965;0;963;0
WireConnection;965;1;961;0
WireConnection;185;0;202;0
WireConnection;185;1;190;0
WireConnection;1069;0;1068;0
WireConnection;1178;0;1180;1
WireConnection;1178;1;1180;2
WireConnection;1067;0;1066;0
WireConnection;1007;0;1001;0
WireConnection;1580;0;1579;0
WireConnection;1580;1;1591;0
WireConnection;1135;0;356;0
WireConnection;1135;1;1142;0
WireConnection;1135;2;1136;0
WireConnection;2135;0;2043;0
WireConnection;1263;0;105;0
WireConnection;1263;1;107;0
WireConnection;2072;0;459;1
WireConnection;2072;1;459;2
WireConnection;1012;0;997;0
WireConnection;1012;1;996;0
WireConnection;1012;2;1013;0
WireConnection;1207;0;977;0
WireConnection;1207;1;1078;0
WireConnection;2083;0;2043;0
WireConnection;759;0;199;0
WireConnection;759;1;760;0
WireConnection;759;2;3;0
WireConnection;1001;0;996;0
WireConnection;1001;1;1002;0
WireConnection;1271;0;1270;0
WireConnection;585;0;582;0
WireConnection;585;1;584;0
WireConnection;585;2;586;0
WireConnection;964;0;962;0
WireConnection;1255;0;568;0
WireConnection;1255;1;1256;0
WireConnection;1593;0;1583;0
WireConnection;5;0;329;0
WireConnection;5;1;3;0
WireConnection;5;2;1145;0
WireConnection;5;3;347;0
WireConnection;5;4;2126;0
WireConnection;765;0;609;0
WireConnection;765;1;610;0
WireConnection;765;2;612;0
WireConnection;1583;0;1587;0
WireConnection;1583;1;1584;0
WireConnection;2084;0;2083;0
WireConnection;1177;0;1176;0
WireConnection;1177;2;1178;0
WireConnection;853;0;851;0
WireConnection;853;1;852;0
WireConnection;853;2;850;0
WireConnection;1238;0;109;0
WireConnection;1238;1;1266;0
WireConnection;2088;0;2089;0
WireConnection;2088;2;2095;0
WireConnection;2144;0;382;0
WireConnection;382;0;381;0
WireConnection;1078;0;1208;0
WireConnection;1078;1;1077;0
WireConnection;1078;2;1080;0
WireConnection;329;0;341;0
WireConnection;329;1;2136;0
WireConnection;2114;0;2113;0
WireConnection;2124;0;2125;0
WireConnection;1908;0;1906;0
WireConnection;464;0;463;1
WireConnection;464;1;463;2
WireConnection;422;0;131;0
WireConnection;422;1;759;0
WireConnection;422;2;424;0
WireConnection;2099;0;1244;0
WireConnection;2099;1;2080;0
WireConnection;1597;0;1072;0
WireConnection;1597;1;1593;0
WireConnection;1172;0;1169;0
WireConnection;1172;1;1180;3
WireConnection;1172;2;1170;0
WireConnection;967;0;966;0
WireConnection;967;1;422;0
WireConnection;1907;0;1903;0
WireConnection;1074;0;1597;0
WireConnection;2090;0;464;0
WireConnection;590;0;591;0
WireConnection;961;0;959;0
WireConnection;973;0;976;0
WireConnection;973;1;422;0
WireConnection;782;0;650;1
WireConnection;782;1;651;0
WireConnection;782;2;783;0
WireConnection;1063;1;1069;0
WireConnection;1272;0;1273;0
WireConnection;1272;1;134;0
WireConnection;1272;2;414;0
WireConnection;1229;0;1230;0
WireConnection;1229;1;1225;0
WireConnection;1890;0;1532;0
WireConnection;1173;0;1180;4
WireConnection;1173;1;1171;0
WireConnection;1173;2;1168;0
WireConnection;1126;0;606;0
WireConnection;1905;0;1904;0
WireConnection;1905;1;1912;2
WireConnection;1905;2;1902;0
WireConnection;2089;0;2082;0
WireConnection;2089;1;2093;0
WireConnection;2089;2;2094;0
WireConnection;2120;0;2118;0
WireConnection;2120;1;2117;0
WireConnection;2120;2;2116;0
WireConnection;997;0;996;0
WireConnection;997;1;1000;0
WireConnection;1909;0;1905;0
WireConnection;1909;1;1912;3
WireConnection;1909;2;1907;0
WireConnection;2054;0;2053;0
WireConnection;2054;1;2058;0
WireConnection;456;0;459;2
WireConnection;1176;0;1175;0
WireConnection;1176;1;1179;0
WireConnection;451;0;453;0
WireConnection;451;1;461;0
WireConnection;451;2;1261;0
WireConnection;1072;0;1210;0
WireConnection;1072;1;1073;0
WireConnection;460;1;226;0
WireConnection;460;3;459;1
WireConnection;460;4;459;2
WireConnection;229;0;451;0
WireConnection;229;2;464;0
WireConnection;968;0;965;0
WireConnection;968;1;964;0
WireConnection;968;2;962;0
WireConnection;852;0;439;4
WireConnection;852;1;849;0
WireConnection;852;2;846;0
WireConnection;851;0;848;0
WireConnection;851;1;439;3
WireConnection;851;2;847;0
WireConnection;2113;0;2112;0
WireConnection;928;0;1207;0
WireConnection;928;1;94;0
WireConnection;2051;0;2043;0
WireConnection;1895;0;1892;0
WireConnection;345;0;330;0
WireConnection;345;1;346;0
WireConnection;2036;0;2130;0
WireConnection;1228;0;1226;1
WireConnection;1228;1;1;2
WireConnection;1228;2;1223;3
WireConnection;453;0;854;0
WireConnection;453;1;454;0
WireConnection;453;2;458;0
WireConnection;235;0;234;0
WireConnection;235;1;232;0
WireConnection;235;2;233;0
WireConnection;1598;0;1147;0
WireConnection;1210;0;1063;0
WireConnection;1210;1;1209;0
WireConnection;1579;0;1578;0
WireConnection;652;0;650;0
WireConnection;671;0;656;0
WireConnection;671;1;672;0
WireConnection;671;2;1621;0
WireConnection;458;0;854;0
WireConnection;458;1;457;0
WireConnection;2035;0;2034;0
WireConnection;854;0;226;0
WireConnection;854;1;853;0
WireConnection;2033;0;2032;0
WireConnection;855;0;460;0
WireConnection;855;1;853;0
WireConnection;599;0;2130;0
WireConnection;599;1;212;0
WireConnection;2034;0;2033;0
WireConnection;1892;0;1532;0
WireConnection;1531;0;1910;0
WireConnection;2057;0;2030;0
WireConnection;1894;0;1891;1
WireConnection;1894;1;1891;2
WireConnection;1894;2;1890;0
WireConnection;908;0;581;4
WireConnection;908;1;903;0
WireConnection;908;2;904;0
WireConnection;1903;0;1532;0
WireConnection;2091;0;2072;0
WireConnection;461;0;855;0
WireConnection;461;2;457;0
WireConnection;1900;0;1898;0
WireConnection;1900;1;1891;4
WireConnection;1900;2;1896;0
WireConnection;609;0;383;0
WireConnection;2121;0;2120;0
WireConnection;2121;2;2119;0
WireConnection;2146;0;2088;0
WireConnection;2146;2;2147;0
WireConnection;231;0;229;0
WireConnection;2031;0;2036;0
WireConnection;2032;0;2031;0
WireConnection;2032;1;2030;0
WireConnection;906;0;901;0
WireConnection;906;1;581;3
WireConnection;906;2;902;0
WireConnection;1068;0;1067;0
WireConnection;910;0;576;0
WireConnection;910;1;907;0
WireConnection;576;3;575;1
WireConnection;576;4;575;2
WireConnection;2056;0;2055;0
WireConnection;2060;0;2068;0
WireConnection;2060;2;2098;0
WireConnection;1267;0;106;0
WireConnection;971;0;968;0
WireConnection;1175;0;1172;0
WireConnection;1175;1;1173;0
WireConnection;1175;2;1174;0
WireConnection;650;0;589;0
WireConnection;2040;0;2051;0
WireConnection;579;0;575;3
WireConnection;579;1;575;4
WireConnection;1896;0;1893;0
WireConnection;909;0;186;0
WireConnection;909;1;907;0
WireConnection;1262;0;45;0
WireConnection;1901;0;1897;0
WireConnection;1904;0;1900;0
WireConnection;1904;1;1912;1
WireConnection;1904;2;1901;0
WireConnection;1223;1;1224;0
WireConnection;455;0;459;1
WireConnection;212;1;2128;0
WireConnection;1066;0;1065;0
WireConnection;1066;1;1064;0
WireConnection;1008;0;960;0
WireConnection;1008;1;1009;0
WireConnection;1008;2;1011;0
WireConnection;653;0;781;0
WireConnection;653;1;782;0
WireConnection;1899;0;1532;0
WireConnection;196;0;2142;0
WireConnection;2128;0;768;0
WireConnection;2128;1;2060;0
WireConnection;2128;2;2129;0
WireConnection;781;0;650;0
WireConnection;781;1;652;0
WireConnection;781;2;655;0
WireConnection;1587;1;1580;0
WireConnection;2081;1;2146;0
WireConnection;2126;0;2127;0
WireConnection;2126;1;2122;0
WireConnection;2126;2;2124;0
WireConnection;2109;0;2108;0
WireConnection;1898;0;1894;0
WireConnection;1898;1;1891;3
WireConnection;1898;2;1895;0
WireConnection;2112;0;2111;0
WireConnection;356;0;4;0
WireConnection;356;1;359;0
WireConnection;996;0;995;1
WireConnection;996;1;995;2
WireConnection;996;2;995;3
WireConnection;1240;0;1238;0
WireConnection;1240;1;130;0
WireConnection;383;0;2037;0
WireConnection;383;2;382;0
WireConnection;1264;0;1263;0
WireConnection;1264;1;1240;0
WireConnection;1897;0;1532;0
WireConnection;2134;0;2135;0
WireConnection;183;0;185;0
WireConnection;1145;0;356;0
WireConnection;1145;1;1135;0
WireConnection;1145;2;1146;0
WireConnection;2142;1;185;0
WireConnection;907;0;906;0
WireConnection;907;1;908;0
WireConnection;907;2;905;0
WireConnection;2078;0;2080;0
WireConnection;2078;1;132;0
WireConnection;109;0;913;0
WireConnection;109;1;1267;0
WireConnection;1226;1;1227;0
WireConnection;2052;0;916;0
WireConnection;580;0;581;1
WireConnection;580;1;581;2
WireConnection;1225;0;1213;1
WireConnection;1225;1;1213;2
WireConnection;582;0;909;0
WireConnection;582;1;579;0
WireConnection;767;0;609;1
WireConnection;767;1;611;0
WireConnection;767;2;766;0
WireConnection;2059;0;2056;0
WireConnection;2053;0;2052;0
WireConnection;1906;0;1532;0
WireConnection;1902;0;1899;0
WireConnection;1224;0;1229;0
WireConnection;1224;1;606;0
WireConnection;131;0;5;0
WireConnection;131;1;1242;0
WireConnection;131;2;1272;0
WireConnection;1227;0;606;0
WireConnection;1227;1;1229;0
WireConnection;2108;0;144;0
WireConnection;960;0;958;0
WireConnection;960;1;957;0
WireConnection;2143;0;2121;0
WireConnection;2143;2;2145;0
WireConnection;2087;0;2042;0
WireConnection;2087;1;2130;0
WireConnection;2087;2;2084;0
WireConnection;651;0;650;1
WireConnection;193;0;192;0
WireConnection;193;1;1533;0
WireConnection;193;2;195;0
WireConnection;589;0;188;0
WireConnection;589;2;590;0
WireConnection;1147;1;1177;0
WireConnection;1147;5;1149;0
WireConnection;1910;0;1909;0
WireConnection;1910;1;1912;4
WireConnection;1910;2;1908;0
WireConnection;1591;0;1599;0
WireConnection;2055;0;2054;0
WireConnection;2068;0;768;0
WireConnection;2068;1;2096;0
WireConnection;2068;2;2097;0
WireConnection;190;1;653;0
WireConnection;190;5;191;0
WireConnection;1266;0;1239;0
WireConnection;2138;2;928;0
WireConnection;2138;3;671;0
WireConnection;2138;5;182;0
ASEEND*/
//CHKSM=A4C02516286E7AEAF1A2125471C0DFB6A1C6DB05