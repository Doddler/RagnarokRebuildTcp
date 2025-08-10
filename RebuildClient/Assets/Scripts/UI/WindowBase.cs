using Assets.Scripts.Utility;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI
{
    public class WindowBase : MonoBehaviour, IClosableWindow, IPointerDownHandler
    {
        public bool CanCloseWithEscape = true;
        public bool AutomaticallyFitIntoPlayArea = true;

        public void OnDestroy()
        {
            UiManager.Instance.WindowStack.Remove(this);
        }

        public virtual void CloseWindow()
        {
            HideWindow();
        }

        public bool CanCloseWindow()
        {
            return CanCloseWithEscape;
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            MoveToTop();
        }

        protected bool IsPointerOverUIObject()
        {
            return RectTransformUtility.RectangleContainsScreenPoint(transform.RectTransform(), Input.mousePosition);
        }

        public virtual void MoveToTop()
        {
            // Debug.Log($"MoveToTop {name}");
            transform.SetAsLastSibling(); //move to top
            UiManager.Instance.MoveToLast(this);
        }

        public void FitWindowIntoPlayArea()
        {
            if (!AutomaticallyFitIntoPlayArea)
                return;

            var rect = GetComponent<RectTransform>();

            var pos = transform.position;
            var halfx = rect.sizeDelta.x * rect.transform.lossyScale.x / 2f;

            pos = new Vector3(Mathf.Clamp(pos.x, -halfx, Screen.width - halfx),
                Mathf.Clamp(pos.y, rect.sizeDelta.y * rect.transform.lossyScale.y / 2f, Screen.height), pos.z);

            transform.position = pos;
        }

        public void ToggleVisibility()
        {
            if (UiManager.Instance.IsDraggingItem)
                UiManager.Instance.EndItemDrag(false);
            
            if (gameObject.activeInHierarchy)
                HideWindow();
            else
                ShowWindow();
        }

        public virtual void ShowWindow()
        {
            if (gameObject == null)
                return;
            gameObject.SetActive(true);
            var mgr = UiManager.Instance;
            if (!mgr.WindowStack.Contains(this))
                mgr.WindowStack.Add(this);

            FitWindowIntoPlayArea();
            transform.SetAsLastSibling(); //move to top

            ((RectTransform)transform).ForceUpdateRectTransforms();

            //Init();
        }

        public virtual void HideWindow()
        {
            if (gameObject == null)
                return;
            gameObject.SetActive(false);
            var mgr = UiManager.Instance;
            if (mgr.WindowStack.Contains(this))
            {
                mgr.WindowStack.Remove(this);
                mgr.ForceHideTooltip();
            }

            if (mgr.IsDraggingItem) mgr.EndItemDrag(false);

            // Debug.Log(name + " : " + gameObject.activeInHierarchy);
        }

        public void CenterWindow(int setHeight = 0)
        {
            //center window
            ((RectTransform)transform).ForceUpdateRectTransforms();
            var rect = gameObject.GetComponent<RectTransform>();
            if (setHeight > 0)
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, setHeight);
            var parentContainer = (RectTransform)gameObject.transform.parent;
            var middle = parentContainer.rect.size / 2f;
            middle = new Vector2(middle.x, -middle.y);
            rect.anchoredPosition = middle - new Vector2(rect.sizeDelta.x / 2, -rect.sizeDelta.y / 2);
        }

        public void CenterWindow(Vector2 center)
        {
            //center window
            ((RectTransform)transform).ForceUpdateRectTransforms();
            var rect = gameObject.GetComponent<RectTransform>();
            var parentContainer = (RectTransform)gameObject.transform.parent;
            var middle = parentContainer.rect.size * center;
            middle = new Vector2(middle.x, -middle.y);
            rect.anchoredPosition = middle - new Vector2(rect.sizeDelta.x / 2, -rect.sizeDelta.y / 2);
        }
        
        
        public void CenterWindowWithOffset(Vector2 offset)
        {
            //center window
            ((RectTransform)transform).ForceUpdateRectTransforms();
            var rect = gameObject.GetComponent<RectTransform>();
            var parentContainer = (RectTransform)gameObject.transform.parent;
            var middle = parentContainer.rect.size / 2f;
            middle = new Vector2(middle.x, -middle.y);
            rect.anchoredPosition = middle - new Vector2(offset.x, -offset.y);
        }


        protected void AttachToMainUI()
        {
            transform.SetParent(UiManager.Instance.PrimaryUserWindowContainer);
            transform.localScale = Vector3.one;
        }
    }
}