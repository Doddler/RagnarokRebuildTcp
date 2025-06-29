using System;
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.Hud;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.Inventory
{
    public class VendingSetupManager : MonoBehaviour
    {
        public WindowBase SourceItemWindow;
        public ItemIconContainer SourceItemList;
        public ItemListDropZoneV2 SourceItemDropZone;
        public GenericItemListV2 SaleItemWindow;
        public ItemListDropZoneV2 SaleItemDropZone;
        public TMP_InputField ShopTitleInput;

        [NonSerialized] public static VendingSetupManager Instance;

        public Dictionary<int, InventoryItem> LeftItemList = new();
        public Dictionary<int, InventoryItem> RightItemList = new();

        public Dictionary<int, ItemListEntryV2> RightItemEntries = new();

        private List<int> selectableInputs = new();
        
        private int currentTabIndex = 0;
        private int maxVendSlots = 0;
        private bool isInTextInput;
        private float closeTime = -1;

        public static void OpenVendSetup()
        {
            if (Instance != null)
            {
                if (Instance.gameObject.activeInHierarchy)
                    return;
                
                Instance.gameObject.SetActive(true);
                return;
            }
            
            var prefab = Resources.Load<GameObject>("VendingSetup");
            var go = Instantiate(prefab, UiManager.Instance.PrimaryUserWindowContainer);
            var vend = go.GetComponent<VendingSetupManager>();
            vend.SourceItemWindow.transform.SetParent(go.transform.parent, true);
            vend.SaleItemWindow.transform.SetParent(go.transform.parent, true);
            vend.BeginVendingSetup();
        }

        public void SubmitVending()
        {
            var hasZeroValue = false;
            foreach (var (_, entry) in RightItemEntries)
            {
                if (!int.TryParse(entry.InputField.text, out var cost) || cost <= 0)
                    hasZeroValue = true;
            }

            if (!hasZeroValue)
                FinalizeSubmitVending();
            else
                UiManager.Instance.YesNoOptionsWindow.BeginPrompt("You have one or more item listed for 0 zeny. Are you sure you want to open shop?", "Yes",
                    "No", FinalizeSubmitVending, null, false, false);
        }

        public void FinalizeSubmitVending()
        {
            NetworkManager.Instance.VendingStart(ShopTitleInput.text, RightItemEntries);
            SaleItemWindow.gameObject.SetActive(false);
            SourceItemWindow.gameObject.SetActive(false);
            closeTime = Time.timeSinceLevelLoad + 5f;
        }

        public void ResumeVendWindow()
        {
            closeTime = -1;
            SaleItemWindow.gameObject.SetActive(true);
            SourceItemWindow.gameObject.SetActive(true);
        }
        
        public void CancelVending()
        {
            Destroy(SourceItemWindow.gameObject);
            Destroy(SaleItemWindow.gameObject);
            Destroy(gameObject);
            Instance = null;
        }

        public void SelectInput(int id)
        {
            isInTextInput = true;
            currentTabIndex = id;
        }

        public void UnselectInput(int id)
        {
            if (currentTabIndex == id)
            {
                isInTextInput = false;
                UpdateSubmitOption();
            }
        }
        
        public void UpdateSubmitOption()
        {
            var isInputsValid = true;
            foreach (var (_, entry) in RightItemEntries)
            {
                if (!int.TryParse(entry.InputField.text, out var cost) || cost < 0)
                {
                    isInputsValid = false;
                    break;
                }
            }
            
            SaleItemWindow.OkButton.interactable = isInputsValid && !string.IsNullOrWhiteSpace(ShopTitleInput.text) && RightItemEntries.Count > 0;
        }

        public void HideDropArea()
        {
            SourceItemDropZone.gameObject.SetActive(false);
            SaleItemDropZone.gameObject.SetActive(false);
        }

        private void OnDropItemOntoRightSide(ItemDragObject srcItem)
        {
            if (NetworkManager.Instance.PlayerState.Cart.GetInventoryData().TryGetValue(srcItem.ItemId, out var item))
            {
                if (item.ItemData.IsUnique || item.Count == 1)
                    FinalizeDropItemOntoRightSide(srcItem.ItemId, 1);
                else
                {
                    item.Count = srcItem
                        .ItemCount; //the left side count might not reflect the cart count, as the item might already have some in the vend window
                    UiManager.Instance.DropCountConfirmationWindow.BeginItemDrop(item, DropConfirmationType.CartToVend);
                }
            }
        }

        private void OnDropItemOntoLeftSide(ItemDragObject srcItem)
        {
            if (srcItem.ItemCount == 1)
                FinalizeDropItemOntoLeftSide(srcItem.ItemId, 1);
            else
            {
                var item = RightItemList[srcItem.ItemId];
                UiManager.Instance.DropCountConfirmationWindow.BeginItemDrop(item, DropConfirmationType.VendToCart);
            }
        }
        
        public void FinalizeDropItemOntoLeftSide(int bagId, int count)
        {
            var rightItem = RightItemList[bagId];
            var rightEntry = RightItemEntries[bagId];
            
            count = Mathf.Clamp(count, 0, rightItem.Count);

            if (rightItem.Count <= count)
            {
                selectableInputs.Remove(bagId);
                RightItemList.Remove(bagId);
                RightItemEntries.Remove(bagId);
                rightEntry.InputField.text = "0";
                rightEntry.InputField.onSelect.RemoveAllListeners();
                rightEntry.InputField.onDeselect.RemoveAllListeners();
                SaleItemWindow.ReturnItemListEntry(rightEntry);
                SaleItemWindow.InfoAreaText.text = $"Slots Available {maxVendSlots-RightItemList.Count}/{maxVendSlots}";
            }
            else
            {
                rightItem.Count -= count;
                RightItemList[bagId] = rightItem;
                rightEntry.UpdateCount(rightItem.Count);
            }

            if (LeftItemList.TryGetValue(bagId, out var leftItem))
            {
                leftItem.Count += count;
                LeftItemList[bagId] = leftItem;
            }
            else
            {
                var newLeftItem = rightItem;
                newLeftItem.Count = count;
                LeftItemList.Add(bagId, newLeftItem);
            }
            
            SourceItemList.RefreshItemList();
            UpdateSubmitOption();
        }

        public void FinalizeDropItemOntoRightSide(int bagId, int count)
        {
            var leftItem = LeftItemList[bagId];

            count = Mathf.Clamp(count, 0, leftItem.Count);

            if (leftItem.Count <= count)
                LeftItemList.Remove(bagId);
            else
            {
                leftItem.Count -= count;
                LeftItemList[bagId] = leftItem;
            }

            SourceItemList.RefreshItemList();

            if (RightItemEntries.TryGetValue(bagId, out var entry))
            {
                var curRight = RightItemList[bagId];
                entry.CountText.text = "";

                curRight.Count += count;
                entry.UpdateCount(curRight.Count);
                RightItemList[bagId] = curRight;

                return;
            }

            leftItem.Count = count;
            entry = SaleItemWindow.GetNewEntry();

            var sprite = ClientDataLoader.Instance.ItemIconAtlas.GetSprite(leftItem.ItemData.Sprite);

            entry.Assign(DragItemType.VendSetupTarget, sprite, leftItem.BagSlotId, count);
            entry.DragOrigin = ItemDragOrigin.VendingTarget;
            entry.UniqueEntryId = bagId;
            entry.ItemName.text = leftItem.ProperName();
            entry.ItemName.rectTransform.anchorMax = new Vector2(1, 1); //resize to full width
            entry.RightText.text = "";
            entry.CanDrag = true;
            entry.CanSelect = false;
            RightItemEntries.Add(bagId, entry);
            RightItemList.Add(bagId, leftItem);
            selectableInputs.Add(bagId);
            
            entry.InputField.onSelect.AddListener((_) => SelectInput(bagId));
            entry.InputField.onDeselect.AddListener((_) => UnselectInput(bagId));
            
            SaleItemWindow.InfoAreaText.text = $"Slots Available {maxVendSlots-RightItemList.Count}/{maxVendSlots}";
            UpdateSubmitOption();
        }

        public void StartDrag(DragItemType type)
        {
            if (type == DragItemType.VendSetupSource && RightItemEntries.Count < maxVendSlots)
                SaleItemDropZone.gameObject.SetActive(true);
            if (type == DragItemType.VendSetupTarget)
                SourceItemDropZone.gameObject.SetActive(true);
        }

        public void DropRightSideItemInTrash(int bagId)
        {
            var entry = SaleItemWindow.GetNewEntry();
        }

        public void BeginVendingSetup()
        {
            Instance = this;
            var cart = PlayerState.Instance.Cart;
            if (cart == null)
            {
                CameraFollower.Instance.AppendError("Unable to open the vending window due to an internal error.");
                Destroy(gameObject);
                return;
            }
            
            UiManager.Instance.CartWindow.HideWindow();
            UiManager.Instance.ForceHideTooltip();

            maxVendSlots = PlayerState.Instance.KnownSkills[CharacterSkill.Vending] + 2;
            SaleItemWindow.InfoAreaText.text = $"Slots Available {maxVendSlots}/{maxVendSlots}";

            foreach (var (bagId, item) in cart.GetInventoryData())
                LeftItemList.Add(bagId, item);

            SourceItemList.AssignItemList(LeftItemList, DragItemType.VendSetupSource);
            SourceItemDropZone.OnDropItem = OnDropItemOntoLeftSide;
            SaleItemDropZone.OnDropItem = OnDropItemOntoRightSide;
            
            SourceItemWindow.CenterWindowWithOffset(new Vector2(428f, 211f));
            SaleItemWindow.CenterWindowWithOffset(new Vector2(71f, 211f));
            
            selectableInputs.Add(0);
        }

        private void Update()
        {
            if (closeTime > 0 && closeTime < Time.timeSinceLevelLoad)
            {
                CancelVending();
                return;
            }
            
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (!isInTextInput) //if they hit tab without an input selected, it'll reselect the current entry
                {
                    if(currentTabIndex == 0)
                        ShopTitleInput.ActivateInputField();
                    else
                        RightItemEntries[currentTabIndex].InputField.ActivateInputField();
                    isInTextInput = true;
                    return;
                }
                
                var isReverse = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                if (currentTabIndex == -1) //if we have nothing selected
                {
                    if (!isReverse || RightItemEntries.Count == 0)
                    {
                        ShopTitleInput.ActivateInputField();
                        currentTabIndex = 0;
                    }
                    else
                    {
                        currentTabIndex = selectableInputs[^1];
                        RightItemEntries[currentTabIndex].InputField.ActivateInputField();
                    }

                    return;
                }

                var idx = selectableInputs.IndexOf(currentTabIndex);
                idx += !isReverse ? 1 : -1;
                if (idx < 0)
                    idx = selectableInputs.Count - 1;
                if (idx >= selectableInputs.Count)
                    idx = 0;

                currentTabIndex = selectableInputs[idx];
                
                if(currentTabIndex == 0)
                    ShopTitleInput.ActivateInputField();
                else
                    RightItemEntries[currentTabIndex].InputField.ActivateInputField();
            }
        }
        

        private void OnDestroy()
        {
            if (SourceItemWindow)
                Destroy(SourceItemWindow.gameObject);
            if (SaleItemWindow)
                Destroy(SaleItemWindow.gameObject);
            Instance = null;
        }
    }
}