using Assets.Scripts.UI.ConfigWindow;
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

            UiManagerV2.Instance.RegisterWindow(WindowID.MAIN_MENU, this, UiStyle.Classic, KeyCode.Escape);
        }

        private void Start()
        {
            SetupButtons();
            HideWindow();
        }

        private void SetupButtons()
        {

            buttons[0].onClick.AddListener(() => UiManagerV2.Instance.Get(WindowID.STATS)?.ToggleVisibility());
            buttons[1].onClick.AddListener(() => UiManagerV2.Instance.Get(WindowID.SKILLS)?.ToggleVisibility());
            buttons[2].onClick.AddListener(() => UiManagerV2.Instance.Get(WindowID.INVENTORY)?.ToggleVisibility());
            buttons[3].onClick.AddListener(() => UiManagerV2.Instance.Get(WindowID.EQUIPMENT)?.ToggleVisibility());
            buttons[4].onClick.AddListener(() => UiManagerV2.Instance.Get(WindowID.HOTBAR)?.ToggleVisibility());
            buttons[5].onClick.AddListener(() => UiManagerV2.Instance.Get(WindowID.EMOTES)?.ToggleVisibility());
            buttons[6].onClick.AddListener(() => UiManagerV2.Instance.Get(WindowID.OPTIONS)?.ToggleVisibility());
            buttons[7].onClick.AddListener(() => UiManagerV2.Instance.Get(WindowID.HELP)?.ToggleVisibility());
            buttons[8].onClick.AddListener(() => UiManagerV2.Instance.Get(WindowID.CLIENT_DATABASE)?.ToggleVisibility());
            buttons[9].onClick.AddListener(Application.Quit);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            headerPanel.OnCloseButtonClick.AddListener(HideWindow);
            ShowWindow();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            headerPanel.OnCloseButtonClick.RemoveListener(HideWindow);
            HideWindow();
        }

        public override void ShowWindow()
        {
            // var mgr = UiManager.Instance;

            // if (!mgr.WindowStack.Contains(this))
            //     mgr.WindowStack.Add(this);

            base.ShowWindow();
            UiManagerV2.Instance?.TrackOpen(this);
        }

        public override void HideWindow()
        {
            var mgr = UiManager.Instance;

            if (mgr.WindowStack.Contains(this))
                mgr.WindowStack.Remove(this);

            UiManagerV2.Instance?.TrackClosed(this);
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