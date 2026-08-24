using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PossessionImprintIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public SinType sin;
    public Image icon;
    public Text stackText;
    public Color zeroStackColor = new Color(1f, 1f, 1f, 0.35f);
    public Color activeStackColor = Color.white;
    public PossessionImprintTooltip tooltip;

    public void Refresh(int stacks)
    {
        if (stackText != null) stackText.text = "×" + Mathf.Max(0, stacks);
        if (icon != null) icon.color = stacks > 0 ? activeStackColor : zeroStackColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltip == null) return;
        int stacks = PossessionImprintManager.Instance != null
            ? PossessionImprintManager.Instance.GetStacks(sin)
            : 0;
        tooltip.Show(sin, stacks);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltip != null) tooltip.Hide();
    }
}
