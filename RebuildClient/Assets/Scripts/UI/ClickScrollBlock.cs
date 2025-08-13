using UnityEngine.EventSystems;

public class ClickScrollBlock : UIBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    public void OnDrag(PointerEventData eventData)
    {
        eventData.Use();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        eventData.Use();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        eventData.Use();
    }
}
