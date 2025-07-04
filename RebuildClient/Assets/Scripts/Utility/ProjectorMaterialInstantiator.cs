using UnityEngine;

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
            var projector = GetComponent<Projector>();
            cloneMaterial = new Material(Material);
            projector.material = cloneMaterial;
        }

        public void Update()
        {
            cloneMaterial.color = Color;
        }
    }
}
