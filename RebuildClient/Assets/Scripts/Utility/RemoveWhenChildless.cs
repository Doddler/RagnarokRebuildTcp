using UnityEngine;

namespace Assets.Scripts.Utility
{
    public class RemoveWhenChildless : MonoBehaviour
    {
        public void Update()
        {
            if(transform.childCount == 0)
                Destroy(gameObject);
        }
    }
}