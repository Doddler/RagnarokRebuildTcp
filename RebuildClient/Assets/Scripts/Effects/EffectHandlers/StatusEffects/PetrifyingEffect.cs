using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.StatusEffects
{
    [RoEffect("Petrifying")]
    public class PetrifyingEffect : IEffectHandler
    {
        public static Ragnarok3dEffect LaunchPetrifyingEffect(ServerControllable controllable, float length)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Petrifying);
            effect.SetDurationByTime(length); //we'll manually end this early probably
            effect.FollowTarget = controllable.gameObject;
            effect.SourceEntity = controllable;
            effect.DataValue = Time.timeSinceLevelLoad;

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (effect.SourceEntity == null || effect.SourceEntity.SpriteAnimator == null)
                return false;

            var sr = effect.SourceEntity.SpriteAnimator;
            var progress = Mathf.Clamp01(effect.CurrentPos / effect.Duration);

            sr.Color = Color.Lerp(Color.white, StatusEffectState.StoneColor, progress);
            sr.AnimSpeed = 1 + 10 * progress;

            if(sr.SpriteRenderer is RoSpriteRendererStandard renderer)
                renderer.SetColorDrain(progress);
            
            foreach(var child in sr.ChildrenSprites)
                if(child.SpriteRenderer is RoSpriteRendererStandard childRenderer)
                    childRenderer.SetColorDrain(progress);

            return true; //stay forever
        }

        public void OnCleanup(Ragnarok3dEffect effect)
        {
            if (effect.SourceEntity == null || effect.SourceEntity.SpriteAnimator == null)
                return;

            var progress = 0;

            var sr = effect.SourceEntity.SpriteAnimator;
            if(sr.SpriteRenderer is RoSpriteRendererStandard renderer)
                renderer.SetColorDrain(progress);
            
            foreach(var child in sr.ChildrenSprites)
                if(child.SpriteRenderer is RoSpriteRendererStandard childRenderer)
                    childRenderer.SetColorDrain(progress);
        }
    }
}