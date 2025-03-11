using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Effects.Misc
{
    public class LightControlGroup : MonoBehaviour
    {
        public List<Light> Lights;

        public void SetBrightness(float val)
        {
            if (Lights == null)
                return;
            
            for (var i = 0; i < Lights.Count; i++)
            {
                Lights[i].intensity = val;
            }
        }
    }
}