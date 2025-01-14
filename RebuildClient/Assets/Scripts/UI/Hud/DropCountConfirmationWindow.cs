using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.Hud
{
    public enum DropConfirmationType
    {
        DropOnGround,
        ShopTransfer,
        InventoryToStorage,
        StorageToInventory
    }
    
    public class DropCountConfirmationWindow : MonoBehaviour
    {
        public TextMeshProUGUI ItemDropTitle;
        public TMP_InputField ItemCountInput;
        private Transform container;
        private InventoryItem bagItem;
        private ShopDragData shopData;
        private DropConfirmationType dropType;
        private bool isShopDrop;

        public void Awake()
        {
            container = transform.parent;
        }

        public void HideInputWindow()
        {
            gameObject.SetActive(false);
            CameraFollower.Instance.InItemInputBox = false;
        }

        public void BeginShopItemDrop(ShopDragData data, string itemName)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            shopData = data;
            var count = data.Count;
            if (count <= 0)
                count = 1;
            if(data.DragSource == ItemListRole.BuyFromNpcItemList)
                ItemDropTitle.text = $"Buy {itemName}";
            if (data.DragSource == ItemListRole.BuyFromNpcSummary || data.DragSource == ItemListRole.SellToNpcSummary)
                ItemDropTitle.text = $"Remove {itemName}";
            if (data.DragSource == ItemListRole.SellToNpcItemList)
                ItemDropTitle.text = $"Sell {itemName}";
            ItemCountInput.text = $"{count}";
            ItemCountInput.ActivateInputField();
            CameraFollower.Instance.InItemInputBox = true;
            dropType = DropConfirmationType.ShopTransfer;
        }
        
        public void BeginItemDrop(InventoryItem item, DropConfirmationType dropType)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            bagItem = item;
            ItemDropTitle.text = dropType switch
            {
                DropConfirmationType.DropOnGround => $"Drop {item.ItemData.Name}",
                _ => $"Transfer {item.ItemData.Name}"
            };
            ItemCountInput.text = item.Count.ToString();
            ItemCountInput.ActivateInputField();
            CameraFollower.Instance.InItemInputBox = true;
            this.dropType = dropType;
        }
        
        public void SubmitDrop()
        {
            if (int.TryParse(ItemCountInput.text, out var count))
            {
                switch (dropType)
                {
                    case DropConfirmationType.ShopTransfer:
                        ShopUI.Instance.FinalizeDropWithCount(shopData, count);
                        break;
                    case DropConfirmationType.DropOnGround:
                        NetworkManager.Instance.SendDropItem(bagItem.Id, count);
                        break;
                    case DropConfirmationType.InventoryToStorage:
                        NetworkManager.Instance.SendMoveStorageItem(bagItem.Id, count, true);
                        break;
                    case DropConfirmationType.StorageToInventory:
                        NetworkManager.Instance.SendMoveStorageItem(bagItem.Id, count, false);
                        break;
                    
                }
            }
            HideInputWindow();
        }

        public void Update()
        {
            if (transform != container.GetChild(container.childCount - 1))
            {
                gameObject.SetActive(false);
                CameraFollower.Instance.InItemInputBox = false;
                return;
            }
        }
    }
}