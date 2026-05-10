using RoRebuildServer.Data;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Data.ServerConfigScript;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Logging;
using System.Runtime.CompilerServices;

namespace RoRebuildServer.Custom.BountySystem;

record MiasmaDungeonDef(string DungeonName, List<string> SignalNames);

public class BountySystemManager : ServerConfigScriptHandlerBase
{
    private static List<MiasmaDungeonDef> MiasmaDungeons = new()
    {
        new MiasmaDungeonDef("Pyramids", new List<string>() { "MiasmaPyramids1", "MiasmaPyramids2", "MiasmaPyramids3", "MiasmaPyramids4", "MiasmaPyramidsB1", "MiasmaPyramidsB2" }),
    };

    private Dictionary<string, int> dungeonLookup;
    private int[] activeDungeons = [-1, -1];
    private int[] nextDungeons = [-1, -1];
    private DateTime[] rolloverTimes = new DateTime[2];

    public override void PostServerStartEvent()
    {
        if (!ServerConfig.OperationConfig.ActiveEvents?.Contains("BountySystem") ?? false)
            return;

        if (dungeonLookup == null! || dungeonLookup.Count != MiasmaDungeons.Count)
        {
            dungeonLookup = new();
            for (var i = 0; i < MiasmaDungeons.Count; i++)
            {
                dungeonLookup.Add(MiasmaDungeons[i].DungeonName, i);
            }
        }

        ServerLogger.Log("BountySystem is currently enabled!");

        MonsterRewardManager.RegisterKillMonsterEvent(OnKillMonster);
        MonsterRewardManager.RegisterDistributeExperienceEvent(DistributeExperience);
    }

    private void OnKillMonster(Monster monster)
    {

    }

    private void DistributeExperience(Monster monster, Player player, ref int baseExp, ref int jobExp)
    {

    }
}
