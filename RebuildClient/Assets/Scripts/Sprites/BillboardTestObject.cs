using Assets.Scripts.Sprites;
using UnityEngine;

namespace Sprites
{
    [RequireComponent(typeof(BillboardObject))]
    public class BillboardTestObject : MonoBehaviour
    {
        public Vector3 Forward = Vector3.down;
        public Vector3 Up = Vector3.right;
        public Vector3 Multiplier = Vector3.forward;
        public Vector3 BaseRotation = new Vector3(0, 0, -90);
        
        private BillboardObject billboard;

        public void Awake()
        {
            billboard = GetComponent<BillboardObject>();
        }

        public void Update()
        {
            billboard.Style = BillboardStyle.AxisAligned;
            
            
            billboard.Axis = Quaternion.LookRotation(Forward) * Multiplier;
            billboard.SubRotation = Quaternion.Euler(BaseRotation);
        }
    }
}