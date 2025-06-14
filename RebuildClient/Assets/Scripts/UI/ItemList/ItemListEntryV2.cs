using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{

    
    public class ItemListEntryV2 : DragItemBase, IDragHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public GenericItemListV2 Parent;
        public GameObject ImageDisplayGroup;
        public Image Background;
        public TextMeshProUGUI ItemName;
        public TextMeshProUGUI RightText;
        
        [NonSerialized] public ItemDragOrigin DragOrigin;
        [NonSerialized] public int UniqueEntryId;
        [NonSerialized] public bool CanDrag;
        [NonSerialized] public bool CanSelect;
        [NonSerialized] public bool IsSelected;
        [NonSerialized] public bool IsActive = true;
        
        //#D5E8FF00
        public Color HoverColor = new Color(0.83f, 0.91f, 1f, 1f); 
        public Color SelectedColor = new Color(0.72f, 0.856f, 1f, 1f); 
        public Color NormalColor = new Color(0.83f, 0.91f, 1f, 0f);

        public Action<int> EventOnClick;
        public Action<int> EventOnSelect;
        public Action<int> EventDoubleClick;
        public Action<int> EventOnRightClick;
        

        public void Awake()
        {
            OnPointerExit(null);
        }

        public void Unselect()
        {
            IsSelected = false;
            Background.color = NormalColor;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!CanDrag || !IsActive)
                return;
            // throw new System.NotImplementedException();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if(!IsSelected && IsActive)
                Background.color = HoverColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if(!IsSelected && IsActive)
                Background.color = NormalColor;
        }
        
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!CanDrag || !IsActive)
                return;
            
            UiManager.Instance.StartItemDrag(this);
            UiManager.Instance.DragItemObject.Origin = DragOrigin;
            UiManager.Instance.DragItemObject.OriginId = UniqueEntryId;
            Image.enabled = false;
            CountText.enabled = false;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            UiManager.Instance.EndItemDrag();

            Image.enabled = true;
            CountText.enabled = true;
        }

        public void ResolveLeftClick()
        {
            if (CanSelect)
            {
                IsSelected = true;
                Background.color = SelectedColor;
                EventOnSelect?.Invoke(UniqueEntryId);
            }
            else
            {
                EventOnClick?.Invoke(UniqueEntryId);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsActive)
                return;
            
            if (EventOnRightClick != null && eventData.button == PointerEventData.InputButton.Right && Type != DragItemType.Skill)
                EventOnRightClick(UniqueEntryId);
            
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            if (eventData.clickCount > 1 && EventDoubleClick != null)
            {
                EventDoubleClick(UniqueEntryId);
                return;
            }

            ResolveLeftClick();
        }
    }
}