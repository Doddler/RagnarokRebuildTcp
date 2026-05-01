using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.General
{
    [RoEffect("ScreenShake")]
    public class ScreenShakeEffect  : IEffectHandler
    {
        public static void DelayedShake(Vector3 target, float delay)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.ScreenShake);
            effect.ActiveDelay = delay;
            effect.SetDurationByFrames(60);
            effect.transform.position = target;
            effect.UpdateOnlyOnFrameChange = true;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if(step == 0)
                CameraFollower.Instance.ShakeTime = 0.5f;
                

            return step < effect.DurationFrames;
        }
    }
}