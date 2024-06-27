using System;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI
{
    public class SkillDragSource : DragItemBase, IDragHandler, IBeginDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public SkillWindowEntry Entry;
        private UiManager manager;

        public void Awake()
        {
            manager = UiManager.Instance;
        }
        
        public void OnBeginDrag(PointerEventData eventData)
        {
            manager.StartItemDrag(this);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            manager.EndItemDrag();
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Debug.Log("HII");
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.clickCount != 2)
                return;
            if(CameraFollower.Instance.PressSkillButton((CharacterSkill)ItemId, ItemCount))
                Entry.HighlightSkillBox();
        }

    }
}