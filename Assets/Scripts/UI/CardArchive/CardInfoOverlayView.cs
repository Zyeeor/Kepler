using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 卡牌信息弹窗的预制体视图——布局本身完全由 Prefab（Resources/SystemUI/CardInfoOverlay）
/// 决定，策划可以直接在 Prefab 编辑模式下可视化调整边框、卡面区域、名称/效果框的位置与大小；
/// 脚本只负责在运行时把动态内容（卡面、名称、效果文案）填进已排好版的坑位。
/// </summary>
public class CardInfoOverlayView : MonoBehaviour
{
    [SerializeField] Button closeButton;
    [Tooltip("关闭按钮图标；不指定时回退代码里运行时切图的默认图标。")]
    [SerializeField] Sprite closeIconSprite;
    [SerializeField] RectTransform artHost;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] GameObject effectSection;
    [SerializeField] TextMeshProUGUI effectText;

    public Button CloseButton => closeButton;
    public Sprite CloseIconSprite => closeIconSprite;
    public RectTransform ArtHost => artHost;
    public TextMeshProUGUI NameText => nameText;
    public GameObject EffectSection => effectSection;
    public TextMeshProUGUI EffectText => effectText;
}
