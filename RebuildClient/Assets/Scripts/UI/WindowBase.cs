using UnityEngine;

namespace Assets.Scripts.UI
{
    public class WindowBase : MonoBehaviour, IClosableWindow
    {
        public void MoveToTop()
        {
            UiManager.Instance.MoveToLast(this);
        }

        public void ShowWindow()
        {
            gameObject.SetActive(true);
            var mgr = UiManager.Instance;
            if (!mgr.WindowStack.Contains(this))
                mgr.WindowStack.Add(this);

            transform.SetAsLastSibling(); //move to top
            
            //Init();
        }

        public void HideWindow()
        {
            gameObject.SetActive(false);
            var mgr = UiManager.Instance;
            if (mgr.WindowStack.Contains(this))
                mgr.WindowStack.Remove(this);
        }
    }
}
