using UnityEngine;
using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.UI.Classic;
using Assets.Scripts.UI;

public class HelpWindow : WindowBase, IStyledWindow
{
        public override void HideWindow()
        {
            base.HideWindow();
            UiManagerV2.Instance?.TrackClosed(this);
        }

        public override void ShowWindow()
        {
            base.ShowWindow();
            UiManagerV2.Instance?.TrackOpen(this);
        }

        private void Awake()
        {
            UiManagerV2.Instance.RegisterWindow(WindowID.HELP, this, UiStyle.Modern);
        }

        public bool IsVisible => gameObject.activeSelf;

    
}
