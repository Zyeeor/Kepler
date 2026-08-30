using UnityEngine;

/// <summary>
/// 游戏内鼠标光标配置（ScriptableObject）：集中提供「进入游戏后使用哪个光标」的可拖放配置位置。
/// 由 GameCursorManager 在启动时应用，避免 Player Settings 默认光标在 built（Standalone）版本不生效的问题。
/// </summary>
[CreateAssetMenu(menuName = "Kepler/UI/Game Cursor Settings", fileName = "GameCursorSettings")]
public class GameCursorSettings : ScriptableObject
{
    [Tooltip("游戏内鼠标光标纹理（建议 Texture Type = Cursor，并开启 Read/Write）。")]
    public Texture2D cursorTexture;

    [Tooltip("光标热点：相对纹理左上角的点击生效点（像素）。")]
    public Vector2 hotspot = Vector2.zero;

    [Tooltip("光标缩放倍数（1=原始尺寸，>1 放大，<1 缩小）。缩放后使用软件光标渲染。")]
    [Min(0.1f)] public float cursorScale = 1f;

    [Tooltip("是否显示游戏光标。")]
    public bool visible = true;
}
