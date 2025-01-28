using System.Collections.Generic;
using Assets.Scripts.MapEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Assets.Scripts.Sprites
{
    public class RoSpriteTrail : MonoBehaviour
    {
        public float Duration;
        public float RemainingTime;
        public Color Color;
        public BillboardObject Billboard;
        public SortingGroup SortingGroup;
        private RoSpriteTrailManager manager;

        // public List<MeshRenderer> Renderers;

        public List<RoSpriteTrailRenderer> Renderers = new();

        public void AddTrailSprite(RoSpriteTrailRenderer renderer, int order = 0)
        {
            renderer.transform.SetParent(transform);
            renderer.transform.localPosition += new Vector3(0, 0, 0.15f);
            renderer.transform.localScale = Vector3.one;
            renderer.Parent = this;
            Renderers.Add(renderer);
            renderer.SetPropertyBlock();
        }

        public void EndTrail()
        {
            if (manager == null)
                manager = RoSpriteTrailManager.Instance;
            
            for(var i = 0; i < Renderers.Count; i++)
                manager.ReturnRenderer(Renderers[i]);
            Renderers.Clear();
            manager.ReturnTrail(this);
        }
        
        public void Update()
        {
            RemainingTime -= Time.deltaTime;

            if (RemainingTime < 0)
            {
                EndTrail();
                return;
            }

            var dist = (RemainingTime / Duration);
            
            Color = new Color(Color.r, Color.g, Color.b, Mathf.Lerp(0, 0.4f, RemainingTime / Duration));
        }
    }
}
