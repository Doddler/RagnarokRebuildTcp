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
        public RoSpriteData SpriteData;
        private MaterialPropertyBlock propertyBlock;
        private bool skipFrame = true;
        

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
            
            propertyBlock.SetTexture("_MainTex", SpriteData.Atlas);
            propertyBlock.SetColor("_Color", Parent.Color);
            propertyBlock.SetColor("_EnvColor", envColor);
            propertyBlock.SetFloat("_Width", SpriteData.AverageWidth / 25f);
            // propertyBlock.SetFloat("_Offset", 0);
            //
            if (Mathf.Approximately(0, SpriteOffset))
                propertyBlock.SetFloat("_Offset", Mathf.Max(SpriteData.Size / 125f, 1f));
            else
                propertyBlock.SetFloat("_Offset", SpriteOffset);
            //
            MeshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void Update()
        {
            SetPropertyBlock();
        }
    }
}