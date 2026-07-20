using Assets.Scripts.Network;
using Assets.Scripts.UI.ConfigWindow;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.Hud
{
    public class ItemOverlay : MonoBehaviour
    {
        public TextMeshProUGUI ItemText;
        public GroundItem Item;

        public void ShowItem(GroundItem item)
        {
            if (item == Item)
                return;
            if (item.Count == 1)
                ItemText.text = $"{item.ItemName}";
            else
                ItemText.text = $"{item.Count}x {item.ItemName}";
            gameObject.SetActive(true);
            Item = item;
        }

        public void HideItem()
        {
            Item = null;
            gameObject.SetActive(false);
        }

        public void SnapDialog()
        {
            if (Item == null)
                return;

            var cf = CameraFollower.Instance;
            var rect = transform as RectTransform;
            var canvasRect = cf.UiCanvas.transform as RectTransform;

            var screenPos = cf.Camera.WorldToScreenPoint(Item.transform.position);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                null,
                out var localPoint
            );

            rect.anchoredPosition = localPoint;

            var d = 70 / cf.Distance;

            if (!GameConfig.Data.ScalePlayerDisplayWithZoom)
                d = 1f;

            rect.localScale = new Vector3(d, d, d);
        }

        public void LateUpdate()
        {
            SnapDialog();
        }
    }
}