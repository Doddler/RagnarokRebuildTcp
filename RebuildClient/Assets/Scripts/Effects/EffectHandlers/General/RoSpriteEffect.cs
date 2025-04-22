using System;
using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.General
{
    [Flags]
    public enum RoSpriteEffectFlags
    {
        None,
        EndWithAnimation,
    }
    
    [RoEffect("RoSprite")]
    public class RoSpriteEffect : IEffectHandler
    {
        public static Ragnarok3dEffect AttachSprite(ServerControllable target, string sprite, float offset, float animSpeed = 1, RoSpriteEffectFlags flags = RoSpriteEffectFlags.None)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.RoSprite);
            effect.SourceEntity = target;
            effect.SetDurationByTime(999999);
            effect.UpdateOnlyOnFrameChange = true;
            effect.PositionOffset = target.transform.position;
            effect.DataValue = animSpeed;
            effect.StringValue = sprite;
            effect.PositionOffset = new Vector3(0f, offset, 0f);
            effect.Flags[0] = (int)flags;
            effect.gameObject.transform.localScale = Vector3.one;
            
            EffectSharedMaterialManager.PrepareEffectSprite(sprite);
            
            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (effect.SourceEntity == null)
                return false;
            
            if (step == 0)
            {
                if (!EffectSharedMaterialManager.TryGetEffectSprite(effect.StringValue, out var sprite))
                {
                    effect.ResetStep();
                    return true; //wait until the sprite loads
                }

                var spriteContainer = new GameObject(sprite.Name);
                spriteContainer.transform.SetParent(effect.SourceEntity.transform, false);
                spriteContainer.transform.localPosition = Vector3.zero;
                
                var spriteObj = new GameObject("Sprite");
                spriteObj.layer = LayerMask.NameToLayer("Characters");
                spriteObj.transform.SetParent(spriteContainer.transform, false);
                spriteObj.transform.localPosition = new Vector3(0f, 0f, -0.08f);

                var layer = spriteObj.AddComponent<RoSpriteAnimator>();

                layer.SpriteOrder = 30; //weapon is 5 so we should be below that
                layer.LockAngle = true; //this also excludes this sprite from interacting with water
                layer.IgnoreShadows = true;
                layer.NoLightProbeAnchor = true;
                layer.VerticalOffset = effect.PositionOffset.y;
                layer.OnSpriteDataLoadNoCollider(sprite);
                layer.AnimSpeed = effect.DataValue;

                var flags = (RoSpriteEffectFlags)effect.Flags[0];
                if(flags.HasFlag(RoSpriteEffectFlags.EndWithAnimation))
                    layer.OnFinishAnimation = effect.EndEffectWithNotifyOwner;
                else
                    layer.ForceLoop = true;
                
                layer.ChangeActionExact(0);
                
                effect.AttachedObjects.Add(spriteContainer);
            }

            return true;
        }
    }
}