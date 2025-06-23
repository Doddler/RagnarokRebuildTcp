using RoRebuildServer.Data;
using RoRebuildServer.Data.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RoWikiGenerator.Pages;
using static RoWikiGenerator.Program;
using RoRebuildServer.Data.Monster;

namespace RoWikiGenerator.Generators;

public class MonsterModel
{
    public required Dictionary<int, List<(string map, MapSpawnRule spawn)>> MonsterMapSpawns;
    public required Dictionary<string, string> SharedIcons;
    public required List<MonsterDatabaseInfo> Monsters;
}

internal static class Monsters
{
    public static MonsterModel MonsterModel;
    public static Dictionary<int, string> IdToTagLookup = new();

    public static async Task<string> RenderMonsterPage()
    {
        var monsterMapSpawns = new Dictionary<int, List<(string map, MapSpawnRule spawn)>>();

        foreach (var (tag, id) in DataManager.TagToIdLookup)
        {
            var tString = tag;
            if (tString == "WeakToSilver")
                tString = "Weak to Silver";
            IdToTagLookup.Add(id, tString);
        }

        foreach (var map in DataManager.Maps)
        {
            var spawns = new WikiSpawnConfig();
            if (!DataManager.MapConfigs.TryGetValue(map.Code, out var loader))
                continue;
            if (!DataManager.InstanceList.Any(i => i.Maps.Contains(map.Code)))
                continue;

            loader(spawns);
            foreach (var spawn in spawns.SpawnRules)
            {
                if (!monsterMapSpawns.TryGetValue(spawn.MonsterDatabaseInfo.Id, out var monList))
                {
                    monList = new List<(string map, MapSpawnRule spawn)>();
                    monsterMapSpawns.Add(spawn.MonsterDatabaseInfo.Id, monList);
                }

                var existing = monList.FirstOrDefault(m =>
                    m.spawn.MonsterDatabaseInfo.Id == spawn.MonsterDatabaseInfo.Id && m.map == map.Code
                    && m.spawn.MinSpawnTime == spawn.MinSpawnTime && m.spawn.MaxSpawnTime == spawn.MaxSpawnTime);

                if (existing.spawn != null)
                    existing.spawn.Count += spawn.Count;
                else
                    monList.Add((map.Code, spawn));
            }
        }

        var monsters = DataManager.MonsterIdLookup.Select(m => m.Value)
            //.OrderBy(m => m.Level).ThenBy(m => m.Name).ToList();
            .Where(m => m.Code != "VALKYRIE" && m.Code != "RANDGRIS" && m.Code != "ICE_TITAN").OrderBy(m => m.Exp + (m.Exp == 0 ? 99000000 : 0)).ToList();

        var model = new MonsterModel()
        {
            MonsterMapSpawns = monsterMapSpawns,
            SharedIcons = Items.SharedIcons,
            Monsters = monsters
        };

        MonsterModel = model;

        return await RenderPage<MonsterModel, MonsterListFull>(model);
    }
}