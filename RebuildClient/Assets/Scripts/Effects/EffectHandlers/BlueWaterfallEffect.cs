using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Effects.PrimitiveHandlers;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers
{
    [RoEffect("BlueWaterfallEffect")]
    public class BlueWaterfallEffect : IEffectHandler
    {
        private static Material[] materials = new Material[4];
        private static Texture2D texture;
        private static string textureName = "waterfall1";
        private static float lastUpdateTime;

        public static void Create(int type, Vector3 position)
        {
            if (texture == null)
                texture = Resources.Load<Texture2D>(textureName);
            

            if (materials[0] == null)
            {
                for (var i = 0; i < 4; i++)
                {
                    materials[i] = new Material(ShaderCache.Instance.AdditiveShader);
                    materials[i].renderQueue = 3002;
                    materials[i].SetTextureScale("_MainTex", new Vector2(1, 1f));
                    materials[i].mainTexture = texture;
                }
            }

            var effect = RagnarokEffectPool.Get3dEffect(EffectType.BlueWaterfallEffect);
            effect.transform.position = position;

            for (var i = 0; i < 4; i++)
            {
                var prim = effect.LaunchPrimitive(PrimitiveType.Texture3D, materials[i]);
                var data = prim.GetPrimitiveData<Texture3DData>();
                if (type == 0)
                {
                    prim.transform.localRotation = Quaternion.Euler(-90f, 0, 0f);
                    prim.transform.localPosition = new Vector3(i * 0.1f, 16f, i * 0.1f);
                }
                else
                {
                    prim.transform.localRotation = Quaternion.Euler(-90f, 90, 0f);
                    prim.transform.localPosition = new Vector3(i * 0.1f, 16f, i * 0.1f);
                }

                data.Size = new Vector2((3.6f + 0.1f * i), 16);
                data.Flags = RoPrimitiveHandlerFlags.NoAnimation;
                data.Color = new Color(80 / 255f, 80 / 255f, 255 / 255f, 120 / 255f);
            }
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            //Technically this gets called for every BlueWaterfall effect, but since they all share the same
            //materials we only need to update the texture offset once on each frame.
            if (Time.timeSinceLevelLoad > lastUpdateTime)
            {
                for (var i = 0; i < materials.Length; i++)
                {
                    var speed = 80 - (i * 13);
                    var realStep = Time.timeSinceLevelLoad * 60f;
                    var progress = realStep / speed;
                    materials[i].SetTextureOffset("_MainTex", new Vector2(0, -progress));
                }

                lastUpdateTime = Time.timeSinceLevelLoad;
            }

            return true; //we never go away
        }

        public void SceneChangeResourceCleanup()
        {
            for (var i = 0; i < 4; i++)
                if (materials[i] != null)
                    GameObject.Destroy(materials[i]);
            if (texture != null)
            {
                Resources.UnloadAsset(texture);
                texture = null;
            }
        }
    }
}