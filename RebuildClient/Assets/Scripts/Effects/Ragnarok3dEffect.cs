using System;
using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects
{
    public class Ragnarok3dEffect : MonoBehaviour
    {
        public float Duration;
        public float CurrentPos;

        public GameObject FollowTarget;

        private bool destroyOnTargetLost;

        private Material material;

        public int Step;

        private MeshBuilder mb;

        private MeshRenderer mr;
        private MeshFilter mf;

        public EffectPart[] Parts;

        private Mesh mesh;

        private float activeDelay = 0f;
        private float pauseTime = 0f;

        public Action Updater;
        public Action Renderer;

        
        public static Ragnarok3dEffect Create()
        {
            var go = new GameObject("Effect");
            var effect = go.AddComponent<Ragnarok3dEffect>();
            return effect;
        }

        public void Reset()
        {
            
        }
        
        public void Awake()
        {
            
        }
    }
}