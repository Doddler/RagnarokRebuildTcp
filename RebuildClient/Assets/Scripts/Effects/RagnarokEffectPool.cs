using System.Collections.Generic;
using Assets.Scripts.Utility;
using UnityEngine;
using Utility;

namespace Assets.Scripts.Effects
{
    public class RagnarokEffectPool : MonoBehaviorSingleton<RagnarokEffectPool>
    {
        private static Stack<Ragnarok3dEffect> effectList = new();
        
        public static Ragnarok3dEffect Get3dEffect()
        {
            if (effectList.Count > 0)
            {
                var effect = effectList.Pop();
                effect.gameObject.transform.SetParent(null);
                return effect;
            }

            return Ragnarok3dEffect.Create();
        }

        public static void Return3dEffect(Ragnarok3dEffect effect)
        {
            effect.Reset();
            effect.gameObject.transform.SetParent(Instance.transform);
            effectList.Push(effect);
        }
        
    }
}