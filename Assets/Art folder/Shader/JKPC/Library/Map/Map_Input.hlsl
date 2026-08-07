// =============================================================================
// JKPC - Map_Input.hlsl
// 棋盘输入层 — 别名指向通用顶点着色器
// Layer 2 — Library/Map/
// =============================================================================

#ifndef JKPC_MAP_INPUT_HLSL_INCLUDED
#define JKPC_MAP_INPUT_HLSL_INCLUDED

#include "Assets/Art folder/Shader/JKPC/Include/Input.hlsl"
#include "Assets/Art folder/Shader/JKPC/Include/Utility.hlsl"

// 顶点着色器别名 — 实现在 Include/Input.hlsl (DRY)
#define Map_Vert JKPC_Vert

#endif // JKPC_MAP_INPUT_HLSL_INCLUDED
