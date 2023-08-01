using Assets.Scripts.Effects;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
    public class Billboard : MonoBehaviour
    {
        public enum BillboardStyle
        {
            Normal,
            AxisAligned,
            Character
        }

        public BillboardStyle Style = BillboardStyle.Normal;
        public Vector3 Axis = Vector3.up;
        

        private bool useSpriteBillboards = true;
        private static Vector3 CharacterAxis = new Vector3(0, 1f, 0);

        public void Update()
        {
            //don't allow this to break containment cause it's bad code.
#if UNITY_EDITOR
            useSpriteBillboards = ShaderCache.Instance.BillboardSprites;
#endif
        }

        public void LateUpdate()
        {
            if (Style == BillboardStyle.Normal)
            {
                //transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                if(useSpriteBillboards)
                    transform.localRotation = Camera.main.transform.rotation;
                else
                    transform.localRotation = Quaternion.identity;
                return;
            }

            if (Style == BillboardStyle.AxisAligned)
            {
                var look = (transform.position - Camera.main.transform.position).normalized;
                var right = Vector3.Cross(Axis, look);

                look = Vector3.Cross(right, Axis);

                var up = Vector3.Cross(look, right);

                transform.rotation = Quaternion.LookRotation(look, up);
            }

            if (Style == BillboardStyle.Character)
            {
                if (useSpriteBillboards)
                    transform.localRotation = Camera.main.transform.rotation;
                else
                    transform.localRotation = Quaternion.identity;

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



