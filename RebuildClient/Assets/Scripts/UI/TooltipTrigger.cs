using UnityEngine.EventSystems;

namespace Assets.Scripts.UI
{
    public class TooltipTrigger : UIBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string TooltipText;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (UiManager.Instance.IsDraggingItem) return;
            UiManager.Instance.ShowTooltip(gameObject, TooltipText);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            UiManager.Instance.HideTooltip(gameObject);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (UiManager.Instance != null)
            {
                UiManager.Instance.HideTooltip(gameObject);
            }
        }
    }
}
