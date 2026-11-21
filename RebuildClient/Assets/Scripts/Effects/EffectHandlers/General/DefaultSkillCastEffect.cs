using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers
{
    [RoEffect("DefaultSkillCastEffect")]
    public class DefaultSkillCastEffect : IEffectHandler
    {
        // public static Material CircleMaterial;
        public static Material flashMaterial;

        // public static Material GetCircleMaterial()
        // {
        //     if (CircleMaterial == null)
        //     {
        //         CircleMaterial = new Material(ShaderCache.Instance.AlphaBlendNoZTestShader);
        //         CircleMaterial.mainTexture = Resources.Load<Texture2D>("alpha_down");
        //         CircleMaterial.renderQueue = 3003; //this material will render above everything
        //     }
        //
        //     return CircleMaterial;
        // }
        //
        public static Ragnarok3dEffect Create(ServerControllable source, bool showCirclePrimitive = true)
        {
            // GetCircleMaterial();

            flashMaterial = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.SkillFlashEffect);
            
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.DefaultSkillCastEffect);
            var scale = 0.025f;
            
            effect.SourceEntity = source;
            effect.SetDurationByFrames(40);
            effect.FollowTarget = source.gameObject;
            effect.UpdateOnlyOnFrameChange = true;
            if (showCirclePrimitive)
            {
                var circleMat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.CircleMaterial);
                
                var circlePrim = effect.LaunchPrimitive(PrimitiveType.Circle2D, circleMat, 0.667f);
                var cData = circlePrim.GetPrimitiveData<CircleData>();

                circlePrim.transform.localScale = new Vector3(scale, scale, scale);
                circlePrim.transform.localPosition += new Vector3(0f, 2f, -0f);
                circlePrim.SetBillboardMode(BillboardStyle.Normal);
                cData.Alpha = 0f;
                cData.MaxAlpha = 130;
                cData.AlphaSpeed = cData.MaxAlpha / 0.166f;
                cData.FadeOutLength = 0.166f;
                cData.Radius = 100f;
                cData.FillCircle = true;
                cData.Color = Color.white;
            }

            var flashPrim = effect.LaunchPrimitive(PrimitiveType.Flash2D, flashMaterial, 0.667f);
            var fData = flashPrim.GetPrimitiveData<FlashData>();
            flashPrim.transform.localScale = new Vector3(scale, scale, scale);
            flashPrim.transform.localPosition += new Vector3(0f, 2f, -0.01f);
            flashPrim.SetBillboardMode(BillboardStyle.Normal);

            for (var i = 0; i < 20; i++)
            {
                ref var ray = ref fData.AddRay();
                ray.RotationAngle = Random.Range(0, 360f);
                ray.RotationSpeed = Random.Range(10, 70) / 0.166f; //10-70 degrees per second
                ray.RotationAccel = -(ray.RotationSpeed / 40) / 1.5f; //acceleration per frame
                ray.Length = Random.Range(20, 60);
                ray.LengthSpeed = Random.Range(20, 50) / 0.166f;
                ray.ArcLength = Random.Range(5, 30) / 10f;
                ray.Alpha = 0;
                ray.MaxAlpha = 170;
                ray.AlphaSpeed = ray.MaxAlpha / 0.1f;
                ray.FadeOutLength = 0.667f - (0.667f / 3f);
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