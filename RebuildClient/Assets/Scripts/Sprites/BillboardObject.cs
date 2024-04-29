using UnityEngine;

namespace Assets.Scripts.Sprites
{
    public enum BillboardStyle
    {
        None,
        Normal,
        AxisAligned,
        Character
    }
    
    public class BillboardObject : MonoBehaviour
    {
        public BillboardStyle Style = BillboardStyle.Normal;
        public Vector3 Axis = Vector3.up;
        public Quaternion SubRotation = Quaternion.identity;

        public void LateUpdate()
        {
            if (Style == BillboardStyle.None)
                return;
            
            if (Style == BillboardStyle.Normal)
            {
                transform.localRotation = Camera.main.transform.rotation;
                return;
            }

            if (Style == BillboardStyle.AxisAligned)
            {
                var look = (transform.position - Camera.main.transform.position).normalized;
                var right = Vector3.Cross(Axis, look);

                look = Vector3.Cross(right, Axis);

                var up = Vector3.Cross(look, right);

                transform.rotation = Quaternion.LookRotation(look, up) * SubRotation;
            }

            if (Style == BillboardStyle.Character)
            {
                transform.localRotation = Camera.main.transform.rotation;
             
                //var look = (transform.position - Camera.main.transform.position).normalized;
                //var right = Vector3.Cross(CharacterAxis, look);

                //look = Vector3.Cross(right, CharacterAxis);

                //var up = Vector3.Cross(look, right);

                //transform.rotation = Quaternion.LookRotation(look, up);

                //var height = Mathf.Clamp(CameraFollower.Instance.Height, 0, 75);

                //transform.rotation = Quaternion.Euler(new Vector3(0f, Camera.main.transform.rotation.eulerAngles.y, 0f));

                //transform.localScale = new Vector3(1.5f, 1f / Mathf.Cos(Mathf.Deg2Rad * height) * 1.5f, 1.5f);
            }
        }
    }
}



