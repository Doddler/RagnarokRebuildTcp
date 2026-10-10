using Assets.Scripts.Utility;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI
{
    public class WindowBase : MonoBehaviour, IClosableWindow, IPointerDownHandler
    {
        public bool CanCloseWithEscape = true;

        public virtual void OnDestroy()
        {
            if (UiManager.Instance)
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
            transform.position = transform.RectTransform().ClampFullyOnScreen(transform.position);
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
            var rect = (RectTransform)transform;
            if (setHeight > 0)
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, setHeight);
            PlaceAt(new Vector2(0.5f, 0.5f), rect.rect.size / 2f);
        }

        public void CenterWindow(Vector2 center)
        {
            PlaceAt(center, ((RectTransform)transform).rect.size / 2f);
        }


        public void CenterWindowWithOffset(Vector2 offset)
        {
            PlaceAt(new Vector2(0.5f, 0.5f), offset);
        }

        //Centres the rect on a normalised point of its parent, measured from the top-left.
        private void PlaceAt(Vector2 normalized, Vector2 halfSize)
        {
            var rect = (RectTransform)transform;
            rect.ForceUpdateRectTransforms();

            var parent = (RectTransform)transform.parent;
            var pr = parent.rect;

            var target = pr.min + Vector2.Scale(new Vector2(normalized.x, 1f - normalized.y), pr.size);
            var anchorRef = pr.min + Vector2.Scale(rect.anchorMin, pr.size);
            //anchoredPosition places the pivot, which isn't always the rect's middle
            var pivotBias = Vector2.Scale(rect.pivot - new Vector2(0.5f, 0.5f), halfSize * 2f);

            rect.anchoredPosition = target + pivotBias - anchorRef;
        }


        protected void AttachToMainUI()
        {
            transform.SetParent(UiManager.Instance.PrimaryUserWindowContainer);
            transform.localScale = Vector3.one;
        }
    }
}
