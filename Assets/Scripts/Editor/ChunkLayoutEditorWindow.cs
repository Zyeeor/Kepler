using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Chunk 固定布局手摆工具（菜单 Kepler/Map/Chunk Layout Editor，2026-08 双模式方案；2026-08-14 配置重构）。
/// 左侧：刷子面板——扫描 TileAsset 目录下全部 prefab + 橡皮擦；
/// 右侧：16×16 网格涂刷（点击/拖拽，按推导类别着色：Normal 绿 / Trigger 橙 / Decoration 红 / 空灰）。
/// 布局紧凑序列化（决策 1）：每格仅记录 prefab 引用，玩法语义（触发逻辑/碰撞）由 prefab 自带组件推导。
/// 显示约定：顶行 = y=15（北在上），与地图本地坐标一致（x→右，y→上）。
/// 资产变更走 Undo.RecordObject + EditorUtility.SetDirty（支持撤销、随项目保存落盘）。
/// </summary>
/// <summary>
/// 资产后处理：MapModules 下 prefab 增/删/改时自动刷新打开的 Chunk Layout Editor 刷子面板，
/// 免去手动点"刷新 Tile 列表"。
/// </summary>
public class ChunkLayoutEditorPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        // 仅关注 MapModules 目录下的 prefab 变化（增删改任一）
        bool relevant = false;
        foreach (var p in importedAssets)
            if (p.StartsWith("Assets/Prefabs/MapModules/") && p.EndsWith(".prefab")) { relevant = true; break; }
        if (!relevant)
            foreach (var p in deletedAssets)
                if (p.StartsWith("Assets/Prefabs/MapModules/") && p.EndsWith(".prefab")) { relevant = true; break; }
        if (!relevant)
            foreach (var p in movedAssets)
                if (p.StartsWith("Assets/Prefabs/MapModules/") && p.EndsWith(".prefab")) { relevant = true; break; }
        if (!relevant) return;

        var wins = Resources.FindObjectsOfTypeAll<ChunkLayoutEditorWindow>();
        foreach (var w in wins)
            if (w != null) { w.RefreshPalette(); w.Repaint(); }
    }
}

public class ChunkLayoutEditorWindow : EditorWindow
{
    /// <summary>格子像素（可缩放，滑块 24~72）。</summary>
    float cellSize = 48f;
    const float MinCell = 24f, MaxCell = 72f;
    const float PaletteWidth = 300f;

    /// <summary>目标布局资产（涂刷直接修改资产本身）。</summary>
    FixedChunkLayout target;
    /// <summary>当前刷子（null = 橡皮擦，刷空）。</summary>
    GameObject brush;
    Vector2 paletteScroll;
    Vector2 gridScroll;
    /// <summary>刷子候选：TileAsset 目录下全部 Tile prefab（类别经 TileSemantics 推导）。</summary>
    readonly List<GameObject> palette = new List<GameObject>();
    GUIStyle cellStyle;

    [MenuItem("Kepler/Map/Chunk Layout Editor")]
    static void Open()
    {
        var w = GetWindow<ChunkLayoutEditorWindow>("Chunk Layout Editor");
        w.minSize = new Vector2(PaletteWidth + 8 * w.cellSize + 80f, 8 * w.cellSize + 160f);
        w.Show();
    }

    void OnEnable()
    {
        RefreshPalette();
        // 项目窗口选中布局资产时自动接管为目标
        if (target == null && Selection.activeObject is FixedChunkLayout sel) target = sel;
    }

    void OnSelectionChange()
    {
        if (Selection.activeObject is FixedChunkLayout sel && sel != target)
        {
            target = sel;
            Repaint();
        }
    }

    /// <summary>
    /// 扫描 Tile 资产目录下的全部 prefab 构建刷子面板（按名称排序，稳定显示）。
    /// 类别由 TileSemantics 推导（无需组件标记）。
    /// public：供 ChunkLayoutEditorPostprocessor（资产变化自动刷新）调用。
    /// </summary>
    public void RefreshPalette()
    {
        palette.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Room/RoomObjects/TileAsset" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null) continue;
            palette.Add(prefab);
        }
        palette.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
    }

    void OnGUI()
    {
        // 窗口最小尺寸随布局边长 + 格子缩放动态调整（target 变化时网格重新适配）
        int curSize = target != null ? target.size : 8;
        minSize = new Vector2(PaletteWidth + curSize * cellSize + 80f, curSize * cellSize + 180f);

        DrawToolbar();
        if (target == null)
        {
            EditorGUILayout.HelpBox("请选择或新建一个 FixedChunkLayout 资产（项目窗口选中即自动接管）。", MessageType.Info);
            return;
        }
        EditorGUILayout.BeginHorizontal();
        DrawPalette();
        DrawGrid();
        EditorGUILayout.EndHorizontal();
        DrawFooter();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        target = (FixedChunkLayout)EditorGUILayout.ObjectField(target, typeof(FixedChunkLayout), false, GUILayout.Width(280f));
        if (GUILayout.Button("新建布局", EditorStyles.toolbarButton, GUILayout.Width(70f))) CreateNewLayout();
        if (GUILayout.Button("刷新 Tile 列表", EditorStyles.toolbarButton, GUILayout.Width(110f))) RefreshPalette();
        GUILayout.FlexibleSpace();
        // 缩放控件
        EditorGUILayout.LabelField("缩放", EditorStyles.miniLabel, GUILayout.Width(30f));
        if (GUILayout.Button("−", EditorStyles.toolbarButton, GUILayout.Width(22f))) SetCellSize(cellSize - 6f);
        cellSize = EditorGUILayout.Slider(cellSize, MinCell, MaxCell, GUILayout.Width(120f));
        if (GUILayout.Button("＋", EditorStyles.toolbarButton, GUILayout.Width(22f))) SetCellSize(cellSize + 6f);
        EditorGUILayout.LabelField($"{target?.size ?? 8}×{target?.size ?? 8}", EditorStyles.miniLabel, GUILayout.Width(36f));
        if (target != null)
            EditorGUILayout.LabelField($"刷子：{(brush != null ? brush.name : "橡皮擦")}", EditorStyles.miniLabel, GUILayout.Width(150f));
        EditorGUILayout.EndHorizontal();
    }

    void SetCellSize(float v)
    {
        cellSize = Mathf.Clamp(v, MinCell, MaxCell);
        Repaint();
    }

    void DrawPalette()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(PaletteWidth));
        EditorGUILayout.LabelField("Tile 刷子（TileAsset 目录全部 prefab）", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("  绿=地面 橙=触发 红=装饰 灰=空（同类自动区分深浅）", EditorStyles.miniLabel);
        // 刷子滚动区固定高度，避免挤压右侧网格
        paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll, GUILayout.Height(480f));
        DrawBrushButton(null, "橡皮擦（刷空）");
        for (int i = 0; i < palette.Count; i++)
            DrawBrushButton(palette[i], BrushLabel(palette[i]));
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    void DrawBrushButton(GameObject prefab, string label)
    {
        bool selected = brush == prefab;
        var oldColor = GUI.backgroundColor;
        if (selected) GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
        if (GUILayout.Button(label, GUILayout.Height(26f))) brush = prefab;
        GUI.backgroundColor = oldColor;
        // 左侧色条（与格子同色，便于对照）
        var r = GUILayoutUtility.GetLastRect();
        EditorGUI.DrawRect(new Rect(r.x, r.y, 4f, r.height), CellColor(prefab));
    }

    /// <summary>坐标轴标签样式。</summary>
    GUIStyle axisStyle;

    void DrawGrid()
    {
        int size = target != null ? target.size : 8;
        float gridPx = size * cellSize;
        // 左侧坐标轴 + 网格总宽高
        const float axisW = 24f, axisH = 20f;
        float totalW = axisW + gridPx, totalH = axisH + gridPx;
        gridScroll = EditorGUILayout.BeginScrollView(gridScroll);
        var area = GUILayoutUtility.GetRect(totalW, totalH, GUILayout.Width(totalW), GUILayout.Height(totalH));
        var axisStyle = AxisStyle();

        // ── 坐标轴标签（装饰层，不参与涂刷命中） ──
        // 顶部 y 轴（北在上：左 → 右 = y=size-1 → 0）
        for (int i = 0; i < size; i++)
        {
            var r = new Rect(area.x + axisW + i * cellSize, area.y, cellSize, axisH);
            GUI.Label(r, (size - 1 - i).ToString(), axisStyle);
        }
        // 左侧 x 轴（北在上：上 → 下 = y=size-1 → 0）
        for (int j = 0; j < size; j++)
        {
            var r = new Rect(area.x, area.y + axisH + j * cellSize, axisW, cellSize);
            GUI.Label(r, (size - 1 - j).ToString(), axisStyle);
        }

        // 网格实际区域（坐标轴内）
        var gridRect = new Rect(area.x + axisW, area.y + axisH, gridPx, gridPx);

        // ── 涂刷事件 ──
        var e = Event.current;
        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && gridRect.Contains(e.mousePosition))
        {
            int cx = Mathf.FloorToInt((e.mousePosition.x - gridRect.x) / cellSize);
            int cy = size - 1 - Mathf.FloorToInt((e.mousePosition.y - gridRect.y) / cellSize); // 顶行 = y=size-1
            if (cx >= 0 && cy >= 0 && cx < size && cy < size) Paint(cx, cy);
            e.Use();
        }

        // ── 格子绘制 ──
        if (cellStyle == null)
            cellStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(cellSize * 0.4f) };
        int hoverX = -1, hoverY = -1;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            var r = CellRect(gridRect, x, y);
            var prefab = target.GetTile(x, y);
            if (prefab == null)
            {
                // 空格：灰白棋盘格，便于看清格位
                EditorGUI.DrawRect(r, ((x + y) % 2 == 0) ? new Color(0.30f, 0.30f, 0.30f) : new Color(0.24f, 0.24f, 0.24f));
            }
            else
            {
                EditorGUI.DrawRect(r, CellColor(prefab));
            }
            // 悬停检测（记录格坐标供信息面板）
            if (r.Contains(e.mousePosition)) { hoverX = x; hoverY = y; }
            if (prefab != null) GUI.Label(r, Abbrev(prefab), cellStyle);
        }
        // 悬停格描边高亮
        if (hoverX >= 0)
        {
            var hr = CellRect(gridRect, hoverX, hoverY);
            EditorGUI.DrawRect(hr, Color.white);
            var prefab = target.GetTile(hoverX, hoverY);
            if (prefab != null)
                EditorGUI.DrawRect(new Rect(hr.x + 2f, hr.y + 2f, hr.width - 4f, hr.height - 4f), CellColor(prefab));
            hoveredCell = new Vector2Int(hoverX, hoverY);
        }
        else hoveredCell = new Vector2Int(-1, -1);
        EditorGUILayout.EndScrollView();
        DrawCellInfo();
    }

    GUIStyle AxisStyle()
    {
        if (axisStyle == null)
            axisStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        return axisStyle;
    }

    /// <summary>当前悬停格（-1 表示无）。</summary>
    Vector2Int hoveredCell = new Vector2Int(-1, -1);

    /// <summary>信息面板：当前悬停格的坐标 + prefab + 推导语义摘要。</summary>
    void DrawCellInfo()
    {
        EditorGUILayout.Space(4f);
        if (hoveredCell.x < 0)
        {
            EditorGUILayout.HelpBox("悬停网格查看格子配置（坐标 / prefab / 推导类别）。", MessageType.None);
            return;
        }
        var prefab = target.GetTile(hoveredCell.x, hoveredCell.y);
        var sb = new StringBuilder($"({hoveredCell.x}, {hoveredCell.y}) ");
        sb.Append(prefab != null ? prefab.name : "空");
        if (prefab != null)
        {
            sb.Append("\n  kind=").Append(TileSemantics.ResolveKind(prefab))
              .Append("（推导）")
              .Append("\n  solidCollider=").Append(TileSemantics.HasSolidCollider(prefab));
        }
        EditorGUILayout.HelpBox(sb.ToString(), MessageType.Info);
    }

    /// <summary>格子屏幕 rect（y 翻转：顶行显示 y=size-1，北在上）；内缩 1px 形成网格线。</summary>
    Rect CellRect(Rect gridRect, int x, int y)
    {
        int size = target != null ? target.size : 8;
        return new Rect(gridRect.x + x * cellSize + 1f, gridRect.y + (size - 1 - y) * cellSize + 1f, cellSize - 2f, cellSize - 2f);
    }

    void Paint(int x, int y)
    {
        if (target.GetTile(x, y) == brush) return; // 无变化不记 Undo
        Undo.RecordObject(target, "涂刷 Chunk 布局 Tile");
        target.SetTile(x, y, brush);
        EditorUtility.SetDirty(target);
        Repaint();
    }

    void DrawFooter()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("清空全部", GUILayout.Width(100f), GUILayout.Height(24f))) ClearAll();
        EditorGUILayout.EndHorizontal();
    }

    void ClearAll()
    {
        if (target == null) return;
        int size = target.size;
        if (!EditorUtility.DisplayDialog("清空全部", $"确定清空布局 '{target.name}' 的全部 {size * size} 格？", "清空", "取消")) return;
        Undo.RecordObject(target, "清空 Chunk 布局");
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
            target.SetTile(x, y, null);
        EditorUtility.SetDirty(target);
        Repaint();
    }

    void CreateNewLayout()
    {
        var path = EditorUtility.SaveFilePanelInProject("新建 Chunk 布局", "FixedChunkLayout", "asset",
            "选择布局资产保存位置", "Assets/Settings/MapStreaming/Layouts");
        if (string.IsNullOrEmpty(path)) return;
        var layout = CreateInstance<FixedChunkLayout>();
        AssetDatabase.CreateAsset(layout, path);
        AssetDatabase.SaveAssets();
        target = layout;
        Repaint();
    }

    // ── prefab 语义标签（推导：kind / solidCollider，无组件配置） ──

    /// <summary>
    /// 格子缩写（清晰标记）：[kind 字母][prefab 名首个数字或尾词首字母]。
    ///  kind 字母：地面 G / 触发 T / 装饰 D / 未知 ?；
    ///  变体：prefab 名含数字取数字（Brick01→G1），否则取最后一个单词首字母（lava→L、Spike_Trap→S）。
    /// </summary>
    static string Abbrev(GameObject prefab)
    {
        if (prefab == null) return "";
        string name = prefab.name;
        // ① kind 字母（推导分类）
        char kindCh;
        switch (TileSemantics.ResolveKind(prefab))
        {
            case TerrainKind.Trigger: kindCh = 'T'; break;
            case TerrainKind.Decoration: kindCh = 'D'; break;
            default: kindCh = 'G'; break; // Normal 地面
        }
        // ② 变体：取 prefab 名中的"连续数字串"（Brick01→"1"、Brick12→"12"）；无有效数字或为 0 则取尾词首字母
        string variant = "";
        var digits = System.Text.RegularExpressions.Regex.Match(name, @"\d+");
        if (digits.Success && digits.Value != "0")
            variant = digits.Value;
        if (variant.Length == 0)
        {
            var words = name.Split('_', ' ', '-');
            for (int i = words.Length - 1; i >= 0; i--)
                if (words[i].Length > 0 && char.IsLetter(words[i][0]))
                {
                    variant = words[i][0].ToString();
                    break;
                }
        }
        return kindCh + variant;
    }

    /// <summary>刷子标签：prefab 名 + [推导 kind, 有碰撞?]。</summary>
    static string BrushLabel(GameObject prefab)
    {
        string name = prefab != null ? prefab.name : "?";
        var sb = new StringBuilder(name);
        if (prefab != null)
        {
            sb.Append("  [").Append(TileSemantics.ResolveKind(prefab));
            if (TileSemantics.HasSolidCollider(prefab)) sb.Append(", 有碰撞");
            sb.Append(']');
        }
        return sb.ToString();
    }

    /// <summary>
    /// 格子颜色：同类地块自动着色，确保同 kind 不同 prefab 差异明显（不靠自定义）。
    /// 核心：直接解析 prefab 名中的数字变体 n（Brick03→3），用"黄金分割散列"把 n 映射到
    /// 色相圆周上充分散开——1/2/3/4/5/6 必然彼此远离，肉眼可分（不再用 hash 碰运气）。
    /// </summary>
    static Color CellColor(GameObject prefab)
    {
        if (prefab == null) return new Color(0.25f, 0.25f, 0.25f); // 空格：灰

        // ① 数字变体 n（Brick03→3、Base→0、lava→0）；黄金分割散列保证相邻 n 充分散开
        int n = VariantNumber(prefab);
        float phi = 0.61803398875f;
        float t = (n * phi) % 1f; // [0,1) 均匀散布（1→0.618, 2→0.236, 3→0.854, 4→0.472, 5→0.09, 6→0.708 …）

        // ② kind 决定基础色相中心 + 色相可偏移范围（推导分类）
        float hBase, hRange, sMin;
        switch (TileSemantics.ResolveKind(prefab))
        {
            case TerrainKind.Trigger: hBase = 0.07f; hRange = 0.06f; sMin = 0.85f; break; // 橙
            case TerrainKind.Decoration: hBase = 0.02f; hRange = 0.10f; sMin = 0.7f; break; // 红/粉
            default: hBase = 0.33f; hRange = 0.22f; sMin = 0.5f; break; // 绿 → 黄/青 更宽范围
        }
        // 色相在 kind 范围内按 n 均匀展开；明度/饱和度也随 n 交替变化，双维度保证可区分
        float h = (hBase - hRange * 0.5f + t * hRange + 1f) % 1f;
        float s = Mathf.Clamp01(sMin + 0.4f * (((n + 1) * phi) % 1f - 0.5f));
        float l = Mathf.Clamp01(0.38f + 0.44f * (((n + 3) * phi) % 1f));
        return Color.HSVToRGB(h, s, l);
    }

    /// <summary>prefab 名中的数字变体（Brick03→3、Brick12→12）；无数字返回 0（如 Base、lava）。</summary>
    static int VariantNumber(GameObject prefab)
    {
        string name = prefab != null ? prefab.name : "";
        var m = System.Text.RegularExpressions.Regex.Match(name, @"\d+");
        return m.Success ? int.Parse(m.Value) : 0;
    }

}
