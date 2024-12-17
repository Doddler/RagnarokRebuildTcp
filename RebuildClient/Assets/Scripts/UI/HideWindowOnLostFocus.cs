using UnityEngine;

namespace Assets.Scripts.UI
{
    public class HideWindowOnLostFocus : MonoBehaviour
    {
        public void Update()
        {
            if (transform != transform.parent.GetChild(transform.parent.childCount - 1))
            {
                Destroy(gameObject);
            }
        }
    }
}