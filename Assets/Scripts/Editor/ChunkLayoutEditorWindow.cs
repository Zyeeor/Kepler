using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Chunk 固定布局手摆工具（菜单 Kepler/Map/Chunk Layout Editor）。
/// 双层编辑（对齐双层 Tile 模型）：
///   - 底层地砖 tiles[]（Normal/Trigger）+ 可选叠加装饰物 overlayTiles[]（Decoration/神龛）。
///   - 编辑时按"刷子 prefab 的推导类别"自动归层：地砖类刷子 → 写底层；装饰物刷子 → 写叠加层（无需手动切层）。
///   - 旧格式（装饰物整格摆在底层字段）显示时自动镜像运行时解析：默认地块兜底 + 装饰物转叠加，所见即所得。
/// 直观显示：用 AssetPreview 缩略图代替纯色块；叠加层在格子右上角小窗叠加；"默认地块预览"开关让兜底地砖可见。
/// 左侧：刷子面板（按推导类别分组：地砖 / 装饰物，每项带缩略图）；右侧：N×N 网格涂刷。
/// 显示约定：顶行 = y=size-1（北在上），与地图本地坐标一致。资产变更走 Undo.RecordObject + EditorUtility.SetDirty。
/// </summary>
public class ChunkLayoutEditorPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        bool relevant = false;
        foreach (var p in importedAssets)
            if (p.StartsWith("Assets/Prefabs/Room/RoomObjects/TileAsset") && p.EndsWith(".prefab")) { relevant = true; break; }
        if (!relevant)
            foreach (var p in deletedAssets)
                if (p.StartsWith("Assets/Prefabs/Room/RoomObjects/TileAsset") && p.EndsWith(".prefab")) { relevant = true; break; }
        if (!relevant)
            foreach (var p in movedAssets)
                if (p.StartsWith("Assets/Prefabs/Room/RoomObjects/TileAsset") && p.EndsWith(".prefab")) { relevant = true; break; }
        if (!relevant) return;

        var wins = Resources.FindObjectsOfTypeAll<ChunkLayoutEditorWindow>();
        foreach (var w in wins)
            if (w != null) { w.RefreshPalette(); w.Repaint(); }
    }
}

public class ChunkLayoutEditorWindow : EditorWindow
{
    /// <summary>当前编辑的层（决定橡皮擦清除哪一层；涂刷按刷子类别自动归层，与此无关）。</summary>
    enum EditLayer { Base = 0, Overlay = 1 }

    /// <summary>格子像素（可缩放，滑块 24~72）。</summary>
    float cellSize = 52f;
    const float MinCell = 24f, MaxCell = 72f;
    const float PaletteWidth = 320f;

    /// <summary>目标布局资产（涂刷直接修改资产本身）。</summary>
    FixedChunkLayout target;
    /// <summary>当前刷子（null = 橡皮擦，刷空当前层）。</summary>
    GameObject brush;
    Vector2 paletteScroll;
    Vector2 gridScroll;
    /// <summary>刷子候选：TileAsset 目录下全部 Tile prefab（类别经 TileSemantics 推导）。</summary>
    readonly List<GameObject> palette = new List<GameObject>();
    GUIStyle cellStyle;
    GUIStyle axisStyle;

    EditLayer editLayer = EditLayer.Base;
    bool multiCellDecorationMode;
    Vector2Int multiCellFootprint = Vector2Int.one;
    bool showPreview = true;
    bool showDefaultGroundPreview = true;
    /// <summary>AssetPreview 异步加载中标记：有任意预览未就绪则下一帧重绘。</summary>
    bool needRepaint = false;

    [MenuItem("Kepler/Map/Chunk Layout Editor")]
    static void Open()
    {
        var w = GetWindow<ChunkLayoutEditorWindow>("Chunk Layout Editor");
        w.minSize = new Vector2(PaletteWidth + 8 * w.cellSize + 80f, 8 * w.cellSize + 200f);
        w.Show();
    }

    void OnEnable()
    {
        // 关键：默认 EditorWindow 不接收 MouseMove 事件，悬停高亮不会刷新。开启后鼠标移动才会派发事件并触发重绘。
        wantsMouseMove = true;
        RefreshPalette();
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

    /// <summary>扫描 Tile 资产目录下的全部 prefab 构建刷子面板（按名称排序）。类别由 TileSemantics 推导。</summary>
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
        int curSize = target != null ? target.size : 8;
        minSize = new Vector2(PaletteWidth + curSize * cellSize + 80f, curSize * cellSize + 220f);

        DrawToolbar();
        if (target == null)
        {
            EditorGUILayout.HelpBox("请选择或新建一个 FixedChunkLayout 资产（项目窗口选中即自动接管）。", MessageType.Info);
            return;
        }
        DrawOptionsBar();
        EditorGUILayout.BeginHorizontal();
        DrawPalette();
        DrawGrid();
        EditorGUILayout.EndHorizontal();
        DrawFooter();

        if (needRepaint) { needRepaint = false; Repaint(); }
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        target = (FixedChunkLayout)EditorGUILayout.ObjectField(target, typeof(FixedChunkLayout), false, GUILayout.Width(280f));
        if (GUILayout.Button("新建布局", EditorStyles.toolbarButton, GUILayout.Width(70f))) CreateNewLayout();
        if (GUILayout.Button("刷新 Tile 列表", EditorStyles.toolbarButton, GUILayout.Width(110f))) RefreshPalette();
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("缩放", EditorStyles.miniLabel, GUILayout.Width(30f));
        if (GUILayout.Button("−", EditorStyles.toolbarButton, GUILayout.Width(22f))) SetCellSize(cellSize - 6f);
        cellSize = EditorGUILayout.Slider(cellSize, MinCell, MaxCell, GUILayout.Width(120f));
        if (GUILayout.Button("＋", EditorStyles.toolbarButton, GUILayout.Width(22f))) SetCellSize(cellSize + 6f);
        EditorGUILayout.LabelField($"{target?.size ?? 8}×{target?.size ?? 8}", EditorStyles.miniLabel, GUILayout.Width(36f));
        if (target != null)
            EditorGUILayout.LabelField($"刷子：{(brush != null ? brush.name : "橡皮擦")}", EditorStyles.miniLabel, GUILayout.Width(150f));
        EditorGUILayout.EndHorizontal();
    }

    void DrawOptionsBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("编辑层", EditorStyles.miniLabel, GUILayout.Width(44f));
        if (GUILayout.Toggle(editLayer == EditLayer.Base, "底层地砖", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            editLayer = EditLayer.Base;
        if (GUILayout.Toggle(editLayer == EditLayer.Overlay, "叠加装饰物", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            editLayer = EditLayer.Overlay;
        if (editLayer == EditLayer.Overlay)
        {
            multiCellDecorationMode = GUILayout.Toggle(multiCellDecorationMode, "多格放置", EditorStyles.toolbarButton, GUILayout.Width(76f));
            if (multiCellDecorationMode)
            {
                EditorGUILayout.LabelField("尺寸", EditorStyles.miniLabel, GUILayout.Width(28f));
                multiCellFootprint.x = Mathf.Max(1, EditorGUILayout.IntField(multiCellFootprint.x, GUILayout.Width(28f)));
                EditorGUILayout.LabelField("×", EditorStyles.miniLabel, GUILayout.Width(10f));
                multiCellFootprint.y = Mathf.Max(1, EditorGUILayout.IntField(multiCellFootprint.y, GUILayout.Width(28f)));
            }
        }
        GUILayout.Space(8f);
        showPreview = GUILayout.Toggle(showPreview, "缩略图", EditorStyles.toolbarButton, GUILayout.Width(70f));
        showDefaultGroundPreview = GUILayout.Toggle(showDefaultGroundPreview, "默认地块预览", EditorStyles.toolbarButton, GUILayout.Width(100f));
        GUILayout.FlexibleSpace();
        EditorGUI.BeginChangeCheck();
        target.defaultGround = (GameObject)EditorGUILayout.ObjectField("默认地块", target.defaultGround, typeof(GameObject), false, GUILayout.Width(240f));
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(target, "设默认地块");
            EditorUtility.SetDirty(target);
        }
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
        EditorGUILayout.LabelField("Tile 刷子（点击即按类别落层）", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("  地砖类→底层 ｜ 装饰物类→叠加层", EditorStyles.miniLabel);
        paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll, GUILayout.Height(460f));
        DrawBrushButton(null, "橡皮擦（刷空）");
        EditorGUILayout.LabelField("— 地砖（底层） —", EditorStyles.miniLabel);
        for (int i = 0; i < palette.Count; i++)
            if (TileSemantics.ResolveKind(palette[i]) != TerrainKind.Decoration)
                DrawBrushButton(palette[i], BrushLabel(palette[i]));
        EditorGUILayout.LabelField("— 装饰物（叠加层） —", EditorStyles.miniLabel);
        for (int i = 0; i < palette.Count; i++)
            if (TileSemantics.ResolveKind(palette[i]) == TerrainKind.Decoration)
                DrawBrushButton(palette[i], BrushLabel(palette[i]));
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    void DrawBrushButton(GameObject prefab, string label)
    {
        bool selected = brush == prefab;
        var oldColor = GUI.backgroundColor;
        if (selected) GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
        EditorGUILayout.BeginHorizontal();
        // 缩略图（或色块兜底）
        var r0 = GUILayoutUtility.GetRect(24f, 24f, GUILayout.Width(26f), GUILayout.Height(26f));
        var prev = showPreview ? GetPreview(prefab) : null;
        if (prev != null) GUI.DrawTexture(r0, prev, ScaleMode.ScaleToFit);
        else EditorGUI.DrawRect(r0, CellColor(prefab));
        if (GUILayout.Button(label, GUILayout.Height(26f))) brush = prefab;
        EditorGUILayout.EndHorizontal();
        GUI.backgroundColor = oldColor;
    }

    void DrawGrid()
    {
        int size = target != null ? target.size : 8;
        float gridPx = size * cellSize;
        const float axisW = 24f, axisH = 20f;
        float totalW = axisW + gridPx, totalH = axisH + gridPx;
        gridScroll = EditorGUILayout.BeginScrollView(gridScroll);
        var area = GUILayoutUtility.GetRect(totalW, totalH, GUILayout.Width(totalW), GUILayout.Height(totalH));
        var axStyle = AxisStyle();

        // 坐标轴标签（顶行 = y=size-1）
        for (int i = 0; i < size; i++)
        {
            var r = new Rect(area.x + axisW + i * cellSize, area.y, cellSize, axisH);
            GUI.Label(r, (size - 1 - i).ToString(), axStyle);
        }
        for (int j = 0; j < size; j++)
        {
            var r = new Rect(area.x, area.y + axisH + j * cellSize, axisW, cellSize);
            GUI.Label(r, (size - 1 - j).ToString(), axStyle);
        }

        var gridRect = new Rect(area.x + axisW, area.y + axisH, gridPx, gridPx);

        // 鼠标移动时持续重绘，使悬停高亮跟随光标
        var e = Event.current;
        if (e.type == EventType.MouseMove) Repaint();
        // scroll view 内 e.mousePosition 为视口坐标，需加回滚动偏移得到 content 坐标
        var mp = e.mousePosition + gridScroll;

        // 涂刷 / 擦除事件（左键涂刷当前刷子，右键擦整格两层）
        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && gridRect.Contains(mp))
        {
            int cx = Mathf.FloorToInt((mp.x - gridRect.x) / cellSize);
            int cy = size - 1 - Mathf.FloorToInt((mp.y - gridRect.y) / cellSize);
            if (cx >= 0 && cy >= 0 && cx < size && cy < size)
            {
                if (e.button == 0)
                {
                    if (multiCellDecorationMode && editLayer == EditLayer.Overlay
                        && brush != null && TileSemantics.ResolveKind(brush) == TerrainKind.Decoration
                        && (multiCellFootprint.x > 1 || multiCellFootprint.y > 1))
                        PaintMultiCellDecoration(cx, cy);
                    else
                        Paint(cx, cy);
                }
                else if (e.button == 1) EraseCell(cx, cy);
                e.Use();
            }
        }

        if (cellStyle == null)
            cellStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(cellSize * 0.32f) };

        int hoverX = -1, hoverY = -1;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            var r = CellRect(gridRect, x, y);
            ResolveCell(x, y, out var ground, out var overlay, out var groundIsDefault);
            var basePrefab = target.GetTile(x, y);

            // 底色（按底层推导类别着色；无预览时仅靠色块区分）
            EditorGUI.DrawRect(r, CellColor(basePrefab ?? ground));
            if (r.Contains(mp)) { hoverX = x; hoverY = y; }

            if (showPreview)
            {
                var groundPrev = GetPreview(ground);
                if (groundPrev != null)
                {
                    var dest = new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f);
                    GUI.DrawTexture(dest, groundPrev, ScaleMode.ScaleToFit);
                }
                // 旧格式装饰物整格但默认地块未配置：画占位提示（运行时仍会兜底 ChunkDef.normalTiles[0]）
                if (ground == null && basePrefab != null && TileSemantics.ResolveKind(basePrefab) == TerrainKind.Decoration)
                {
                    EditorGUI.DrawRect(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), new Color(0.6f, 0.5f, 0.1f, 0.3f));
                    GUI.Label(new Rect(r.x + 2f, r.y + 2f, 22f, 14f), "默?", cellStyle);
                }
                if (overlay != null)
                {
                    var ovPrev = GetPreview(overlay);
                    var oRect = new Rect(r.x + r.width * 0.5f, r.y, r.width * 0.5f, r.height * 0.5f);
                    if (ovPrev != null) GUI.DrawTexture(oRect, ovPrev, ScaleMode.ScaleToFit);
                    DrawRectOutline(oRect, new Color(1f, 0.85f, 0.2f, 0.95f), 2f); // 叠加层角标：仅描边，不遮挡缩略图
                    if (basePrefab == null || TileSemantics.ResolveKind(basePrefab) == TerrainKind.Decoration)
                        GUI.Label(oRect, "叠", cellStyle);
                }
                // 默认地块兜底遮罩提示
                if (groundIsDefault && showDefaultGroundPreview)
                {
                    var mask = new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f);
                    EditorGUI.DrawRect(mask, new Color(0.5f, 0.5f, 0.5f, 0.35f));
                    GUI.Label(new Rect(r.x + 2f, r.y + 2f, 18f, 14f), "默", cellStyle);
                }
            }
            else
            {
                // 无预览：色块 + 缩写
                if (basePrefab != null) GUI.Label(r, Abbrev(basePrefab), cellStyle);
                else if (overlay != null) GUI.Label(r, "叠", cellStyle);
            }
        }

        // 新式多格装饰 placement：整组预览一次并绘制 footprint 边界，避免被误看成多个单格装饰。
        if (target.decorationPlacements != null)
        {
            foreach (var placement in target.decorationPlacements)
            {
                if (placement == null || placement.prefab == null) continue;
                var placementSize = placement.SafeFootprintSize;
                var first = CellRect(gridRect, placement.anchor.x, placement.anchor.y);
                var last = CellRect(gridRect, placement.anchor.x + placementSize.x - 1, placement.anchor.y + placementSize.y - 1);
                var placementArea = Rect.MinMaxRect(first.x, last.y, last.xMax, first.yMax);
                if (showPreview)
                {
                    var preview = GetPreview(placement.prefab);
                    if (preview != null)
                        GUI.DrawTexture(new Rect(placementArea.x + 3f, placementArea.y + 3f, placementArea.width - 6f, placementArea.height - 6f), preview, ScaleMode.ScaleToFit);
                }
                DrawRectOutline(placementArea, new Color(1f, 0.35f, 0.1f, 0.95f), 2f);
                GUI.Label(new Rect(placementArea.x + 3f, placementArea.y + 2f, 48f, 16f), $"{placementSize.x}×{placementSize.y}", cellStyle);
            }
        }

        if (hoverX >= 0)
        {
            var hr = CellRect(gridRect, hoverX, hoverY);
            EditorGUI.DrawRect(hr, Color.white);
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

    /// <summary>信息面板：镜像运行时解析，显示底层（含默认地块兜底标记）+ 叠加 + 推导语义。</summary>
    void DrawCellInfo()
    {
        EditorGUILayout.Space(4f);
        if (hoveredCell.x < 0)
        {
            EditorGUILayout.HelpBox("悬停网格查看格子配置（底层 / 叠加 / 推导语义）。", MessageType.None);
            return;
        }
        var baseP = target.GetTile(hoveredCell.x, hoveredCell.y);
        var ov = target.GetOverlay(hoveredCell.x, hoveredCell.y);
        var placement = FindPlacementAt(hoveredCell.x, hoveredCell.y);
        ResolveCell(hoveredCell.x, hoveredCell.y, out var ground, out var overlay, out var gDefault);

        var sb = new StringBuilder($"({hoveredCell.x}, {hoveredCell.y})\n");
        sb.Append("底层：");
        if (baseP == null && ov == null) sb.Append("空（普通地面）");
        else if (ground != null)
        {
            if (gDefault) sb.Append("[默认地块兜底] ");
            sb.Append(ground.name)
              .Append("\n  kind=").Append(TileSemantics.ResolveKind(ground))
              .Append(" solidCollider=").Append(TileSemantics.HasSolidCollider(ground));
        }
        else sb.Append("空");
        sb.Append("\n叠加：");
        if (placement != null)
            sb.Append(placement.prefab != null ? placement.prefab.name : "空")
              .Append($"\n  footprint={placement.SafeFootprintSize.x}×{placement.SafeFootprintSize.y} anchor={placement.anchor}")
              .Append("\n  kind=").Append(placement.prefab != null ? TileSemantics.ResolveKind(placement.prefab) : TerrainKind.Normal)
              .Append(" solidCollider=").Append(placement.prefab != null && TileSemantics.HasSolidCollider(placement.prefab));
        else if (overlay != null)
            sb.Append(overlay.name)
              .Append("\n  kind=").Append(TileSemantics.ResolveKind(overlay))
              .Append(" solidCollider=").Append(TileSemantics.HasSolidCollider(overlay));
        else sb.Append("无");
        if (baseP != null && TileSemantics.ResolveKind(baseP) == TerrainKind.Decoration)
            sb.Append("\n（旧格式：装饰物整格将自动拆为 默认地块 + 叠加）");
        EditorGUILayout.HelpBox(sb.ToString(), MessageType.Info);
    }

    /// <summary>格子屏幕 rect（y 翻转：顶行显示 y=size-1，北在上）；内缩 1px 形成网格线。</summary>
    Rect CellRect(Rect gridRect, int x, int y)
    {
        int size = target != null ? target.size : 8;
        return new Rect(gridRect.x + x * cellSize + 1f, gridRect.y + (size - 1 - y) * cellSize + 1f, cellSize - 2f, cellSize - 2f);
    }

    /// <summary>矩形描边（仅边框，不填充），用于叠加层角标等，避免遮挡内容。</summary>
    static void DrawRectOutline(Rect r, Color color, float thickness = 2f)
    {
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, thickness), color);
        EditorGUI.DrawRect(new Rect(r.x, r.y + r.height - thickness, r.width, thickness), color);
        EditorGUI.DrawRect(new Rect(r.x, r.y, thickness, r.height), color);
        EditorGUI.DrawRect(new Rect(r.x + r.width - thickness, r.y, thickness, r.height), color);
    }

    /// <summary>多格装饰放置：点击为 footprint 左下角锚点，整组占位后一次保存。</summary>
    void PaintMultiCellDecoration(int x, int y)
    {
        if (target == null || brush == null) return;
        int size = target.size;
        var footprint = new Vector2Int(Mathf.Max(1, multiCellFootprint.x), Mathf.Max(1, multiCellFootprint.y));
        // 与程序生成一致：边沿保留给 Chunk 连通，固定布局也提示并拒绝跨边界。
        if (x < 1 || y < 1 || x + footprint.x > size - 1 || y + footprint.y > size - 1)
        {
            ShowNotification(new GUIContent("多格装饰必须完整位于内部区域，不能占用 Chunk 边沿"));
            return;
        }
        for (int px = x; px < x + footprint.x; px++)
        for (int py = y; py < y + footprint.y; py++)
        {
            if (target.GetOverlay(px, py) != null || FindPlacementAt(px, py) != null)
            {
                ShowNotification(new GUIContent("多格装饰与已有叠加物重叠"));
                return;
            }
        }

        Undo.RecordObject(target, "放置多格装饰物");
        if (target.decorationPlacements == null)
            target.decorationPlacements = new List<DecorationPlacement>();
        target.decorationPlacements.Add(new DecorationPlacement
        {
            prefab = brush,
            anchor = new Vector2Int(x, y),
            footprintSize = footprint,
        });
        EditorUtility.SetDirty(target);
        Repaint();
    }

    DecorationPlacement FindPlacementAt(int x, int y)
    {
        if (target == null || target.decorationPlacements == null) return null;
        for (int i = 0; i < target.decorationPlacements.Count; i++)
        {
            var p = target.decorationPlacements[i];
            if (p != null && p.Contains(x, y)) return p;
        }
        return null;
    }

    /// <summary>左键涂刷：地砖刷子→底层，装饰刷子→叠加；橡皮擦（brush==null）→擦当前编辑层。</summary>
    void Paint(int x, int y)
    {
        var baseP = target.GetTile(x, y);
        var ov = target.GetOverlay(x, y);
        if (brush == null)
        {
            if (editLayer == EditLayer.Base)
            {
                if (baseP == null) return;
                Undo.RecordObject(target, "擦除底层");
                target.SetTile(x, y, null);
            }
            else
            {
                if (ov == null) return;
                Undo.RecordObject(target, "擦除叠加");
                target.SetOverlay(x, y, null);
            }
            EditorUtility.SetDirty(target);
            Repaint();
            return;
        }

        var kind = TileSemantics.ResolveKind(brush);
        if (kind == TerrainKind.Decoration)
        {
            if (ov == brush) return;
            Undo.RecordObject(target, "涂刷叠加层");
            target.SetOverlay(x, y, brush);
        }
        else
        {
            if (baseP == brush) return;
            Undo.RecordObject(target, "涂刷底层");
            target.SetTile(x, y, brush);
        }
        EditorUtility.SetDirty(target);
        Repaint();
    }

    /// <summary>右键擦除：整格两层皆空。</summary>
    void EraseCell(int x, int y)
    {
        var placement = FindPlacementAt(x, y);
        if (placement != null)
        {
            Undo.RecordObject(target, "擦除多格装饰物");
            target.decorationPlacements.Remove(placement);
            EditorUtility.SetDirty(target);
            Repaint();
            return;
        }
        if (target.GetTile(x, y) == null && target.GetOverlay(x, y) == null) return;
        Undo.RecordObject(target, "清空整格");
        target.SetTile(x, y, null);
        target.SetOverlay(x, y, null);
        EditorUtility.SetDirty(target);
        Repaint();
    }

    void DrawFooter()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        EditorGUILayout.HelpBox("左键涂刷（地砖→底层 / 装饰→叠加）｜ 多格放置模式点击锚点放整组 ｜ 右键擦整格/整组 ｜ 多格装饰不可跨 Chunk 边沿", MessageType.None);
        if (GUILayout.Button("清空全部", GUILayout.Width(100f), GUILayout.Height(24f))) ClearAll();
        EditorGUILayout.EndHorizontal();
    }

    void ClearAll()
    {
        if (target == null) return;
        int size = target.size;
        if (!EditorUtility.DisplayDialog("清空全部", $"确定清空布局 '{target.name}' 的全部 {size * size} 格（底层+叠加，两层）？", "清空", "取消")) return;
        Undo.RecordObject(target, "清空 Chunk 布局");
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            target.SetTile(x, y, null);
            target.SetOverlay(x, y, null);
        }
        if (target.decorationPlacements != null)
            target.decorationPlacements.Clear();
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

    // ── 显示辅助：镜像运行时 GenerateFromLayout 的解析语义，确保编辑器所见即所得 ──

    /// <summary>编辑器可用的默认地块（仅 FixedChunkLayout.defaultGround；运行时还会回退 ChunkDef.normalTiles[0]，编辑器不预览后者）。</summary>
    GameObject ResolveEditorDefaultGround() => target != null ? target.defaultGround : null;

    /// <summary>解析单格显示内容（与 GenerateFromLayout 一致）：装饰物整格→默认地块+叠加；仅叠加无地砖→默认地块兜底。</summary>
    void ResolveCell(int x, int y, out GameObject ground, out GameObject overlay, out bool groundIsDefault)
    {
        ground = null; overlay = null; groundIsDefault = false;
        var baseP = target.GetTile(x, y);
        var ov = target.GetOverlay(x, y);
        if (baseP != null)
        {
            var r = TileSemantics.ResolveKind(baseP);
            if (r == TerrainKind.Decoration)
            {
                ground = ResolveEditorDefaultGround();
                groundIsDefault = ground != null;
                overlay = baseP;
            }
            else
            {
                ground = baseP;
            }
        }
        if (ground == null && ov != null)
        {
            ground = ResolveEditorDefaultGround();
            groundIsDefault = ground != null;
        }
        if (ground != null && overlay == null && ov != null) overlay = ov;
    }

    /// <summary>资产缩略图（异步加载未就绪时返回 null，并标记下一帧重绘）。</summary>
    Texture2D GetPreview(GameObject prefab)
    {
        if (prefab == null) return null;
        var t = AssetPreview.GetAssetPreview(prefab);
        if (t == null) needRepaint = true;
        return t;
    }

    // ── prefab 语义标签（推导：kind / solidCollider，无组件配置） ──

    static string Abbrev(GameObject prefab)
    {
        if (prefab == null) return "";
        string name = prefab.name;
        char kindCh;
        switch (TileSemantics.ResolveKind(prefab))
        {
            case TerrainKind.Trigger: kindCh = 'T'; break;
            case TerrainKind.Decoration: kindCh = 'D'; break;
            default: kindCh = 'G'; break;
        }
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

    /// <summary>格子颜色：同类地块按数字变体黄金分割散列均匀散开（绿=地面 橙=触发 红=装饰 灰=空）。</summary>
    static Color CellColor(GameObject prefab)
    {
        if (prefab == null) return new Color(0.25f, 0.25f, 0.25f);
        int n = VariantNumber(prefab);
        float phi = 0.61803398875f;
        float t = (n * phi) % 1f;
        float hBase, hRange, sMin;
        switch (TileSemantics.ResolveKind(prefab))
        {
            case TerrainKind.Trigger: hBase = 0.07f; hRange = 0.06f; sMin = 0.85f; break;
            case TerrainKind.Decoration: hBase = 0.02f; hRange = 0.10f; sMin = 0.7f; break;
            default: hBase = 0.33f; hRange = 0.22f; sMin = 0.5f; break;
        }
        float h = (hBase - hRange * 0.5f + t * hRange + 1f) % 1f;
        float s = Mathf.Clamp01(sMin + 0.4f * (((n + 1) * phi) % 1f - 0.5f));
        float l = Mathf.Clamp01(0.38f + 0.44f * (((n + 3) * phi) % 1f));
        return Color.HSVToRGB(h, s, l);
    }

    static int VariantNumber(GameObject prefab)
    {
        string name = prefab != null ? prefab.name : "";
        var m = System.Text.RegularExpressions.Regex.Match(name, @"\d+");
        return m.Success ? int.Parse(m.Value) : 0;
    }
}
