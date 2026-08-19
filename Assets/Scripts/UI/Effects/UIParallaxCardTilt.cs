using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Adds a mouse-driven layered tilt effect to a UI card.
/// Assign separate foreground and background RectTransforms for independent motion.
/// </summary>
[DisallowMultipleComponent]
public class UIParallaxCardTilt : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Pointer Area")]
    [Tooltip("The visible card area used to normalize the mouse position. Falls back to this RectTransform.")]
    [SerializeField] private RectTransform pointerArea;

    [Header("Layers")]
    [Tooltip("Visual objects in front of the card, such as artwork, text, and buttons.")]
    [SerializeField] private RectTransform foregroundLayer;
    [Tooltip("Visual objects between the front artwork and the card background.")]
    [SerializeField] private RectTransform middlegroundLayer;
    [Tooltip("Visual objects behind the card, such as the card frame or background field.")]
    [SerializeField] private RectTransform backgroundLayer;

    [Header("Tilt")]
    [SerializeField, Range(0f, 20f)] private float foregroundTilt = 7f;
    [SerializeField, Range(0f, 20f)] private float middlegroundTilt = 5f;
    [SerializeField, Range(0f, 20f)] private float backgroundTilt = 3f;
    [SerializeField] private bool invertBackgroundTilt;
    [SerializeField, Min(0.01f)] private float smoothSpeed = 12f;



    [Header("Parallax Offset")]
    [Tooltip("Maximum local horizontal movement of the foreground layer at the card edge.")]
    [SerializeField] private float foregroundOffset = 10f;
    [Tooltip("Maximum local horizontal movement of the middleground layer at the card edge.")]
    [SerializeField] private float middlegroundOffset = 7f;
    [Tooltip("Maximum local horizontal movement of the background layer at the card edge.")]
    [SerializeField] private float backgroundOffset = 4f;

    private RectTransform root;
    private Quaternion foregroundBaseRotation;
    private Quaternion middlegroundBaseRotation;
    private Quaternion backgroundBaseRotation;
    private Vector3 foregroundBasePosition;
    private Vector3 middlegroundBasePosition;
    private Vector3 backgroundBasePosition;
    private Vector2 pointerNormalized;
    private bool isHovered;

    private void Awake()
    {
        root = transform as RectTransform;
        CacheLayerBases();
    }

    private void OnEnable()
    {
        pointerNormalized = Vector2.zero;
        isHovered = false;
        CacheLayerBases();
    }

    private void LateUpdate()
    {
        if (root == null) root = transform as RectTransform;

        Vector2 targetPointer = isHovered ? ReadPointerNormalized() : Vector2.zero;
        pointerNormalized = Vector2.Lerp(pointerNormalized, targetPointer, GetInterpolationFactor());

        ApplyLayer(
            foregroundLayer,
            foregroundBaseRotation,
            foregroundBasePosition,
            foregroundTilt,
            foregroundOffset,
            false);

        ApplyLayer(
            middlegroundLayer,
            middlegroundBaseRotation,
            middlegroundBasePosition,
            middlegroundTilt,
            middlegroundOffset,
            false);

        ApplyLayer(
            backgroundLayer,
            backgroundBaseRotation,
            backgroundBasePosition,
            backgroundTilt,
            backgroundOffset,
            invertBackgroundTilt);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }

    private Vector2 ReadPointerNormalized()
    {
        RectTransform area = pointerArea != null ? pointerArea : root;
        if (area == null) return Vector2.zero;

        Canvas canvas = area.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, Input.mousePosition, eventCamera, out Vector2 localPoint))
            return Vector2.zero;

        Vector2 halfSize = area.rect.size * 0.5f;
        if (halfSize.x <= 0.001f || halfSize.y <= 0.001f) return Vector2.zero;

        return new Vector2(
            Mathf.Clamp(localPoint.x / halfSize.x, -1f, 1f),
            Mathf.Clamp(localPoint.y / halfSize.y, -1f, 1f));
    }

    private void ApplyLayer(
        RectTransform layer,
        Quaternion baseRotation,
        Vector3 basePosition,
        float tilt,
        float horizontalOffset,
        bool invertTilt)
    {
        if (layer == null) return;

        float direction = invertTilt ? -1f : 1f;
        Quaternion tiltRotation = Quaternion.Euler(
            -pointerNormalized.y * tilt * direction,
            pointerNormalized.x * tilt * direction,
            0f);

        layer.localRotation = baseRotation * tiltRotation;
        layer.localPosition = basePosition + new Vector3(
            pointerNormalized.x * horizontalOffset,
            0f,
            0f);
    }

    private void CacheLayerBases()
    {
        if (foregroundLayer != null)
        {
            foregroundBaseRotation = foregroundLayer.localRotation;
            foregroundBasePosition = foregroundLayer.localPosition;
        }

        if (middlegroundLayer != null)
        {
            middlegroundBaseRotation = middlegroundLayer.localRotation;
            middlegroundBasePosition = middlegroundLayer.localPosition;
        }

        if (backgroundLayer != null)
        {
            backgroundBaseRotation = backgroundLayer.localRotation;
            backgroundBasePosition = backgroundLayer.localPosition;
        }
    }

    private float GetInterpolationFactor()
    {
        return 1f - Mathf.Exp(-smoothSpeed * Time.unscaledDeltaTime);
    }
}
