using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Effects
{
    public class EffectSpawner : MonoBehaviour
    {
        public EffectType EffectType;
        public int Variant = 0;

        public void Awake()
        {
            if (NetworkManager.Instance == null)
                return;
            
            switch (EffectType)
            {
                case EffectType.ForestLightEffect:
                    ForestLightEffect.Create((ForestLightType)Variant, transform.position);
                    break;
            }
        }
    }
}