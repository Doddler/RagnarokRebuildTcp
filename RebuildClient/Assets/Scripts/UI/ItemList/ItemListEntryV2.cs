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
        public Image Background;
        public TextMeshProUGUI ItemName;
        public TextMeshProUGUI RightText;

        public int UniqueEntryId;
        public bool CanDrag;
        public bool CanSelect;
        public bool IsSelected;
        
        //#D5E8FF00
        public Color HoverColor = new Color(0.83f, 0.91f, 1f, 1f); 
        public Color SelectedColor = new Color(0.72f, 0.856f, 1f, 1f); 
        public Color NormalColor = new Color(0.83f, 0.91f, 1f, 0f);

        public Action<int> EventOnSelect;
        public Action<int> EventDoubleClick;
        

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
            if (!CanDrag)
                return;
            throw new System.NotImplementedException();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if(!IsSelected)
                Background.color = HoverColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if(!IsSelected)
                Background.color = NormalColor;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            throw new System.NotImplementedException();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            throw new System.NotImplementedException();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            if (eventData.clickCount > 1 && EventDoubleClick != null)
            {
                EventDoubleClick(UniqueEntryId);
                return;
            }

            if (CanSelect)
            {
                IsSelected = true;
                Background.color = SelectedColor;
                EventOnSelect(UniqueEntryId);
            }


        }
    }
}