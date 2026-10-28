using UnityEngine;
using UnityEngine.EventSystems;

public sealed class DrawingEraserHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private UnityEngine.UI.Outline hoverOutline;
    [SerializeField] private GameObject tooltip;

    private void Awake() => SetHovered(false);
    public void OnPointerEnter(PointerEventData eventData) => SetHovered(true);
    public void OnPointerExit(PointerEventData eventData) => SetHovered(false);
    private void OnDisable() => SetHovered(false);

    private void SetHovered(bool hovered)
    {
        if (hoverOutline != null) hoverOutline.enabled = hovered;
        if (tooltip == null) return;
        tooltip.SetActive(hovered);
        if (hovered) tooltip.transform.SetAsLastSibling();
    }
}
