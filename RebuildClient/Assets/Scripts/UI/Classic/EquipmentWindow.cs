using RO_Flex_UI.Panels;
using UnityEngine;

namespace Assets.Scripts.UI.Classic
{
    public class EquipmentWindow : Window, IStyledWindow
    {
        protected override void Awake()
        {
            base.Awake();
            UiManagerV2.Instance.RegisterWindow(WindowID.EQUIPMENT, this, KeyCode.Q);
        }

        private void Start()
        {
            HideWindow();
        }
    }
}