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
    }
}