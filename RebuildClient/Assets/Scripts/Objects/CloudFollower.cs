using UnityEngine;

namespace Assets.Scripts.Objects
{
    public class CloudFollower : MonoBehaviour
    {
        public CameraFollower Camera;
        
        public void Update()
        {
            if (Camera == null)
                Camera = CameraFollower.Instance;

            transform.position = new Vector3(Camera.TargetFollow.x, transform.position.y, Camera.TargetFollow.z);
        }
    }
}