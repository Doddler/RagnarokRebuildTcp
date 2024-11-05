using RebuildSharedData.ClientTypes;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.Hud
{
    public class ItemObtainedToast : MonoBehaviour
    {
        public GameObject Container;
        public RectTransform Rect;
        public TextMeshProUGUI Text;

        private float activeTime;

        public void SetText(int itemId, int itemCount)
        {
            
        }
    }
}