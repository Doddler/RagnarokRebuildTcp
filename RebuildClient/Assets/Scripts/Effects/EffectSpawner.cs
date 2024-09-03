using System;
using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.Environment;
using Assets.Scripts.Network;
using UnityEngine;

namespace Assets.Scripts.Effects
{
    public class EffectSpawner : MonoBehaviour
    {
        public string EffectTypeName;

        //this whole shit is set up this way because the EffectType enum list will change when adding new effects, so we store a string instead
        public EffectType EffectType
        {
            set => EffectTypeName = value.ToString();
        }

        public int Variant = 0;

        public void Awake()
        {
            if (NetworkManager.Instance == null)
                return;

            var type = Enum.Parse<EffectType>(EffectTypeName);
            
            switch (type)
            {
                case EffectType.BlueWaterfallEffect:
                    BlueWaterfallEffect.Create(Variant, transform.position);
                    break;
                case EffectType.ForestLightEffect:
                    ForestLightEffect.Create((ForestLightType)Variant, transform.position);
                    break;
            }
        }
    }
}