using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.Hud
{
    public class DropCountConfirmationWindow : MonoBehaviour
    {
        public TextMeshProUGUI ItemDropTitle;
        public TMP_InputField ItemCountInput;
        private Transform container;
        private InventoryItem bagItem;
        private ShopDragData shopData;
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
            isShopDrop = true;
        }
        
        public void BeginItemDrop(InventoryItem item)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            bagItem = item;
            ItemDropTitle.text = $"Drop {item.ItemData.Name}";
            ItemCountInput.text = item.Count.ToString();
            ItemCountInput.ActivateInputField();
            CameraFollower.Instance.InItemInputBox = true;
            isShopDrop = false;
        }
        
        public void SubmitDrop()
        {
            if (int.TryParse(ItemCountInput.text, out var count))
            {
                if (isShopDrop)
                    ShopUI.Instance.FinalizeDropWithCount(shopData, count);
                else
                {
                    if(count <= bagItem.Count)
                        NetworkManager.Instance.SendDropItem(bagItem.Id, count);
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