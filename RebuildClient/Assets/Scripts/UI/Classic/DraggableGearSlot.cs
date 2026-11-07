using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RO_Flex_UI.Components;
using RO_Flex_UI.Components.DragAndDrop;
using UnityEngine;

using FlexDragAndDrop = Assets.Scripts.UI.Classic.DragAndDrop;

namespace Assets.Scripts.UI.Classic
{
    public class DraggableGearSlot : IconText, FlexDragAndDrop.IItemDragSource, IDragTarget
    {
        int itemId;
        int amount;
        public int ItemId => itemId;
        public int Amount => amount;

        [SerializeField] protected bool canReplaceOnDrop = false;
        private RO_Flex_UI.Components.TooltipTrigger tooltipTrigger;

        protected override void Start()
        {
            base.Start();

            tooltipTrigger = GetComponent<RO_Flex_UI.Components.TooltipTrigger>();
        }

        public DragPayload CreatePayload()
        {
            var data = PlayerState.Instance.Inventory.TryGetInventoryItem(ItemId, out var item);
            return new DragPayload(this, Sprite, $"{item.ItemData.Name}");
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

            if (payload.source is not FlexDragAndDrop.IItemDragSource p)
            {
                Debug.LogWarning("Payload source is not an item drag source.");
                return;
            }

            if (!PlayerState.Instance.Inventory.TryGetInventoryItem(p.ItemId, out var data))
                throw new System.Exception("Item not found in inventory.");

            NetworkManager.Instance.SendEquipItem(p.ItemId);

            Assign(payload.sprite, data.ItemData.Name, data.Count, p.ItemId);
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

        public void Assign(Sprite sprite, string text, int amount, int itemId)
        {
            Assign(sprite, text, amount.ToString());
            this.itemId = itemId;
            this.amount = amount;
        }

        public void OnDrop()
        {
            throw new System.NotImplementedException();
        }

    }
}
