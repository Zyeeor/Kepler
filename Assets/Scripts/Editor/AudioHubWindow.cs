using UnityEditor;
using UnityEngine;

/// <summary>
/// 音频配置中心（统一 EditorWindow）：把分散的音频配置收敛到同一窗口，按 Tab 分页编辑。
///   - 音效表：SfxBank（三分区）；
///   - 怪物技能音：MonsterSkillAudioConfig（七罪 × 技能类别，随机多音源 + 敌我分轨）；
///   - 阶段 BGM：StageBgmMap（逐波 + 阶段槽位）。
/// 每个 Tab 复用对应资产的 CustomEditor 绘制逻辑（Editor.CreateEditor + OnInspectorGUI），
/// 资产按固定路径 Assets/Resources/Audio/xxx.asset 自动加载（与运行时 Resources 兜底路径一致）。
/// 菜单：Kepler → Audio → 音频配置中心。
/// </summary>
public class AudioHubWindow : EditorWindow
{
    const string SfxPath = "Assets/Resources/Audio/SfxBank.asset";
    const string MonsterPath = "Assets/Resources/Audio/MonsterSkillAudioConfig.asset";
    const string BgmPath = "Assets/Resources/Audio/StageBgmMap.asset";

    [MenuItem("Kepler/Audio/音频配置中心")]
    public static void Open()
    {
        var w = GetWindow<AudioHubWindow>("音频配置中心");
        w.minSize = new Vector2(520f, 480f);
        w.Show();
    }

    int _tab;
    static readonly string[] Tabs = { "音效表", "怪物技能音", "阶段 BGM" };

    // 复用各 CustomEditor（缓存，避免每帧重建）
    Editor _sfxEditor, _monsterEditor, _bgmEditor;
    Object _sfxTarget, _monsterTarget, _bgmTarget;

    void OnDestroy()
    {
        DisposeEditor(ref _sfxEditor);
        DisposeEditor(ref _monsterEditor);
        DisposeEditor(ref _bgmEditor);
    }

    static void DisposeEditor(ref Editor ed)
    {
        if (ed != null) { DestroyImmediate(ed); ed = null; }
    }

    static Editor EnsureEditor(ref Editor cached, ref Object cachedTarget, Object asset)
    {
        if (asset == null) return null;
        if (cached == null || cachedTarget != asset)
        {
            if (cached != null) DestroyImmediate(cached);
            cached = Editor.CreateEditor(asset);
            cachedTarget = asset;
        }
        return cached;
    }

    void OnGUI()
    {
        _tab = GUILayout.Toolbar(_tab, Tabs);
        EditorGUILayout.Space(6);
        switch (_tab)
        {
            case 0: DrawAssetTab(ref _sfxEditor, ref _sfxTarget, SfxPath); break;
            case 1: DrawAssetTab(ref _monsterEditor, ref _monsterTarget, MonsterPath); break;
            case 2: DrawAssetTab(ref _bgmEditor, ref _bgmTarget, BgmPath); break;
        }
    }

    void DrawAssetTab(ref Editor cached, ref Object cachedTarget, string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
        if (asset == null)
        {
            EditorGUILayout.HelpBox(
                $"未找到资产：{path}\n请在 Unity 里创建对应音频资产（Create → Kepler → Audio → …）。",
                MessageType.Warning);
            return;
        }

        var ed = EnsureEditor(ref cached, ref cachedTarget, asset);
        if (ed == null) return;

        // 顶部只读显示当前编辑的资产路径，下方直接嵌入其定制 Inspector
        EditorGUILayout.ObjectField("正在编辑", asset, asset.GetType(), false);
        EditorGUILayout.Space(4);
        EditorGUI.BeginChangeCheck();
        ed.OnInspectorGUI();
        // EditorWindow 里复用 CustomEditor 不会像选中资产时的 Inspector 那样自动标记 dirty 并保存；
        // 检测到改动后显式 SetDirty + SaveAssets，确保配置真正写盘（运行时才能读到）。
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }
    }
}
