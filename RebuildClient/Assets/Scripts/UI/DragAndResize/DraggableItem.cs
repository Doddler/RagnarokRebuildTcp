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

        private UiManager manager;

        public void Awake()
        {
            manager = UiManager.Instance;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            manager.StartItemDrag(this);
            manager.DragItemObject.Origin = Origin;
            manager.DragItemObject.OriginId = OriginId;
            Image.enabled = false;
            CountText.enabled = false;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            manager.EndItemDrag();

            Image.enabled = true;
            CountText.enabled = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Debug.Log("HII");
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Type != DragItemType.None && eventData.clickCount >= 2 && OnDoubleClick != null)
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
                    if (skill.AdjustableLevel)
                        text = $"{skill.Name} Lv {ItemCount}";
                    else
                        text = skill.Name;
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

                UiManager.Instance.ShowTooltip(gameObject, text);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            UiManager.Instance.HideTooltip(gameObject);
        }
    }
}