using UnityEngine;

namespace Assets.Scripts.Objects
{
    public class ModelTrigger : MonoBehaviour
    {
        public RoKeyframeRotator[] Rotators;

        public void Activate()
        {
            if (Rotators == null)
                return;
            
            foreach (var r in Rotators)
            {
                r.enabled = true;
            }
        }
    }
}