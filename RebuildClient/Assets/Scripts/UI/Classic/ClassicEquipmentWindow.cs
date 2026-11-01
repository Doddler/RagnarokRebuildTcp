using System.Collections.Generic;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using RO_Flex_UI.Components;
using RO_Flex_UI.Panels;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Classic
{
    public class ClassicEquipmentWindow : Window, IStyledWindow
    {
        [SerializeField] private GearPanel gearPanel;
        private PlayerState state;
        private bool dirty = true;

        protected override void Awake()
        {
            base.Awake();
            UiManagerV2.Instance.RegisterWindow(WindowID.EQUIPMENT, this, KeyCode.Q);
        }

        private void Start()
        {
            HideWindow();
        }
        protected override void OnEnable()
        {
            base.OnEnable();
            state = PlayerState.Instance;
            state.OnEquipmentChanged += PopulateSlots;
            if (state.IsValid) PopulateSlots();
        }
        private void OnDisable()
        {
            if (state != null) state.OnEquipmentChanged -= PopulateSlots;
            state = null;
        }
        private void MarkDirty() => dirty = true;

        private void PopulateSlots()
        {
            if (state == null || !state.IsValid) return;

            Debug.Log("GEAR CHANGED");
            var inventory = state.Inventory.GetInventoryData();
            var equips = state.EquippedItems;

            var i = 0;
            foreach (var bagId in equips)
            {
                Debug.Log($"idx {i} bagId {bagId}");
                var slot = gearPanel.slots[i] as DraggableGearSlot;

                // Zero means an empty equipment slot, not an inventory bag ID.
                if (bagId <= 0)
                {
                    slot.Clear();
                    i++;
                    continue;
                }
                if (!inventory.TryGetValue(bagId, out var item) || item.ItemData == null)
                {
                    Debug.LogWarning($"Equipped bag {bagId} has no resolved inventory item.", this);
                    continue;
                }

                slot.Assign(
                    ClientDataLoader.Instance.GetIconAtlasSprite(item.ItemData.Sprite),
                    item.ItemData.Name,
                    item.Count.ToString(),
                    item.ItemData.Id
                );

                slot.TryGetComponent<RO_Flex_UI.Components.TooltipTrigger>(out var tooltip);
                if (tooltip != null)
                {
                    tooltip.SetText($"{item.ItemData.Name}");
                }
                i++;
            }
        }
    }
}
