using UnityEngine;

namespace Assets.Scripts.Sprites
{
    public class Billboard : MonoBehaviour
    {
        public static bool UseOldStyle = false;

        public void LateUpdate()
        {
            //if (UseOldStyle)
            {
                //transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                transform.localRotation = Camera.main.transform.rotation;
                return;
            }
        }
    }
}



