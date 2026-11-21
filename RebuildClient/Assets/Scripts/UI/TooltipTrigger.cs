using UnityEngine.EventSystems;

namespace Assets.Scripts.UI
{
    public class TooltipTrigger : UIBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string TooltipText;

        private bool isShowing;

        //also updates the tooltip while it's showing
        public void SetText(string text)
        {
            TooltipText = text;
            if (isShowing)
                UiManager.Instance.ShowTooltip(gameObject, TooltipText);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (UiManager.Instance.IsDraggingItem) return;
            isShowing = true;
            UiManager.Instance.ShowTooltip(gameObject, TooltipText);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isShowing = false;
            UiManager.Instance.HideTooltip(gameObject);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            isShowing = false;
            if (UiManager.Instance != null)
            {
                UiManager.Instance.HideTooltip(gameObject);
            }
        }
    }
}
