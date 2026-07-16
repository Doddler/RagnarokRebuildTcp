using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Assets.Scripts.Utility
{
    [ExecuteInEditMode]
    public class ProjectorMaterialInstantiator : MonoBehaviour
    {
        public Material Material;
        public Color Color;
        private Material cloneMaterial;

        public void Awake()
        {
            var decal = GetComponent<DecalProjector>();
            cloneMaterial = new Material(Material);
            decal.material = cloneMaterial;
        }

        public void Update()
        {
            cloneMaterial.color = Color;
        }
    }
}
