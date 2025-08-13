using System;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.Inventory;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI
{
    public enum DragItemType
    {
        None,
        Skill,
        Item,
        Equipment,
        ShopItem,
        StorageItem,
        SocketedItem,
        CartItem,
        VendSetupSource,
        VendSetupTarget,
        VendActive,
        VendShop,
        VendPurchase
    }

    public class DraggableItem : DragItemBase, IDragHandler, IBeginDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        // private bool isMouseDown = false;
        private Vector3 startMousePosition;
        private Vector3 startPosition;
        private bool adjustCanvasAlpha;
        public ItemDragOrigin Origin;
        public int OriginId;
        public Action OnDoubleClick;
        public Action OnRightClick;

        private UiManager manager;

        public void Awake()
        {
            manager = UiManager.Instance;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Type == DragItemType.SocketedItem)
                return;
            
            manager.StartItemDrag(this);
            manager.DragItemObject.Origin = Origin;
            manager.DragItemObject.OriginId = OriginId;
            Image.enabled = false;
            if(CountText != null)
                CountText.enabled = false;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            manager.EndItemDrag();

            Image.enabled = true;
            if(CountText != null)
                CountText.enabled = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Debug.Log("HII");
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (OnRightClick != null && eventData.button == PointerEventData.InputButton.Right 
                                     && (Type == DragItemType.Item || Type == DragItemType.Equipment || Type == DragItemType.SocketedItem || Type == DragItemType.CartItem))
                OnRightClick();
            if (OnDoubleClick != null && Type != DragItemType.None && eventData.button == PointerEventData.InputButton.Left 
                && eventData.clickCount >= 2 && OnDoubleClick != null)
            {
                OnDoubleClick();
                UiManager.Instance.HideTooltip(gameObject);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Type != DragItemType.None)
            {
                var text = "";
                if (Type == DragItemType.Skill)
                {
                    var skill = ClientDataLoader.Instance.GetSkillData((CharacterSkill)ItemId);

                    var spCost = "";
                    if (skill.Target != SkillTarget.Passive && skill.SpCost != null && skill.SpCost.Length > 0)
                    {
                        var lvl = ItemCount;
                        if (!skill.AdjustableLevel && PlayerState.Instance.KnownSkills.TryGetValue(skill.SkillId, out var knownLevel))
                            lvl = knownLevel;
                        lvl = Mathf.Clamp(lvl, 1, skill.SpCost.Length);

                        spCost = $" (SP: {skill.SpCost[lvl - 1]})";
                    }

                    if (skill.AdjustableLevel)
                        text = $"{skill.Name} Lv {ItemCount}{spCost}";
                    else
                        text = $"{skill.Name}{spCost}";
                    
                }

                if (Type == DragItemType.CartItem)
                {
                    var inventory = NetworkManager.Instance.PlayerState.Cart.GetInventoryData();
                    if (!inventory.TryGetValue(ItemId, out var dat))
                        return;
                    text = dat.ProperName();
                }
                
                
                if (Type == DragItemType.VendActive)
                {
                    var inventory = NetworkManager.Instance.PlayerState.Cart.GetInventoryData();
                    var prices = VendingActiveWindow.Instance.ItemPriceList;
                    if (!inventory.TryGetValue(ItemId, out var dat) || !prices.TryGetValue(ItemId, out var price))
                        return;
                    text = $"{dat.ProperName()} : {price:N0}z";
                }

                if (Type == DragItemType.VendSetupSource)
                {
                    var inventory = NetworkManager.Instance.PlayerState.Cart.GetInventoryData();
                    if (!inventory.TryGetValue(ItemId, out var dat))
                        return;
                    text = dat.ProperName();
                }

                if (Type == DragItemType.Item)
                {
                    var inventory = NetworkManager.Instance.PlayerState.Inventory.GetInventoryData();
                    if (!inventory.TryGetValue(ItemId, out var dat))
                        return;
                    text = dat.ProperName();
                    // if (dat.ItemData.IsUnique)
                    // {
                    //     if (dat.ItemData.Slots == 0)
                    //         text = dat.ItemData.Name;
                    //     else
                    //         text = $"{dat.ItemData.Name} [{dat.ItemData.Slots}]";
                    // }
                    // else
                    // {
                    //     text = $"{dat.ItemData.Name}: {ItemCount} ea.";
                    // }
                }

                if (Type == DragItemType.SocketedItem)
                {
                    if (ItemId < 0)
                        return;
                    if (ClientDataLoader.Instance.TryGetItemById(ItemId, out var dat))
                        text = dat.Name;
                }

                UiManager.Instance.ShowTooltip(gameObject, text);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            UiManager.Instance.HideTooltip(gameObject);
        }
    }
}