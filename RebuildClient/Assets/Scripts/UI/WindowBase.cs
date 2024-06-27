using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public class WindowBase : MonoBehaviour, IClosableWindow
    {
        public void MoveToTop()
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
            gameObject.SetActive(true);
            var mgr = UiManager.Instance;
            if (!mgr.WindowStack.Contains(this))
                mgr.WindowStack.Add(this);

            FitWindowIntoPlayArea();
            transform.SetAsLastSibling(); //move to top
            
            ((RectTransform)transform).ForceUpdateRectTransforms();
            
            //Init();
        }

        public void HideWindow()
        {
            gameObject.SetActive(false);
            var mgr = UiManager.Instance;
            if (mgr.WindowStack.Contains(this))
                mgr.WindowStack.Remove(this);
            
            // Debug.Log(name + " : " + gameObject.activeInHierarchy);
        }
    }
}
