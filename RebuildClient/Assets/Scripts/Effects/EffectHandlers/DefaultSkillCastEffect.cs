using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers
{
    [RoEffect("DefaultSkillCastEffect")]
    public class DefaultSkillCastEffect : IEffectHandler
    {
        public static Material CircleMaterial;
        public static Material FlashMaterial;
        
        public static Ragnarok3dEffect Create(ServerControllable source)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.DefaultSkillCastEffect);
            
            effect.SourceEntity = source;
            effect.SetDurationByFrames(40);
            effect.FollowTarget = source.gameObject;
            effect.UpdateOnlyOnFrameChange = true;
            
            if (CircleMaterial == null)
            {
                CircleMaterial = new Material(ShaderCache.Instance.AlphaBlendNoZWriteShader);
                CircleMaterial.mainTexture = Resources.Load<Texture2D>("alpha_down");
                CircleMaterial.renderQueue = 3003; //this material will render above everything
            }
            
            
            if (FlashMaterial == null)
            {
                FlashMaterial = new Material(ShaderCache.Instance.AlphaBlendNoZWriteShader);
                FlashMaterial.mainTexture = Resources.Load<Texture2D>("alpha_center");
                FlashMaterial.renderQueue = 3003; //this material will render above everything
            }

            
            var circlePrim = effect.LaunchPrimitive(PrimitiveType.Circle2D, CircleMaterial, 0.667f);
            var cData = circlePrim.GetPrimitiveData<CircleData>();

            var scale = 0.03f;

            circlePrim.transform.localScale = new Vector3(scale, scale, scale);
            circlePrim.transform.localPosition += new Vector3(0f, 2f, -0f);
            circlePrim.SetBillboardMode(BillboardStyle.Normal);
            cData.Alpha = 0f;
            cData.MaxAlpha = 170;
            cData.AlphaSpeed = cData.MaxAlpha / 0.166f;
            cData.FadeOutLength = 0.166f;
            cData.Radius = 100f;
            cData.FillCircle = true;
            
            for (var i = 0; i < 20; i++)
            {
                var flashPrim = effect.LaunchPrimitive(PrimitiveType.Flash2D, FlashMaterial, 0.667f);
                var fData = flashPrim.GetPrimitiveData<FlashData>();
                flashPrim.transform.localScale = new Vector3(scale, scale, scale);
                flashPrim.transform.localPosition += new Vector3(0f, 2f, -0.01f);
                flashPrim.SetBillboardMode(BillboardStyle.Normal);
                fData.RotationAngle = Random.Range(0, 360f);
                fData.RotationSpeed = (Random.Range(0, 60) + 10f) / 0.166f; //angle rotation per frame
                fData.RotationAccel = -(fData.RotationSpeed / 40) / 1.5f; //angle acceleration per frame
                fData.Length = (Random.Range(0, 40) + 20);
                fData.LengthSpeed = (Random.Range(0, 30) + 20f) / 0.166f;
                fData.ArcLength = (Random.Range(0, 25) + 5) / 10f;
                fData.Alpha = 0;
                fData.MaxAlpha = 200;
                fData.AlphaSpeed = fData.MaxAlpha / 0.1f;
                fData.FadeOutLength = 0.667f - (0.667f / 3f);
            }

            //Debug.Break();
            
            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            
            return step < effect.DurationFrames;
        }

        public void OnCleanup(Ragnarok3dEffect effect)
        {
            
        }
    }
}