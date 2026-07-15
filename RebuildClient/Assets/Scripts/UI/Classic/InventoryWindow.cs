
using Assets.Scripts.PlayerControl;
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

        private bool initialLoad = false;

        protected override void Awake()
        {
            base.Awake();
            UiManagerV2.Instance.RegisterWindow(WindowID.INVENTORY, this, KeyCode.E);
        }

        private void Start()
        {
            LoadInventoryData();
            HideWindow();
        }

        public void OnEnable()
        {
            LoadInventoryData();
        }

        private bool LoadInventoryData()
        {
            if (PlayerState.Instance == null) return false;

            var inventory = PlayerState.Instance?.Inventory?.GetInventoryData();
            if (inventory == null) return false;

            var itemDataLoader = Sprites.ClientDataLoader.Instance;
            if (itemDataLoader == null) return false;

            Debug.Log($"Inventory {inventory.Count}");

            var index = 0;
            foreach (var itemData in inventory.Values)
            {
                var itemName = itemData.ItemData.Name;
                var itemSprite = itemDataLoader.GetIconAtlasSprite(itemData.ItemData.Sprite);
                var itemAmount = itemData.Count;

                var cell = itemPanel.GetCell<IconAmount>(index);
                var tooltip = itemPanel.GetCell<RO_Flex_UI.Components.TooltipTrigger>(index);
                cell.Assign(itemSprite, itemAmount.ToString());
                cell.SetActive(true);
                tooltip.OnTrigger += () =>
                {
                    tooltip.Enabled = cell.IsVisible;
                    tooltip.SetText($"{itemName}: {itemAmount} un.");
                };
                index++;
            }
            itemPanel.SetFilledCells(index);
            itemPanel.Refresh();

            initialLoad = true;
            return true;
        }

    }
}