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
    [RoEffect("WaterBallAttack")]
    public class WaterBallAttackEffect : IEffectHandler
    {
        public static Ragnarok3dEffect LaunchWaterBallAttack(ServerControllable src, ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.WaterBallAttack);
            effect.SetDurationByFrames(150);
            effect.SourceEntity = src;
            effect.FollowTarget = null;
            effect.AimTarget = target.gameObject;
            effect.DestroyOnTargetLost = false;
            effect.UpdateOnlyOnFrameChange = true;
            effect.transform.localPosition = src.transform.position + new Vector3(0f, 1.5f, 0f);
            effect.transform.localScale = new Vector3(1f, 1f, 1f);
            effect.Material = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.WaterBallEffect);

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

                
                if (effect.SourceEntity == null || effect.AimTarget == null)
                    return false;
                
                AudioManager.Instance.OneShotSoundEffect(effect.SourceEntityId, $"wizard_waterball_chulung.ogg", effect.SourceEntity.transform.position);
                
                var pos1 = new Vector2(effect.SourceEntity.transform.position.x, effect.SourceEntity.transform.position.z);
                var pos2 = new Vector2(effect.AimTarget.transform.position.x, effect.AimTarget.transform.position.z);
                var angle = Vector2.SignedAngle((pos2 - pos1).normalized, Vector2.up);
                if (angle < 0)
                    angle += 360f;
                var distance = Vector2.Distance(pos1, pos2); //target distance stored here
                
                effect.Material.mainTexture = sprite.Sprites[0].texture;
                
                LaunchWaterBall(effect, sprite, effect.Material, angle, 0, distance, 0);
            }

            return step < effect.DurationFrames;
        }
        
        private static void LaunchWaterBall(Ragnarok3dEffect effect, RoSpriteData sprite, Material mat, float forwardAngle, float upAngle, float distance, float delay)
        {
            var frames = 40f;
            var prim = effect.LaunchPrimitive(PrimitiveType.Particle3DSpline, mat, 1f);
            prim.CreateSegments(12);
            prim.DelayTime = delay;
            prim.FrameDuration = Mathf.RoundToInt(frames); //shorter than proper duration to allow for the segments to fade out
            prim.RenderHandler = Particle3DSplinePrimitive.RenderParticle3DSplineSpritePrimitive;

            var backSpeed = 0.467f;

            var data = prim.GetPrimitiveData<Particle3DSplineData>();
            data.Position = Vector3.zero;
            data.Velocity = new Vector2(distance / frames - backSpeed, 0.267f);
            data.Size = 0.467f;
            data.Acceleration = new Vector2(-(-backSpeed / frames) * 2, -(data.Velocity.y / frames) * 2f);
            data.SpriteData = sprite;
            data.AnimTime = 12; //5 fps
            data.DoShrink = false;
            data.Rotation = Quaternion.Euler(0, forwardAngle, upAngle);
        }
    }
}