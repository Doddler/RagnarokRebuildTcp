using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers
{
    public enum ForestLightType
    {
        Type0, //0
        Type1, //1
        Type2, //2
        Type10, //10
    }

    [RoEffect("ForestLightEffect")]
    public class ForestLightEffect : IEffectHandler
    {
        private static Material forestLightMaterial;

        public static Ragnarok3dEffect Create(ForestLightType type, Vector3 position)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.CastEffect);
            effect.Duration = float.MaxValue;
            effect.transform.position = position; // + new Vector3(0f, 10f, 0f);

            if (forestLightMaterial == null)
            {
                forestLightMaterial = new Material(ShaderCache.Instance.AdditiveShader);
                forestLightMaterial.mainTexture = Resources.Load<Texture2D>("cloud11");
                forestLightMaterial.renderQueue = 3001;
            }
            
            // Debug.Log("Creating ForestLight effect type " + type);

            var prim = effect.LaunchPrimitive(PrimitiveType.ForestLight, forestLightMaterial, float.MaxValue);
            prim.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);

            prim.CreateParts(4);

            for (var i = 0; i < 4; i++)
            {
                var part = prim.Parts[i];

                switch (type)
                {
                    case ForestLightType.Type0:
                        part.Alpha = 40f;
                        if (i == 3)
                            part.MaxHeight = 2f;
                        else
                            part.MaxHeight = 2f + i;
                        break;
                    case ForestLightType.Type1:
                        part.Alpha = 40f;
                        if (i == 3)
                            part.MaxHeight = 4f;
                        else
                            part.MaxHeight = 4f + i * 2f;
                        break;
                    case ForestLightType.Type2:
                        part.Alpha = 30f;
                        if (i == 3)
                            part.MaxHeight = 1f;
                        else
                            part.MaxHeight = 1f + i * 0.5f;
                        break;
                    case ForestLightType.Type10:
                        part.Alpha = 25f;
                        if (i == 3)
                            part.MaxHeight = 2f;
                        else
                            part.MaxHeight = 2f + i;
                        break;
                }

                part.Active = true;
                part.RotStart = 25 * i;
                part.Heights[0] = part.MaxHeight;
                part.Position = new Vector3( -70f, 300f, -70f);
                if (type == ForestLightType.Type10)
                    part.Position = new Vector3(-3, 12f, -3f);
                part.Flags[0] = (int)type;
                
                if (i == 3)
                    part.Step = 180;
            }

            return effect;
        }

        public void SceneChangeResourceCleanup()
        {
            if (forestLightMaterial == null)
                return;
            Resources.UnloadAsset(forestLightMaterial.mainTexture);
            GameObject.Destroy(forestLightMaterial);
        }
    }
}