using RO_Flex_UI.Components;
using RO_Flex_UI.Components.DragAndDrop;
using UnityEngine;


namespace Assets.Scripts.UI.Classic
{
    public class DraggableSkill : SkillEntry, IDragSource
    {
        public void Start()
        {
            spriteButton.onClick.AddListener(() =>
            {
                Debug.Log($"[DRAGGABLE] Skill Clicked");
            });
        }

        public DragPayload CreatePayload()
        {
            return new DragPayload(this, Sprite, "");
        }

        public bool CanDrag()
        {
            return true;
        }

        public void OnDragComplete()
        {
        }
    }
}