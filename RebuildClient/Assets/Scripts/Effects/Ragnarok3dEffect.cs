using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Utility;
using UnityEngine;
using UnityEngine.Serialization;
using Object = System.Object;

namespace Assets.Scripts.Effects
{
    public class Ragnarok3dEffect : MonoBehaviour
    {
        public float Duration;
        public float CurrentPos;
        public int Step;

        public Vector3 PositionOffset = Vector3.zero;
        public GameObject FollowTarget;
        public Object EffectData;

        public bool DestroyOnTargetLost = false;

        public float activeDelay = 0f;
        public float pauseTime = 0f;

        private IEffectHandler effectHandler;
        private List<RagnarokPrimitive> primitives = new();
        private List<GameObject> attachedObjects = new();

        private bool isInitialized = false;
        
        public static Ragnarok3dEffect Create()
        {
            var go = new GameObject("Effect");
            var effect = go.AddComponent<Ragnarok3dEffect>();
            return effect;
        }

        public void SetEffectType(EffectType type)
        {
            effectHandler = RagnarokEffectData.GetEffectHandler(type);
        }

        public void AttachChildObject(GameObject obj)
        {
            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = Vector3.zero;
            attachedObjects.Add(obj);
        }

        public void Reset()
        {
            // Debug.Log("EffectReset");
            isInitialized = false;
            FollowTarget = null;
            activeDelay = 0;
            pauseTime = 0;
            effectHandler = null;
            EffectData = null;
            Duration = 0;
            CurrentPos = 0;
            Step = 0;
            PositionOffset = Vector3.zero;
            for(var i = 0; i < primitives.Count; i++)
            {
                var p = primitives[i];
                RagnarokEffectPool.ReturnPrimitive(p);
                primitives[i] = null;
            }

            for (var i = 0; i < attachedObjects.Count; i++)
            {
                var obj = attachedObjects[i];
                GameObject.Destroy(obj);
            }

            primitives.Clear();
        }

        public RagnarokPrimitive LaunchPrimitive(PrimitiveType type, Material mat, float duration)
        {
            var primitive = RagnarokEffectPool.GetPrimitive(this);

            var pHandler = RagnarokEffectData.GetPrimitiveHandler(type);
            if (pHandler != null)
            {
                primitive.UpdateHandler = pHandler.GetDefaultUpdateHandler();
                primitive.RenderHandler = pHandler.GetDefaultRenderHandler();
            }

            primitive.Prepare(this, type, mat, duration);

            primitives.Add(primitive);

            return primitive;
        }
        
        private void RenderDirtyPrimitives()
        {
            for (var i = 0; i < primitives.Count; i++)
            {
                var p = primitives[i];
                if(p.IsDirty)
                    p.RenderPrimitive();
            }
        }
        
        public void Update()
        {
            activeDelay -= Time.deltaTime;
            if (activeDelay > 0f)
                return;

            if (pauseTime > 0f)
            {
                pauseTime -= Time.deltaTime;
                if (pauseTime > 0f)
                    return;
            }

            CurrentPos += Time.deltaTime;
            if(Mathf.RoundToInt(CurrentPos / (1 / 60f)) > Step)
                Step++; //only advance once per frame.
            
            if (FollowTarget == null && DestroyOnTargetLost)
            {
                RagnarokEffectPool.Return3dEffect(this);
                return;
            }

            if (FollowTarget != null)
                transform.localPosition = FollowTarget.transform.position + PositionOffset;

            if (effectHandler == null)
                return;
            
            var active = effectHandler.Update(this, CurrentPos, Step);
            var anyActive = active;

            for (var i = 0; i < primitives.Count; i++)
            {
                var p = primitives[i];

                if (!p.IsActive)
                    continue;
                
                var pActive = p.UpdatePrimitive();
                if (pActive)
                    anyActive = true;
                
                if(pActive && p.IsDirty)
                    p.RenderPrimitive();
            }

            if (!anyActive)
                RagnarokEffectPool.Return3dEffect(this);
        }
    }
}