using UnityEngine;

/// <summary>
/// 卡牌收藏独立预览场景启动器。
/// 在 Play Mode 自动显示正式的 CardArchivePanel，便于检查运行时界面效果。
/// </summary>
public class CardArchivePreviewLauncher : MonoBehaviour
{
    [Tooltip("进入 Play Mode 时自动打开卡牌收藏。")]
    public bool showOnStart = true;

    void Start()
    {
        if (showOnStart)
            CardArchivePanel.EnsureInstance().Show();
    }
}
