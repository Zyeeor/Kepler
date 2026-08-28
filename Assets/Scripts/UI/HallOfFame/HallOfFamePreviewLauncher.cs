using UnityEngine;

/// <summary>
/// 独立荣誉殿堂预览场景的启动器。
/// 在 Play Mode 自动显示正式的 HallOfFamePanel，便于检查运行时界面效果。
/// </summary>
public class HallOfFamePreviewLauncher : MonoBehaviour
{
    [Tooltip("进入 Play Mode 时自动打开荣誉殿堂。")]
    public bool showOnStart = true;

    void Start()
    {
        if (showOnStart)
            HallOfFamePanel.EnsureInstance().Show();
    }
}
