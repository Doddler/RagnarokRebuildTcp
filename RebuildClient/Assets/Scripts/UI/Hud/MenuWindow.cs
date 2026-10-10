using Assets.Scripts.PlayerControl;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Hud
{
    public class MenuWindow : MonoBehaviour
    {
        public GameObject CartRow;
        public RectTransform MenuButton;   //the opener; excluded from the click-outside check so it can toggle

        public void Awake()
        {
            //every child button also closes the menu - no per-row wiring needed
            foreach (var row in GetComponentsInChildren<Button>(true))
                row.onClick.AddListener(Close);
        }

        public void Toggle()
        {
            if (gameObject.activeSelf)
                Close();
            else
                Show();
        }

        public void Show()
        {
            RefreshRows();      //toggle rows before showing so the window is sized correctly on the first frame
            gameObject.SetActive(true);
        }

        public void Close() => gameObject.SetActive(false);

        public void RefreshRows()
        {
            CartRow.SetActive(PlayerState.Instance.HasCart);
        }

        public void Update()
        {
            if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1))
                return;

            var pos = Input.mousePosition;
            if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, pos))
                return; //clicked inside the menu (e.g. a row) - let the row's own handler run

            if (MenuButton && RectTransformUtility.RectangleContainsScreenPoint(MenuButton, pos))
                return; //clicked the opener - let Toggle handle it, don't double-close

            Close();
        }
    }
}
