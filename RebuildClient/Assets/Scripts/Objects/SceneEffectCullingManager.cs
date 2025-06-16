using System;
using System.Collections.Generic;
using Assets.Scripts.Effects;
using UnityEngine;

namespace Assets.Scripts.Objects
{
    public class SceneEffectCullingManager : MonoBehaviour
    {
        public List<EffectSpawner> Spawners = new();
        
        private CullingGroup cullingGroup;
        private BoundingSphere boundingSphere;
        private BoundingSphere[] boundingSpheres;

        private bool[] activeEffects;
        private int count;

        public void Awake()
        {
            cullingGroup = new CullingGroup();
            cullingGroup.targetCamera = Camera.main;

            count = Spawners.Count;
            boundingSpheres = new BoundingSphere[count];
            activeEffects = new bool[count];
            
            for (var i = 0; i < Spawners.Count; i++)
            {
                var spawner = Spawners[i];
                boundingSpheres[i] = new BoundingSphere(spawner.transform.position + spawner.EffectCenterOffset, spawner.Size);
                spawner.gameObject.SetActive(false);
            }
            
            cullingGroup.SetBoundingSpheres(boundingSpheres);
            cullingGroup.SetBoundingSphereCount(count);
        }

        public void Update()
        {
            for (var i = 0; i < count; i++)
            {
                var visible = cullingGroup.IsVisible(i);
                if (activeEffects[i] && !visible)
                {
                    Spawners[i].Deactivate();
                    activeEffects[i] = visible;
                }
                
                if(!activeEffects[i] && visible)
                {
                    Spawners[i].Activate();
                    activeEffects[i] = visible;
                }
            }
        }

        public void OnDestroy()
        {
            if (cullingGroup != null)
                cullingGroup.Dispose();
        }
    }
}