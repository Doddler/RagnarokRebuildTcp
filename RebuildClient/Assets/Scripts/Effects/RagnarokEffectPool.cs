using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace Assets.Scripts.Effects
{
    public class RagnarokEffectPool : MonoBehaviorSingleton<RagnarokEffectPool>
    {
        private static Stack<Ragnarok3dEffect> effectList = new();
        private static Stack<RagnarokPrimitive> primitiveList = new();
        private static Stack<DamageIndicator> indicatorList = new();

        private static GameObject effectContainer;
        private static GameObject damageContainer;
        
        public static Ragnarok3dEffect Get3dEffect(EffectType type)
        {
            if (effectContainer == null)
                effectContainer = new GameObject("EffectContainer");
            
            if (effectList.TryPop(out var effect))
                effect.gameObject.transform.SetParent(effectContainer.transform, false);
            else
            {
                effect = Ragnarok3dEffect.Create();
                effect.gameObject.transform.SetParent(effectContainer.transform, false);
            }

            effect.SetEffectType(type);
            effect.gameObject.SetActive(true);
            effect.transform.localScale = Vector3.one;
            effect.transform.rotation = Quaternion.identity;
            
            return effect;
        }

        public static void Return3dEffect(Ragnarok3dEffect effect)
        {
            if (Instance == null || effect == null || effect.gameObject == null)
                return;
            
            if (!effect.IsInitialized)
                Debug.LogWarning($"Returning effect object to the pool but it was either already returned or never initialized.");
            
            if (effectList.Contains(effect))
                throw new Exception($"Attempting to return a 3d effect that is already in the pool!");
            
            foreach(var sprite in effect.SpriteEffects)
                if(sprite != null)
                    Destroy(sprite.gameObject);
            effect.SpriteEffects.Clear();
            
            effect.Reset();
            effect.gameObject.transform.SetParent(Instance.transform);
            effect.gameObject.SetActive(false);
            effectList.Push(effect);
        }

        public static RagnarokPrimitive GetPrimitive(Ragnarok3dEffect parent)
        {
            if (!primitiveList.TryPop(out var p))
                p = RagnarokPrimitive.Create();
        
            p.gameObject.transform.SetParent(parent.transform);
            p.gameObject.SetActive(true);
            p.transform.localPosition = Vector3.zero;
            p.transform.localScale = Vector3.one;
            p.transform.rotation = Quaternion.identity;
            
            return p;
        }

        public static void ReturnPrimitive(RagnarokPrimitive primitive)
        {
            if (primitive == null)
            {
                Debug.LogWarning($"Attempting to return an empty or uninitialized primitive!");
                return;
            }
            
            #if DEBUG
            if (primitiveList.Contains(primitive))
                throw new Exception($"Attempting to return primitive that is already in the pool!");
            #endif
            
            primitive.Reset();
            primitive.gameObject.transform.SetParent(Instance.transform);
            primitive.gameObject.SetActive(false);
            primitiveList.Push(primitive);
        }

        private static GameObject indicatorSource;
        
        public static DamageIndicator GetDamageIndicator()
        {
            if (damageContainer == null)
            {
                damageContainer = new GameObject("DamageContainer");
                indicatorSource = Resources.Load<GameObject>("DamageCritical");
            }

            if (!indicatorList.TryPop(out var i))
            {
                var go = GameObject.Instantiate(indicatorSource);
                i = go.GetComponent<DamageIndicator>();
            }
            
            i.gameObject.transform.SetParent(damageContainer.transform);
            
            return i;
        }

        public static void ReturnDamageIndicator(DamageIndicator indicator)
        {
            if (indicatorList.Count > 50)
            {
                Destroy(indicator.gameObject);
                return;
            }

            indicator.gameObject.transform.SetParent(Instance.transform);
            indicator.gameObject.SetActive(false);
            indicatorList.Push(indicator);
        }
    }
}