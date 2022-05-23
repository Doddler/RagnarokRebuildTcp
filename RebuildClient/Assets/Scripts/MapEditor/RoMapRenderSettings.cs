using UnityEngine;

namespace Assets.Scripts.MapEditor
{
    [ExecuteInEditMode]
    public class RoMapRenderSettings : MonoBehaviour
    {
        public Color AmbientColor = Color.grey;
        public Color Diffuse;

        public Color TempDiffuse;
        
        public float Opacity;
        [Range(0,1)]
        public float AmbientOcclusionStrength;
        public bool UseMapAmbient = true;

        public void Awake()
        {

        }

#if UNITY_EDITOR
        void Update()
        {
            DoLightUpdate();
        }
#endif

        public void DoLightUpdate()
        {
            var one = Color.white;

            //TempDiffuse = one - (one - Diffuse) * (one - AmbientColor);

            TempDiffuse.r = 1f - ((1f - Diffuse.r) * (1f - AmbientColor.r));
            TempDiffuse.g = 1f - ((1f - Diffuse.g) * (1f - AmbientColor.g));
            TempDiffuse.b = 1f - ((1f - Diffuse.b) * (1f - AmbientColor.b));
            TempDiffuse.a = 1f;

            //TempDiffuse = (Diffuse + AmbientColor) / 2f;

            //if (!UseMapAmbient)
            //    TempDiffuse = Diffuse;

            Shader.SetGlobalColor("_RoAmbientColor", AmbientColor);
            Shader.SetGlobalColor("_RoDiffuseColor", Diffuse);
            Shader.SetGlobalFloat("_RoLightmapAOStrength", AmbientOcclusionStrength);
            Shader.SetGlobalFloat("_Opacity", Opacity);

            //Debug.Log("hi");
        }

        void OnEnable()
        {
            DoLightUpdate();
        }
    }
}
