using RO_Flex_UI.Components;
using RO_Flex_UI.Panels;
using RO_Flex_UI.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.UI.Classic
{
    public class MainMenuWindow : Window, IStyledWindow, IClosableWindow
    {
        [SerializeField] private Header headerPanel;
        [SerializeField] private List<RoButton> buttons;

        protected override void Awake()
        {
            base.Awake();

            if (!Tools.IsValid(this, headerPanel)) return;

            // UiManagerV2.Instance.RegisterWindow(WindowID.MAIN_MENU, this, KeyCode.Escape);

            HideWindow();
        }

        private void Start()
        {
            SetupButtons();
        }

        private void SetupButtons()
        {
            var emotes = UiManager.Instance.EmoteManager.GetComponent<EmoteWindow>(); // don't ask me why

            buttons[0].onClick.AddListener(UiManager.Instance.StatusWindow.ToggleVisibility);
            buttons[1].onClick.AddListener(UiManager.Instance.SkillManager.ToggleVisibility);
            buttons[2].onClick.AddListener(UiManager.Instance.InventoryWindow.ToggleVisibility);
            buttons[3].onClick.AddListener(UiManager.Instance.EquipmentWindow.ToggleVisibility);
            buttons[4].onClick.AddListener(UiManager.Instance.SkillHotbar.ToggleVisibility);
            buttons[5].onClick.AddListener(emotes.ToggleVisibility);
            buttons[6].onClick.AddListener(UiManager.Instance.ConfigManager.ToggleVisibility);
            buttons[7].onClick.AddListener(UiManager.Instance.HelpWindow.ToggleVisibility);
            buttons[8].onClick.AddListener(UiManager.Instance.ClientDatabaseWindow.ToggleVisibility);
            buttons[9].onClick.AddListener(Application.Quit);
        }

        private void OnEnable()
        {
            headerPanel.OnCloseButtonClick.AddListener(HideWindow);
            ShowWindow();
        }

        private void OnDisable()
        {
            headerPanel.OnCloseButtonClick.RemoveListener(HideWindow);
            HideWindow();
        }

        public override void ShowWindow()
        {
            var mgr = UiManager.Instance;

            if (!mgr.WindowStack.Contains(this))
                mgr.WindowStack.Add(this);

            base.ShowWindow();
        }

        public override void HideWindow()
        {
            var mgr = UiManager.Instance;

            if (mgr.WindowStack.Contains(this))
                mgr.WindowStack.Remove(this);

            base.HideWindow();
        }

        public void CloseWindow()
        {
            HideWindow();
        }

        public bool CanCloseWindow()
        {
            return true;
        }

    }
}