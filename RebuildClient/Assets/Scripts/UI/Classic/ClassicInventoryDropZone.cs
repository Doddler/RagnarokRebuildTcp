using Assets.Scripts.UI.Classic.DragAndDrop;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI.Classic
{
    public sealed class ClassicInventoryDropZone : MonoBehaviour, IDropHandler
    {
        public void OnDrop(PointerEventData eventData)
        {
            if (eventData?.pointerDrag == null) return;
            var slot = eventData.pointerDrag.GetComponentInParent<DraggableGearSlot>();
            if (slot != null && slot.ItemId > 0) Assets.Scripts.Network.NetworkManager.Instance?.SendUnEquipItem(slot.ItemId);
        }
    }
}
