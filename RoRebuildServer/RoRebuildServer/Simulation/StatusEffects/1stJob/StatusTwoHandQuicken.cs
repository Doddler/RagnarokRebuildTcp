using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob
{
    public class StatusTwoHandQuicken : StatusEffectBase
    {
        public override StatusUpdateMode UpdateMode => StatusUpdateMode.None;
        public override bool OnApply(Player p, ref StatusEffectState state)
        {


            return true;
        }
    }
}
