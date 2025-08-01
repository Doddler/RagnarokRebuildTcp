using RebuildSharedData.Data;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Custom.OkolnirEvent;


public class OkolnirEventBattle : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent("OkolnirDamageZone", new OkolnirDamageZoneObjectEvent());
        DataManager.RegisterEvent("ExaflareControlEvent", new ExaflareControlEvent());
        DataManager.RegisterEvent("ExaflareRowEvent", new ExaflareRowEvent());
        DataManager.RegisterEvent("ExaflareBlastEvent", new ExaflareBlastEvent());
        DataManager.RegisterEvent("EarthShakerCastEvent", new EarthShakerSkillEvent());
    }
}
