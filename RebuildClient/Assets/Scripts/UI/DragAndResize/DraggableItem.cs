using System;
using Assets.Scripts.Network;
using Assets.Scripts.SkillHandlers;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        SocketedItem
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
                                     && (Type == DragItemType.Item || Type == DragItemType.Equipment || Type == DragItemType.SocketedItem))
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
                        var lvl = Mathf.Clamp(ItemCount, 1, skill.SpCost.Length);
                        spCost = $" (SP: {skill.SpCost[lvl - 1]})";
                    }

                    if (skill.AdjustableLevel)
                        text = $"{skill.Name} Lv {ItemCount}{spCost}";
                    else
                        text = $"{skill.Name}{spCost}";
                    
                }

                if (Type == DragItemType.Item)
                {
                    var inventory = NetworkManager.Instance.PlayerState.Inventory.GetInventoryData();
                    if (!inventory.TryGetValue(ItemId, out var dat))
                        return;
                    text = dat.ToString();
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