using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.General
{
    [RoEffect("RoProjectileSprite")]
    public class RoSpriteProjectileEffect : IEffectHandler
    {
        public static Ragnarok3dEffect CreateProjectile(ServerControllable source, GameObject target, string projectileType, Color color, float delayTime, float offset = 0f)
        {
            var speed = 38f + Random.Range(0, 4f);
            
            var startPosition = source.transform.position + new Vector3(0, 2+offset, 0);
            var targetPosition = target.transform.position + new Vector3(0, 2+offset+Random.Range(-0.1f, 0.1f), 0);
            var distance = Vector3.Distance(startPosition, targetPosition);

            var effect = RagnarokEffectPool.Get3dEffect(EffectType.RoProjectileSprite);
            effect.SourceEntity = source;
            effect.AimTarget = target;
            effect.SetDurationByTime(distance / speed);
            effect.transform.position = startPosition;
            effect.ActiveDelay = delayTime;
            effect.PositionOffset = target.transform.position;
            effect.StringValue = projectileType;
            
            EffectSharedMaterialManager.PrepareEffectSprite(projectileType);

            var mat = EffectSharedMaterialManager.GetProjectileMaterial(projectileType);

            var prim = effect.LaunchPrimitive(PrimitiveType.DirectionalBillboard, mat, effect.Duration);
            var data = prim.GetPrimitiveData<EffectSpriteData>();

            data.FrameTime = 12;
            data.Alpha = color.a * 255f;
            data.Style = BillboardStyle.AxisAligned;
            data.BaseRotation = new Vector3(0, 0, 180);
            data.Color = color;

            prim.Velocity = (targetPosition - startPosition).normalized * speed; //-2.75f;
            prim.transform.position = startPosition + prim.Velocity * 0.03f;
            prim.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

            return effect;
        }
        
        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step == 0)
            {
                if (!EffectSharedMaterialManager.TryGetEffectSprite(effect.StringValue, out var sprite))
                {
                    effect.ResetStep();
                    return true; //wait until the sprite loads
                }

                var p = effect.Primitives[0];
                p.Material.mainTexture = sprite.Sprites[0].texture;
                var data = p.GetPrimitiveData<EffectSpriteData>();
                data.SpriteData = sprite;
                data.Frame = 0;
                data.FrameTime = data.SpriteData.Actions[0].Delay;
                data.TextureCount = data.SpriteData.Actions[0].Frames.Length;
                data.AnimateTexture = true;
            }
            
            if (effect.Primitives.Count < 1)
                return false;

            var prim = effect.Primitives[0];
            var targetPos = effect.PositionOffset;
            if (effect.AimTarget != null)
                targetPos = effect.AimTarget.transform.position;
            if (!prim.IsActive || effect.SourceEntity == null)
                return false;

            var target = targetPos + new Vector3(0, 2, 0);
            var speed = prim.Velocity.magnitude;
            var distToArrow = Vector3.Distance(effect.SourceEntity.transform.position, prim.transform.position);
            var distToTarget = Vector3.Distance(effect.SourceEntity.transform.position, targetPos);
            
            // Debug.Log($"Distance {distToArrow} {distToTarget}");
            
            if (distToArrow >= distToTarget)
            {
                prim.EndPrimitive();
                return false;
            }
            
            prim.Velocity = (target - prim.transform.position).normalized * speed;

            return step < effect.DurationFrames;
        }
    }
}