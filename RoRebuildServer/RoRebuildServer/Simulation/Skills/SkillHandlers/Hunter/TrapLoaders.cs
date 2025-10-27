using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Npcs;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;

public class TrapLoaders : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent(nameof(AnkleSnareEvent), new AnkleSnareEvent());
        DataManager.RegisterEvent(nameof(LandMineEvent), new LandMineEvent());
        DataManager.RegisterEvent(nameof(BlastMineEvent), new BlastMineEvent());
        DataManager.RegisterEvent(nameof(ClaymoreTrapEvent), new ClaymoreTrapEvent());
        DataManager.RegisterEvent(nameof(FlasherEvent), new FlasherEvent());
        DataManager.RegisterEvent(nameof(SandmanTrapEvent), new SandmanTrapEvent());
        DataManager.RegisterEvent(nameof(SkidTrapEvent), new SkidTrapEvent());
        DataManager.RegisterEvent(nameof(TalkieBoxEvent), new TalkieBoxEvent());
        DataManager.RegisterEvent(nameof(ShockwaveTrapEvent), new ShockwaveTrapEvent());
    }
}