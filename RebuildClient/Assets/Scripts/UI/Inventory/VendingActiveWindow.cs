using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.UI.Utility;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.Inventory
{
    public class VendingActiveWindow : WindowBase
    {
        public TextMeshProUGUI ShopName;
        public ItemIconContainer IconContainer;

        public void StopVending()
        {
            Destroy(gameObject);
            NetworkManager.Instance.VendingEnd();
            CameraFollower.Instance.AppendChatText("You are no longer vending.", TextColor.Job);
        }

        private Dictionary<int, InventoryItem> itemList;
        public Dictionary<int, int> ItemPriceList; //public because it's used by DraggableItem to show selling price in mouseover overlay

        public static VendingActiveWindow Instance;


        public static void BeginActiveVending(string shopName, Dictionary<int, InventoryItem> list, Dictionary<int, int> prices)
        {
            var go = Instantiate(Resources.Load<GameObject>("ActiveVend"), UiManager.Instance.PrimaryUserWindowContainer);
            var window = go.GetComponent<VendingActiveWindow>();
            var co = window.IconContainer;

            window.ShopName.text = $"Vending: {shopName}";
            co.AssignItemList(list, DragItemType.VendActive);

            window.ItemPriceList = prices;
            window.itemList = list;

            window.CenterWindow();
            window.MoveToTop();

            Instance = window;
        }

        public void ReceiveNotificationOfSale(int bagId, int count)
        {
            if (!itemList.TryGetValue(bagId, out var saleItem))
                return;

            var price = ItemPriceList[bagId];

            var zeny = price * count;
            if(count == 1)
                CameraFollower.Instance.AppendChatText($"Sold {saleItem.ProperName()} for {zeny:N0}z.");
            else
                CameraFollower.Instance.AppendChatText($"Sold {count}x {saleItem.ProperName()} for {zeny:N0}z.");

            saleItem.Count -= count;
            if (saleItem.Count <= 0)
            {
                itemList.Remove(bagId);
                ItemPriceList.Remove(bagId);
            }
            else
                itemList[bagId] = saleItem;
            
            PlayerState.Instance.Zeny += zeny;
            
            CameraFollower.Instance.CharacterDetailBox.CharacterZeny.text = $"Zeny: {PlayerState.Instance.Zeny:N0}";
            
            IconContainer.RefreshItemList();
        }
    }
}