using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public class SkillHotbarEntry : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IItemDropTarget
    {
        public SkillHotbar Parent;
        public UiManager UIManager;
        public DraggableItem DragItem;
        public GameObject HighlightImage;
        public Image FlashImage;
        public TextMeshProUGUI HotkeyText;
        public int Id;
        public bool CanDrag = true;

        public void Awake()
        {
            HighlightImage.SetActive(false);
        }

        public void PressKey()
        {
            FlashImage.gameObject.SetActive(true);
        }

        public void OnDoubleClick()
        {
            if (DragItem.Type == DragItemType.None)
                return;
            Parent.ActivateHotBarEntry(this);
        }

        public void ReleaseSkill()
        {
            FlashImage.gameObject.SetActive(false);
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanDrag)
                return;

            if (!IsValidItemType(UIManager.DragItemObject))
                return;
            
            HighlightImage.SetActive(true);
            
            if (UIManager.IsDraggingItem)
            {
                UIManager.RegisterDragTarget(this);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!CanDrag)
                return;
            
            
            if (!IsValidItemType(UIManager.DragItemObject))
                return;
            
            HighlightImage.SetActive(false);
            
            if (UIManager.IsDraggingItem)
            {
                UIManager.UnregisterDragTarget(this);
            }
        }

        private bool IsValidItemType(ItemDragObject dragObject)
        {
            if (dragObject.Type == DragItemType.Item || dragObject.Type == DragItemType.Skill)
                return true;
            return false;
        }

        public void DropItem()
        {
            Debug.Log($"Dropped item into {name}");
            var dragObject = UIManager.DragItemObject;
            if (dragObject.Origin == ItemDragOrigin.HotBar)
            {
                if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                {
                    var src = Parent.GetEntryById(dragObject.OriginId);
                    if (DragItem.Type == DragItemType.None)
                        src.DragItem.Clear();
                    else
                        src.DragItem.Assign(DragItem);
                }
            }

            if (IsValidItemType(dragObject))
            {
                DragItem.gameObject.SetActive(true);
                DragItem.Assign(dragObject);
            }

            HighlightImage.SetActive(false);
        }

        public void Clear()
        {
            if (!CanDrag)
                return;
            
            DragItem.Type = DragItemType.None;
            DragItem.gameObject.SetActive(false);
            HighlightImage.SetActive(false);
        }


    }
}