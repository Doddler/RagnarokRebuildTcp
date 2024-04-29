using UnityEngine;

namespace Assets.Scripts.Effects
{
    class MapWarpObject : MonoBehaviour
    {
        public void Awake()
        {
            EffectHandlers.MapWarpEffect.StartWarp(gameObject);
            //MapWarpEffect.StartWarp(gameObject);
        }
    }
}
