using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

/// <summary>
/// 通用确认弹窗组件。
/// 支持自定义标题、描述文本、确认/取消回调。
/// </summary>
public class ConfirmDialog : MonoBehaviour
{
    [Header("UI References")]
    public GameObject dialogPanel;
    public TMP_Text titleText;
    public TMP_Text messageText;
    public Button confirmButton;
    public TMP_Text confirmButtonText;
    public Button cancelButton;
    public TMP_Text cancelButtonText;

    [Header("Default Text")]
    [Tooltip("默认标题（TextCatalog 优先：ui.dialog.title）")]
    public string defaultTitle = "确认";
    [Tooltip("默认正文（TextCatalog 优先：ui.dialog.message）")]
    public string defaultMessage = "确定要执行此操作吗？";
    [Tooltip("确认按钮文案（TextCatalog 优先：ui.dialog.confirm）")]
    public string defaultConfirmLabel = "确认";
    [Tooltip("取消按钮文案（TextCatalog 优先：ui.dialog.cancel）")]
    public string defaultCancelLabel = "取消";

    private UnityAction onConfirmCallback;
    private UnityAction onCancelCallback;

    private bool inited = false;

    /// <summary>
    /// Init from a controller that IS always active (Canvas root).
    /// Binds listeners even if this panel starts inactive.
    /// </summary>
    public void Init()
    {
        if (inited) return;
        inited = true;

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (dialogPanel != null)
            dialogPanel.SetActive(false);
    }

    /// <summary>
    /// 显示确认弹窗。
    /// </summary>
    public void Show(string title, string message, UnityAction onConfirm = null, UnityAction onCancel = null)
    {
        // 统一文本目录：默认文案优先取目录（可全局改文案），字段值回退兜底
        if (titleText != null)
            titleText.text = string.IsNullOrEmpty(title) ? TextCatalog.Get("ui.dialog.title") : title;

        if (messageText != null)
            messageText.text = string.IsNullOrEmpty(message) ? TextCatalog.Get("ui.dialog.message") : message;

        if (confirmButtonText != null)
            confirmButtonText.text = TextCatalog.Get("ui.dialog.confirm");

        if (cancelButtonText != null)
            cancelButtonText.text = TextCatalog.Get("ui.dialog.cancel");

        onConfirmCallback = onConfirm;
        onCancelCallback = onCancel;

        if (dialogPanel != null)
        {
            dialogPanel.SetActive(true);
            dialogPanel.transform.SetAsLastSibling();
        }
    }

    /// <summary>
    /// 隐藏确认弹窗。
    /// </summary>
    public void Hide()
    {
        if (dialogPanel != null)
            dialogPanel.SetActive(false);

        onConfirmCallback = null;
        onCancelCallback = null;
    }

    public bool IsVisible()
    {
        return dialogPanel != null && dialogPanel.activeSelf;
    }

    private void OnConfirm()
    {
        Debug.Log("ConfirmDialog: OnConfirm CALLED. callback null? " + (onConfirmCallback == null));
        var cb = onConfirmCallback;
        Hide();
        cb?.Invoke();
    }

    private void OnCancel()
    {
        Debug.Log("ConfirmDialog: OnCancel CALLED. callback null? " + (onCancelCallback == null));
        var cb = onCancelCallback;
        Hide();
        cb?.Invoke();
    }
}
