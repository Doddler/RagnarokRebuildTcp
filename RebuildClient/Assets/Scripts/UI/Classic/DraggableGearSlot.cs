using Assets.Scripts.Sprites;
using RO_Flex_UI.Components;
using RO_Flex_UI.Components.DragAndDrop;
using UnityEngine;

namespace Assets.Scripts.UI.Classic
{
    public class DraggableGearSlot : IconText, IDragSource, IDragTarget
    {
        int itemId;
        public int ItemId => itemId;

        [SerializeField] protected bool canReplaceOnDrop = false;
        private RO_Flex_UI.Components.TooltipTrigger tooltipTrigger;

        protected override void Start()
        {
            base.Start();

            tooltipTrigger = GetComponent<RO_Flex_UI.Components.TooltipTrigger>();
        }

        public DragPayload CreatePayload()
        {
            var data = ClientDataLoader.Instance.GetItemById(ItemId);
            return new DragPayload(this, Sprite, "", new object[] { ItemId, Amount });
        }

        public bool CanDrag()
        {
            return !Empty;
        }

        public bool CanDrop(DragPayload payload)
        {
            return Empty;
        }

        public void OnDropComplete(DragPayload payload)
        {
            if (!Empty) return;
            var objects = (object[])payload.data;
            Assign(payload.sprite, payload.text, (string)objects[1], (int)objects[0]);
            SetActive(true);
        }

        public void OnDragComplete()
        {
            Clear();
            Debug.Log("DRAG COMPLETE");
        }

        public override void Clear()
        {
            base.Clear();

            if (tooltipTrigger == null)
                tooltipTrigger = GetComponent<RO_Flex_UI.Components.TooltipTrigger>();
            if (tooltipTrigger != null)
                tooltipTrigger.Enabled = false;
        }

        public override void Assign(Sprite sprite, string text, string amount = "")
        {
            base.Assign(sprite, text, amount);

            if (tooltipTrigger == null)
                tooltipTrigger = GetComponent<RO_Flex_UI.Components.TooltipTrigger>();
            if (tooltipTrigger != null)
            {
                tooltipTrigger.SetText($"{text}: {amount}");
                tooltipTrigger.Enabled = true;
            }
        }

        public void Assign(Sprite sprite, string text, string amount, object data)
        {
            Assign(sprite, text, amount);
            itemId = (int)data;
        }
    }
}
