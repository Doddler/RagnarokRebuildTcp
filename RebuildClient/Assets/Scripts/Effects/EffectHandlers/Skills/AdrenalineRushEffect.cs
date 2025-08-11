using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("AdrenalineRush")]
    public class AdrenalineRushEffect : IEffectHandler
    {
        public static Ragnarok3dEffect Create(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.AdrenalineRush);
            effect.FollowTarget = target.gameObject;
            effect.SourceEntity = target;
            effect.SetDurationByTime(1.67f);
            effect.UpdateOnlyOnFrameChange = true;
            effect.DestroyOnTargetLost = true;

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step == 0)
            {
                AudioManager.Instance.OneShotSoundEffect(-1, "black_adrenalinerush_a.ogg", effect.transform.position, 0.7f);

                var scale = 0.025f;
                var flashMat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.SkillFlashEffect);
                for (var i = 0; i < 40; i++)
                {
                    var flashPrim = effect.LaunchPrimitive(PrimitiveType.Flash2D, flashMat, 1.33f);
                    var fData = flashPrim.GetPrimitiveData<FlashData>();
                    flashPrim.transform.localScale = new Vector3(scale, scale, scale);
                    flashPrim.transform.localPosition += new Vector3(0f, 1.6f, -0.01f);
                    flashPrim.SetBillboardMode(BillboardStyle.Normal);
                    fData.RotationAngle = Random.Range(0, 360f);
                    fData.RotationSpeed = Random.Range(10, 60) / 0.166f; //10-60 degrees per second
                    fData.RotationAccel = -(fData.RotationSpeed / 80) / 1.5f; //acceleration per frame
                    fData.Length = 0;
                    fData.LengthSpeed = Random.Range(20, 50) / 0.2167f;
                    fData.ArcLength = Random.Range(5, 30) / 10f;
                    fData.Alpha = 0;
                    fData.MaxAlpha = 170;
                    fData.AlphaSpeed = fData.MaxAlpha / 0.1f;
                    fData.FadeOutLength = 0.167f; //.15f;
                    fData.ChangePoint = 40;
                    fData.ChangeRotationSpeed = Random.Range(15, 25) / 0.166f;
                    fData.ChangeRotationAccel = 0;
                    fData.ChangeLengthSpeed = 0;
                }
                
                var particleMat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.ParticleAlphaBlend);
                var prim = effect.LaunchPrimitive(PrimitiveType.ParticleOrbit, particleMat, 1.667f);
                prim.CreateParts(4);

                var dat = prim.GetPrimitiveData<ParticleOrbitData>();
                dat.Sprite = EffectSharedMaterialManager.GetParticleSprite("particle1");

                dat.Radius = 7;
                dat.GravitySpeed = 2f;
                dat.GravityAccel = 0.12f / 2f;
                dat.RotationSpeed = 3f * 60f;
                dat.RotationAccel = 12f * 60f;
                dat.Rotation = 0;
                dat.Size = 1.2f;
                dat.FadeOutTime = 0.167f;

                var particlePos = new Vector3(0f, 0f, 7f / 5f);
                for (var i = 0; i < 4; i++)
                {
                    prim.Parts[i] = new EffectPart()
                    {
                        Active = true,
                        Step = 0,
                        Alpha = 255f,
                        Angle = i * 90,
                        Position = particlePos,
                        Color = Color.white,
                    };
                }
            }


            if (step == 30)
                AudioManager.Instance.OneShotSoundEffect(-1, "black_adrenalinerush_b.ogg", effect.transform.position, 0.7f);

            return effect.IsTimerActive;
        }
    }
}