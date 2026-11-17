using Assets.Scripts.UI.ConfigWindow;
using System.Collections.Generic;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.Classic.DragAndDrop;
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
        public static ClassicEquipmentWindow Instance { get; private set; }
        private readonly Dictionary<EquipSlot, DraggableGearSlot> slots = new();
        // GearPanel creates the left column first, then the right column.
        private static readonly EquipSlot[] DisplaySlotOrder =
        {
            EquipSlot.HeadTop,
            EquipSlot.HeadBottom,
            EquipSlot.RightHand,
            EquipSlot.Garment,
            EquipSlot.Accessory1,

            EquipSlot.HeadMid,
            EquipSlot.Body,
            EquipSlot.LeftHand,
            EquipSlot.Footgear,
            EquipSlot.Accessory2
        };

        public bool CanEquip(int bagId, EquipSlot target)
        {
            if (!PlayerState.Instance.IsValid || !PlayerState.Instance.Inventory.TryGetInventoryItem(bagId, out var item) || item.ItemData == null) return false;
            if (target == EquipSlot.Ammunition) return item.ItemData.ItemClass == ItemClass.Ammo;
            if (item.ItemData.ItemClass != ItemClass.Weapon && item.ItemData.ItemClass != ItemClass.Equipment) return false;
            var mask = target >= EquipSlot.Accessory1 ? EquipPosition.Accessory : (EquipPosition)(1 << (int)target);
            return (item.ItemData.Position & mask) != 0;
        }
        public void Equip(int bagId, EquipSlot target)
        {
            if (CanEquip(bagId, target)) Assets.Scripts.Network.NetworkManager.Instance?.SendEquipItem(bagId, target);
        }
        public void EquipAutomatically(int bagId)
        {
            var player = PlayerState.Instance;
            if (player.EquippedBagIdHashes.Contains(bagId) || player.AmmoId == bagId) return;
            if (CanEquip(bagId, EquipSlot.Accessory1))
            {
                Equip(bagId, player.EquippedItems[(int)EquipSlot.Accessory1] == 0 ? EquipSlot.Accessory1 : player.EquippedItems[(int)EquipSlot.Accessory2] == 0 ? EquipSlot.Accessory2 : EquipSlot.Accessory1);
                return;
            }
            // Double-click also works before the equipment window has rendered its slots.
            for (var i = (int)EquipSlot.HeadTop; i <= (int)EquipSlot.Accessory2; i++)
            {
                var slot = (EquipSlot)i;
                if (CanEquip(bagId, slot)) { Equip(bagId, slot); return; }
            }
        }

        public override void ShowWindow()
        {
            base.ShowWindow();
            UiManagerV2.Instance?.TrackOpen(this);
        }

        public override void HideWindow()
        {
            UiManagerV2.Instance?.TrackClosed(this);
            base.HideWindow();
        }

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
            UiManagerV2.Instance.RegisterWindow(WindowID.EQUIPMENT, this, UiStyle.Classic, KeyCode.Q);
        }

        private void Start()
        {
            PopulateSlots();
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


        private void PopulateSlots()
        {
            if (state == null || !state.IsValid) return;


            var inventory = state.Inventory.GetInventoryData();
            var equips = state.EquippedItems;

            for (var i = 0; i < gearPanel.slots.Count && i < DisplaySlotOrder.Length; i++)
            {
                var slot = gearPanel.slots[i] as DraggableGearSlot;
                if (slot == null) continue;
                var target = DisplaySlotOrder[i];
                var bagId = equips[(int)target];
                slot.Target = target;
                slots[target] = slot;

                // Zero means an empty equipment slot, not an inventory bag ID.
                if (bagId <= 0)
                {
                    slot.Clear();
                    continue;
                }
                if (!inventory.TryGetValue(bagId, out var item) || item.ItemData == null)
                {
                    slot.Clear();
                    continue;
                }

                slot.Assign(
                    ClientDataLoader.Instance.GetIconAtlasSprite(item.ItemData.Sprite),
                    item.ItemData.Name,
                    item.Count,
                    item.BagSlotId
                );

                slot.TryGetComponent<RO_Flex_UI.Components.TooltipTrigger>(out var tooltip);
                if (tooltip != null)
                {
                    tooltip.SetText($"{item.ItemData.Name}");
                }
            }
        }
    }
}
