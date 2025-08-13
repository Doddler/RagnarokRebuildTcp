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
            
            ItemText.text = $"{item.ItemName}: {item.Count} ea.";
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
            var screenPos = cf.Camera.WorldToScreenPoint(Item.transform.position);
            //var screenPos = Input.mousePosition;
            
            var d = 70 / cf.Distance;
            var reverseScale = 1f / cf.CanvasScaler.scaleFactor;

            if (!GameConfig.Data.ScalePlayerDisplayWithZoom)
                d = 1f;
            
            rect.localScale = new Vector3(d, d, d);
            rect.anchoredPosition = new Vector2(screenPos.x * reverseScale, (screenPos.y - cf.UiCanvas.pixelRect.height) * reverseScale);
        }

        public void LateUpdate()
        {
            SnapDialog();
        }
    }
}