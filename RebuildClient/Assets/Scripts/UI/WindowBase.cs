using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public class WindowBase : MonoBehaviour, IClosableWindow, IPointerDownHandler
    {
        public virtual void MoveToTop()
        {
            // Debug.Log($"MoveToTop {name}");
            transform.SetAsLastSibling(); //move to top
            UiManager.Instance.MoveToLast(this);
        }
        
        public void FitWindowIntoPlayArea()
        {

            var rect = GetComponent<RectTransform>();

            var pos = transform.position;
            var halfx = rect.sizeDelta.x * rect.transform.lossyScale.x / 2f;

            pos = new Vector3(Mathf.Clamp(pos.x, -halfx, Screen.width - halfx), Mathf.Clamp(pos.y, rect.sizeDelta.y * rect.transform.lossyScale.y / 2f, Screen.height), pos.z);

            transform.position = pos;
        }

        public void ToggleVisibility()
        {
            if(gameObject.activeInHierarchy)
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

            if (mgr.IsDraggingItem)
            {
                mgr.EndItemDrag(false);
            }

            // Debug.Log(name + " : " + gameObject.activeInHierarchy);
        }

        public void CenterWindow(int setHeight = 0)
        {
            //center window
            ((RectTransform)transform).ForceUpdateRectTransforms();
            var rect = gameObject.GetComponent<RectTransform>();
            if(setHeight > 0)
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, setHeight);
            var parentContainer = (RectTransform)gameObject.transform.parent;
            var middle = parentContainer.rect.size / 2f;
            middle = new Vector2(middle.x, -middle.y);
            rect.anchoredPosition = middle - new Vector2(rect.sizeDelta.x / 2, -rect.sizeDelta.y / 2);
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            MoveToTop();
        }
    }
}
