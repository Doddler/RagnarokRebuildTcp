using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.Data.MapData;

namespace DataToClientUtility;

internal static class MonsterJsonExport
{
    public static void Write(string outPath)
    {
        var idToTagLookup = new Dictionary<int, string>();
        foreach (var (tag, id) in DataManager.TagToIdLookup)
        {
            var tString = tag;
            if (tString == "WeakToSilver")
                tString = "Weak to Silver";
            idToTagLookup.Add(id, tString);
        }

        var monsterMapSpawns = new Dictionary<int, List<(string map, MapSpawnRule spawn)>>();
        foreach (var map in DataManager.Maps)
        {
            var spawns = new ExportSpawnConfig();
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
            .Where(m => m.Id >= 4000 && m.Code != "ICE_TITAN")
            .OrderBy(m => m.Exp + (m.Exp == 0 ? 99000000 : 0))
            .ToList();

        var output = new MonsterDbFile { Items = new List<MonsterDbEntry>() };

        foreach (var mob in monsters)
        {
            var entry = new MonsterDbEntry
            {
                Id = mob.Id,
                Code = mob.Code,
                Name = mob.Name,
                Level = mob.Level,
                HP = mob.HP,
                Exp = mob.Exp,
                JExp = mob.JobExp,
                AtkMin = mob.AtkMin,
                AtkMax = mob.AtkMax,
                Def = mob.Def,
                MDef = mob.MDef,
                Str = mob.Str,
                Agi = mob.Agi,
                Vit = mob.Vit,
                Int = mob.Int,
                Dex = mob.Dex,
                Luk = mob.Luk,
                Range = mob.Range,
                ScanDist = mob.ScanDist,
                MoveSpeed = mob.MoveSpeed,
                Size = mob.Size.ToString(),
                Element = mob.Element.ToString(),
                Race = mob.Race.ToString(),
                Ai = mob.AiType.ToString(),
                Special = mob.Special.ToString(),
                Tags = new List<string>(),
                Drops = new List<MonsterDbDropEntry>(),
                Spawns = new List<MonsterDbSpawnEntry>(),
            };

            if (mob.Tags != null)
            {
                foreach (var tagId in mob.Tags)
                {
                    if (idToTagLookup.TryGetValue(tagId, out var tagName))
                        entry.Tags.Add(tagName);
                }
            }

            if (DataManager.MonsterDropData.TryGetValue(mob.Code, out var dropData))
            {
                foreach (var drop in dropData.DropChances)
                {
                    entry.Drops.Add(new MonsterDbDropEntry
                    {
                        ItemId = drop.Id,
                        Chance = drop.Chance,
                        CountMin = drop.CountMin,
                        CountMax = drop.CountMax,
                    });
                }
            }

            if (monsterMapSpawns.TryGetValue(mob.Id, out var spawnsForMob))
            {
                foreach (var (mapCode, rule) in spawnsForMob)
                {
                    entry.Spawns.Add(new MonsterDbSpawnEntry
                    {
                        Map = mapCode,
                        Count = rule.Count,
                        RespawnMin = rule.MinSpawnTime,
                        RespawnMax = rule.MaxSpawnTime,
                        IsBoss = rule.DisplayType == CharacterDisplayType.Boss,
                        IsMvp = rule.DisplayType == CharacterDisplayType.Mvp,
                    });
                }
            }

            output.Items.Add(entry);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var json = JsonSerializer.Serialize(output, options);

        var outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, outPath));
        Directory.CreateDirectory(outDir);

        var monsterPath = Path.Combine(outDir, "monsterdatabase.json");
        File.WriteAllText(monsterPath, json);

        Console.WriteLine($"Writing data to {monsterPath}");
    }
}
