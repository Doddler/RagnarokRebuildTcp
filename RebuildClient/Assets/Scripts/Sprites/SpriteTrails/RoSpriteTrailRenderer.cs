using Assets.Scripts.MapEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.Sprites
{
    public class RoSpriteTrailRenderer : MonoBehaviour
    {
        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
        public RoSpriteTrail Parent;
        public SortingGroup SortingGroup;
        public float SpriteOffset;
        public float VerticalOffset;
        public RoSpriteData SpriteData;
        private MaterialPropertyBlock propertyBlock;
        //private bool skipFrame = true;
        
        private static readonly int VPos = Shader.PropertyToID("_VPos");
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private static readonly int EnvColor = Shader.PropertyToID("_EnvColor");
        private static readonly int Width = Shader.PropertyToID("_Width");
        private static readonly int Offset = Shader.PropertyToID("_Offset");

        public void Init()
        {
            if (MeshRenderer == null)
            {
                MeshRenderer = gameObject.AddComponent<MeshRenderer>();
                MeshRenderer.receiveShadows = false;
                MeshRenderer.lightProbeUsage = LightProbeUsage.Off;
                MeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            if (MeshFilter == null)
                MeshFilter = gameObject.AddComponent<MeshFilter>();
            if (SortingGroup == null)
                SortingGroup = gameObject.AddComponent<SortingGroup>();
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
            
            gameObject.SetActive(true);
        }
        
        public void SetPropertyBlock()
        {
            var envColor = RoMapRenderSettings.GetBakedLightContribution(new Vector2(transform.position.x, transform.position.z));
            
            propertyBlock.SetTexture(MainTex, SpriteData.Atlas);
            propertyBlock.SetColor(ColorProp, Parent.Color);
            propertyBlock.SetColor(EnvColor, envColor);
            propertyBlock.SetFloat(Width, SpriteData.AverageWidth / 25f);
            //
            if (Mathf.Approximately(0, SpriteOffset))
                propertyBlock.SetFloat(Offset, Mathf.Max(SpriteData.Size / 125f, 1f));
            else
                propertyBlock.SetFloat(Offset, SpriteOffset);
            //
            propertyBlock.SetFloat(VPos, VerticalOffset);
            MeshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void Update()
        {
            SetPropertyBlock();
        }
    }
}