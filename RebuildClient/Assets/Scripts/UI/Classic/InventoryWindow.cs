using System.Collections.Generic;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RO_Flex_UI.Components;
using RO_Flex_UI.Panels;
using UnityEngine;

namespace Assets.Scripts.UI.Classic
{
    public class InventoryWindow : Window, IStyledWindow
    {
        [SerializeField] private FillPanel itemPanel;
        [SerializeField] private FillPanel gearPanel;
        [SerializeField] private FillPanel etcPanel;
        [SerializeField] private RoText totalText;

        private ClientInventory inventory;
        private readonly HashSet<FillPanel> pendingPanels = new();
        private readonly Dictionary<FillPanel, int> renderedCellCounts = new();
        private readonly List<InventoryItem> items = new();

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
            UiManagerV2.Instance.RegisterWindow(WindowID.INVENTORY, this, KeyCode.E);
            if (GetComponent<ClassicInventoryDropZone>() == null)
                gameObject.AddComponent<ClassicInventoryDropZone>();
        }

        private void Start() => HideWindow();

        protected override void OnEnable()
        {
            base.OnEnable();
            BindInventory();
            PlayerState.Instance.OnEquipmentChanged += RefreshInventory;
            RefreshInventory();
        }

        private void OnDisable()
        {
            PlayerState.Instance.OnEquipmentChanged -= RefreshInventory;
            if (inventory != null)
                inventory.OnChanged -= RefreshInventory;
            inventory = null;
        }

        private void BindInventory()
        {
            var next = PlayerState.Instance?.Inventory;
            if (next == inventory)
                return;
            if (inventory != null)
                inventory.OnChanged -= RefreshInventory;
            inventory = next;
            if (inventory != null)
                inventory.OnChanged += RefreshInventory;
            RefreshInventory();
        }

        public void RefreshInventory()
        {
            pendingPanels.Add(itemPanel);
            pendingPanels.Add(gearPanel);
            pendingPanels.Add(etcPanel);

            if (totalText != null && inventory != null)
                totalText.SetText($"{inventory.GetInventoryData().Count,3}/100");
        }

        protected new void LateUpdate()
        {
            base.LateUpdate();
            BindInventory();
            var loader = ClientDataLoader.Instance;
            if (loader == null || !loader.IsInitialized)
                return;

            RenderPanel(itemPanel, InventoryCategory.Consumables, loader);
            RenderPanel(gearPanel, InventoryCategory.Gear, loader);
            RenderPanel(etcPanel, InventoryCategory.Etc, loader);
        }

        private void RenderPanel(FillPanel panel, InventoryCategory category, ClientDataLoader loader)
        {
            // Inactive tabs cannot create their cells until they are shown.
            if (panel == null || !panel.isActiveAndEnabled)
                return;
            if (!pendingPanels.Contains(panel)
                && renderedCellCounts.TryGetValue(panel, out var count) && count == panel.Cells.Count)
                return;

            items.Clear();
            if (inventory != null)
                foreach (var item in inventory.GetInventoryData().Values)
                    if (item.ItemData != null && item.Category == category)
                        items.Add(item);

            panel.SetFilledCells(items.Count);
            panel.Refresh();
            if (panel.Cells.Count == 0)
                return;

            for (var i = 0; i < panel.Cells.Count; i++)
            {
                if (!panel.TryGetCell<IconAmount>(i, out var icon))
                    continue;

                var filled = i < items.Count;
                if (icon is DragAndDrop.DraggableItem draggable) draggable.ItemId = filled ? items[i].BagSlotId : 0;
                if (filled)
                    icon.Assign(loader.GetIconAtlasSprite(items[i].ItemData.Sprite), items[i].Count.ToString());
                else
                    icon.Clear();
                icon.SetActive(filled);

                if (panel.TryGetCell<RO_Flex_UI.Components.TooltipTrigger>(i, out var tooltip))
                {
                    tooltip.Enabled = filled;
                    var equipped = filled && (PlayerState.Instance.EquippedBagIdHashes.Contains(items[i].BagSlotId)
                        || PlayerState.Instance.AmmoId == items[i].BagSlotId);
                    tooltip.SetText(filled ? $"{items[i].ProperName()}: {items[i].Count} un. {(equipped ? " (Equipped)" : "")}" : string.Empty);
                }
            }
            if (items.Count > panel.Cells.Count)
                Debug.LogWarning($"Inventory tab {category} has more items than its grid capacity.", this);
            pendingPanels.Remove(panel);
            renderedCellCounts[panel] = panel.Cells.Count;
        }
    }
}
