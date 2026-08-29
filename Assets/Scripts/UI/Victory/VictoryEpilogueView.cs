using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Victory Epilogue 的可手动编辑视图引用。
/// 挂在 VictoryEpilogueView.prefab 根节点；所有 RectTransform、字号、颜色、字体、层级均以 Prefab 为准。
/// </summary>
public sealed class VictoryEpilogueView : MonoBehaviour
{
    [Header("Canvas / 画布")]
    public Canvas canvas;
    public CanvasGroup rootGroup;
    public Image blackBackground;

    [Header("First Stage / 第一幕")]
    public CanvasGroup firstStageGroup;
    public TextMeshProUGUI firstMessageText;
    public TextMeshProUGUI namePromptText;
    public CanvasGroup inputGroup;
    public TMP_InputField nameInput;
    public Button confirmButton;

    [Header("Final Stage / 最终幕")]
    public CanvasGroup finalStageGroup;
    public CanvasGroup finalTitleGroup;
    public TextMeshProUGUI finalTitleText;
    public CanvasGroup finalNameGroup;
    public TextMeshProUGUI finalNameText;
    public CanvasGroup finalCoronationGroup;
    public TextMeshProUGUI finalCoronationText;

    public bool HasRequiredReferences
    {
        get
        {
            return canvas != null && rootGroup != null && blackBackground != null
                && firstStageGroup != null && firstMessageText != null && namePromptText != null
                && inputGroup != null && nameInput != null && confirmButton != null
                && finalStageGroup != null && finalTitleGroup != null && finalTitleText != null
                && finalNameGroup != null && finalNameText != null
                && finalCoronationGroup != null && finalCoronationText != null;
        }
    }

    public void ApplyDefaultRuntimeFlags()
    {
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
        }
        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
        }
        if (blackBackground != null)
            blackBackground.raycastTarget = true;
        SetGroup(firstStageGroup, false);
        SetGroup(inputGroup, false);
        SetGroup(finalStageGroup, false);
        SetGroup(finalTitleGroup, false);
        SetGroup(finalNameGroup, false);
        SetGroup(finalCoronationGroup, false);
    }

    static void SetGroup(CanvasGroup group, bool active)
    {
        if (group == null) return;
        group.alpha = active ? 1f : 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        group.gameObject.SetActive(active);
    }
}
