using Assets.Scripts.Network;
using Assets.Scripts.UI.Hud;
using Assets.Scripts.UI.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI
{
    public class DragTrashBucket : UIBehaviour, IPointerEnterHandler, IPointerExitHandler, IItemDropTarget
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (UiManager.Instance.IsDraggingItem)
                UiManager.Instance.RegisterDragTarget(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (UiManager.Instance.IsDraggingItem)
                UiManager.Instance.UnregisterDragTarget(this);
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
                    if (UiManager.Instance.EquipmentWindow.isActiveAndEnabled)
                    {
                        CameraFollower.Instance.AppendNotice($"Cannot drop items while equipment window is open.");
                        return;
                    }
                    
                    if (StorageUI.Instance != null)
                    {
                        CameraFollower.Instance.AppendNotice($"Cannot drop items while storage window is open.");
                        return;
                    }

                    Debug.Log($"Dropped item from inventory onto ground area.");
                    
                    if(obj.ItemCount == 1)
                        NetworkManager.Instance.SendDropItem(obj.ItemId, obj.ItemCount);
                    else
                    {
                        if(NetworkManager.Instance.PlayerState.Inventory.GetInventoryData().TryGetValue(obj.ItemId, out var item))
                        {
                            UiManager.Instance.DropCountConfirmationWindow.BeginItemDrop(item, DropConfirmationType.DropOnGround);
                        }
                    }
                        
                    break;
                case ItemDragOrigin.EquipmentWindow:
                    NetworkManager.Instance.SendUnEquipItem(obj.OriginId);
                    break;
                case ItemDragOrigin.ShopWindow:
                    ShopUI.Instance.OnDropTrash(obj);
                    break;
                case ItemDragOrigin.VendingTarget:
                    VendingSetupManager.Instance?.DropRightSideItemInTrash(obj.OriginId);
                    break;
            }
        }
    }
}