using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Effects.PrimitiveHandlers;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("WaterBallRise")]
    public class WaterBallRiseEffect : IEffectHandler
    {
        public static Ragnarok3dEffect LaunchWaterBallRise(GameObject src)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.WaterBallRise);
            effect.SetDurationByFrames(9999);
            effect.FollowTarget = src.gameObject;
            effect.DestroyOnTargetLost = false;
            effect.UpdateOnlyOnFrameChange = true;
            effect.transform.localPosition = src.transform.position + new Vector3(0f, 0f, 0f);
            effect.transform.localScale = new Vector3(1f, 1f, 1f);
            effect.Material = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.WaterBallEffect);
            effect.PositionOffset = Vector3.zero;
            effect.SetBillboardMode(BillboardStyle.Normal);
            
            EffectSharedMaterialManager.PrepareEffectSprite("Assets/Sprites/Effects/waterball.spr");

            return effect;
        }
        
        
        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step == 0)
            {
                if (!EffectSharedMaterialManager.TryGetEffectSprite("Assets/Sprites/Effects/waterball.spr", out var sprite))
                {
                    effect.ResetStep();
                    return true; //wait until the sprite loads
                }
                
                effect.Material.mainTexture = sprite.Sprites[0].texture;
                
                LaunchWaterBall(effect, sprite, effect.Material, 0, 0, 0);
                
                //create shadow
                var mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.ShadowMaterial);
                var prim = effect.LaunchPrimitive(PrimitiveType.Texture3D, mat, 9999);
                prim.SetBillboardMode(BillboardStyle.Normal);
                var data = prim.GetPrimitiveData<Texture3DData>();

                data.Size = new Vector2(0.4f, 0.2f);
                data.ScalingSpeed = Vector2.zero; //-data.Size / 0.333f; //why is this scaling speed in speed per second and the other per frame? Who knows!
                data.Alpha = 0;
                data.AlphaSpeed = 9f * 60;
                data.AlphaMax = 147;
                data.MinSize = Vector2.zero;
                data.MaxSize = data.Size;
                data.IsStandingQuad = true;
                data.FadeOutTime = 0.1f;
                data.Color = Color.white;
                prim.Velocity = Vector3.zero;
                prim.transform.localPosition = Vector3.zero;
            }

            if (effect.Primitives.Count == 0)
                return false;

            var dat = effect.Primitives[0].GetPrimitiveData<Particle3DSplineData>();
            
            if (effect.Flags[0] == 0)
            {
                if (dat.Color.a < 250)
                    dat.Color = new Color32(255, 255, 255, (byte)(dat.Color.a + 12));
                
                if (effect.FollowTarget == null)
                {
                    effect.Primitives[0].FrameDuration = step;
                    effect.Flags[0] = 1;
                    effect.SetDurationByFrames(step + 20);
                    return true;
                }
            }
            else
            {
                if (dat.Color.a > 0)
                {
                    dat.Color = new Color32(255, 255, 255, (byte)(dat.Color.a - 12));
                    
                    var shadow = effect.Primitives[1].GetPrimitiveData<Texture3DData>();
                    shadow.AlphaSpeed = 0;
                    shadow.Alpha -= 7f;
                }
            }

            return step < effect.DurationFrames;
        }
        
        private static void LaunchWaterBall(Ragnarok3dEffect effect, RoSpriteData sprite, Material mat, float forwardAngle, float upAngle, float delay)
        {
            var frames = 9999f;
            var prim = effect.LaunchPrimitive(PrimitiveType.Particle3DSpline, mat, 9999f);
            prim.CreateSegments(6);
            prim.DelayTime = delay;
            prim.FrameDuration = Mathf.RoundToInt(frames); //shorter than proper duration to allow for the segments to fade out
            prim.RenderHandler = Particle3DSplinePrimitive.RenderParticle3DSplineSpritePrimitive;

            var data = prim.GetPrimitiveData<Particle3DSplineData>();
            data.Position = Vector3.zero;
            data.Velocity = new Vector2(0, 0.075f);
            data.Size = 0.467f;
            data.Acceleration = new Vector2(0, -(data.Velocity.y / 40));
            data.CapVelocity = true;
            data.MinVelocity = Vector2.zero;
            data.SpriteData = sprite;
            data.AnimTime = 12; //5 fps
            data.AnimOffset = Random.Range(0, 36);
            data.DoShrink = false;
            data.Rotation = Quaternion.Euler(0, forwardAngle, upAngle);
        }
    }
}