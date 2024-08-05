using UnityEngine;

namespace Assets.Scripts.MapEditor
{
    [ExecuteInEditMode]
    public class RoMapRenderSettings : MonoBehaviour
    {
        public Color AmbientColor = Color.grey;
        public Color Diffuse;

        public Color TempDiffuse;

        public Texture2D BakedLightContributionMap;

        public float Opacity;
        [Range(0, 1)] public float AmbientOcclusionStrength;
        public bool UseMapAmbient = true;

        private static Color32[] globalMap;
        private static int mWidth;
        private static int mHeight;

        public static void ClearBakedLightmap()
        {
            globalMap = null;
        }

        private static float BilinearInterpolate(float tl, float tr, float bl, float br, float x, float y)
        {
            var v = tl * (1 - x) + tr * x;
            var h = bl * (1 - y) + br * y;

            return (v + h) / 2f;
        }

        public static Color GetBakedLightContribution(Vector2 position)
        {
            if (globalMap == null)
                return new Color32(0, 0, 0, 1);

            if (position.x < 0 || position.y < 0 || position.x > mWidth || position.y > mHeight)
                return new Color(0, 0, 0, 1f);
            
            var pos = new Vector2(Mathf.Clamp(position.x, 0, mWidth - 1), Mathf.Clamp(position.y, 0, mHeight - 1));
            var x1 = (int)Mathf.Floor(pos.x);
            var x2 = (int)Mathf.Floor(pos.x) + 1;
            var y1 = (int)Mathf.Floor(pos.y);
            var y2 = (int)Mathf.Floor(pos.y) + 1;

            var tl = globalMap[x1 + y1 * mWidth];
            var tr = globalMap[x2 + y1 * mWidth];
            var bl = globalMap[x1 + y2 * mWidth];
            var br = globalMap[x2 + y2 * mWidth];

            var r = BilinearInterpolate(tl.r / 255f, tr.r / 255f, bl.r / 255f, br.r / 255f, pos.x - x1, pos.y - y1);
            var g = BilinearInterpolate(tl.g / 255f, tr.g / 255f, bl.g / 255f, br.g / 255f, pos.x - x1, pos.y - y1);
            var b = BilinearInterpolate(tl.b / 255f, tr.b / 255f, bl.b / 255f, br.b / 255f, pos.x - x1, pos.y - y1);

            var m = 1 - Mathf.Max(r, g, b);

            return new Color(m+r, m+g, m+b, (r+g+b)/3f);
        }

        public void Awake()
        {
            if (BakedLightContributionMap == null || Camera.main == null)
            {
                globalMap = null;
                return;
            }

            globalMap = BakedLightContributionMap.GetPixels32();
            mWidth = BakedLightContributionMap.width;
            mHeight = BakedLightContributionMap.height;
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