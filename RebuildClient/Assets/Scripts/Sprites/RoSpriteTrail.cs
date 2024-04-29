using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.Sprites
{
    class RoSpriteTrail : MonoBehaviour
    {
        public float Duration;
        public float LifeTime;
        public float StartTime;
        public Color Color;
        public SortingGroup SortingGroup;

        public List<MeshRenderer> Renderers;

        public void Init()
        {

            foreach (var r in Renderers)
            {
                foreach (var m in r.sharedMaterials)
                    m.SetColor("_Color", new Color(Color.r, Color.g, Color.b, 0f));
            }
        }
        
        public void Update()
        {
            LifeTime -= Time.deltaTime;

            if (LifeTime > StartTime)
                return;

            if (LifeTime < 0)
            {
                foreach (var r in Renderers)
                {
                    foreach(var m in r.sharedMaterials)
                        Destroy(m);
                }

                Destroy(gameObject);
                return;
            }

            foreach (var r in Renderers)
            {
                foreach(var m in r.sharedMaterials)
                    m.SetColor("_Color", new Color(Color.r, Color.g, Color.b, Mathf.Lerp(0, 1, LifeTime / Duration)));
            }
        }
    }
}
