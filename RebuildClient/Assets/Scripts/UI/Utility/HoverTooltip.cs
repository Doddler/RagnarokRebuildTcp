using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI.Utility
{
    public class HoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [TextArea]
        public string Text;

        public bool HideWhileDraggingItem = true;

        public void OnPointerEnter(PointerEventData eventData)
        {
            var mgr = global::UiManager.Instance;

            if (HideWhileDraggingItem && mgr.IsDraggingItem)
                return;

            mgr.ShowTooltip(gameObject, Text);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            var mgr = global::UiManager.Instance;
            mgr.HideTooltip(gameObject);
        }

        public void OnDisable()
        {
            var mgr = global::UiManager.Instance;
            mgr.HideTooltip(gameObject);
        }

        public void OnDestroy()
        {
            var mgr = global::UiManager.Instance;
            mgr.HideTooltip(gameObject);
        }
    }
}
