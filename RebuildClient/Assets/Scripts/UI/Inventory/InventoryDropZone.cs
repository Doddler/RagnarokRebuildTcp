using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.Hud;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Inventory
{
    public class InventoryDropZone : UIBehaviour, IPointerEnterHandler, IPointerExitHandler, IItemDropTarget
    {
        public Image HighlightZone;

        public new void Awake()
        {
            HighlightZone.color = new Color32(0, 0, 0, 0);
            base.Awake();
        }

        public void DisableDropArea()
        {
            HighlightZone.color = new Color32(0, 0, 0, 0);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (UiManager.Instance.IsDraggingItem)
            {
                UiManager.Instance.RegisterDragTarget(this);
                HighlightZone.color = new Color32(149, 190, 255, 255);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (UiManager.Instance.IsDraggingItem)
            {
                UiManager.Instance.UnregisterDragTarget(this);
                HighlightZone.color = new Color32(0, 0, 0, 0);
            }
        }

        public void DropItem()
        {
            var obj = UiManager.Instance.DragItemObject;
            switch (obj.Origin)
            {
                case ItemDragOrigin.HotBar:
                    UiManager.Instance.SkillHotbar.GetEntryById(obj.OriginId).Clear();
                    break;
                case ItemDragOrigin.ItemWindow:
                    NetworkManager.Instance.SendEquipItem(obj.ItemId); //why doesn't this use origin? I don't know!
                    break;
                case ItemDragOrigin.EquipmentWindow:
                    NetworkManager.Instance.SendUnEquipItem(obj.OriginId);
                    break;
                case ItemDragOrigin.StorageWindow:
                    StorageUI.Instance?.OnMoveItemToInventory(obj.OriginId);
                    break;
            }
        }
    }
}