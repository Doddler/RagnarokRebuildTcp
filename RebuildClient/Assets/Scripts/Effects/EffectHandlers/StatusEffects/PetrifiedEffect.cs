using Assets.Scripts.Network;

namespace Assets.Scripts.Effects.EffectHandlers.StatusEffects
{
    [RoEffect("Petrified")]
    public class PetrifiedEffect : IEffectHandler
    {
        public static void LaunchPetrifiedEffect(ServerControllable target)
        {
            
        }
        
        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            throw new System.NotImplementedException();
        }
    }
}