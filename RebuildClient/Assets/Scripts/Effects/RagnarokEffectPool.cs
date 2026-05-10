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
        private static Stack<DamageIndicatorTmpVisual> tmpVisualList = new();

        private static GameObject effectContainer;
        private static GameObject damageContainer;
        private static GameObject tmpVisualSource;

        #if UNITY_EDITOR
        public static int DebugGet3DEffectPoolCount() => effectList.Count;
        public static int DebugGetPrimitivePoolCount() => primitiveList.Count;
        public static int DebugGetDamageIndicatorCount() => tmpVisualList.Count;
        #endif
	    
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

        public static DamageIndicator GetDamageIndicator()
        {
            var i = new DamageIndicator();
            DamageIndicatorBatcher.Instance.indicators.Add(i);
            return i;
        }

        public static DamageIndicatorTmpVisual GetTmpDamageVisual()
        {
            if (damageContainer == null)
            {
                damageContainer = new GameObject("DamageContainer");
            }

            if (tmpVisualSource == null)
            {
                tmpVisualSource = Resources.Load<GameObject>("DamageCritical");
            }

            DamageIndicatorTmpVisual visual;
            if (tmpVisualList.TryPop(out visual) && visual)
            {
                visual.gameObject.transform.SetParent(damageContainer.transform, false);
            }
            else
            {
                var go = Instantiate(tmpVisualSource, damageContainer.transform, false);
                visual = go.GetComponent<DamageIndicatorTmpVisual>();
            }

            visual.gameObject.SetActive(true);
            return visual;
        }

        public static void ReturnTmpDamageVisual(DamageIndicatorTmpVisual visual)
        {
            if (!visual) return;

            if (!Instance)
            {
                Destroy(visual.gameObject);
                return;
            }

            if (tmpVisualList.Count > 50)
            {
                Destroy(visual.gameObject);
                return;
            }

            visual.gameObject.transform.SetParent(Instance.transform, false);
            visual.gameObject.SetActive(false);
            tmpVisualList.Push(visual);
        }
    }
}