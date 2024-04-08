using System.Globalization;
using System.Reflection;
using System.Text;
using CsvHelper;
using RebuildSharedData.Data;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data.Config;
using RoRebuildServer.Data.CsvDataTypes;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Data.Player;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.Logging;
using RoRebuildServer.ScriptSystem;

namespace RoRebuildServer.Data;

internal class DataLoader
{
    public List<MapEntry> LoadMaps()
    {
        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/Maps.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var maps = csv.GetRecords<MapEntry>().ToList();

        ServerLogger.Log($"Loading maps: {maps.Count}");

        return maps;
    }

    public List<InstanceEntry> LoadInstances()
    {
        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/Instances.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var instances = new List<InstanceEntry>();

        csv.Read(); //skip header row, we aren't using it

        while (csv.Read())
        {
            
            if (csv.Context.Parser.Record == null)
                continue; //piss off possible null exceptions
            var instance = new InstanceEntry
            {
                Name = csv.Context.Parser.Record.ElementAt(0),
                IsWorldInstance = csv.Context.Parser.Record.ElementAt(1) == "true",
                Maps = csv.Context.Parser.Record.Skip(2).ToList()
            };
            instances.Add(instance);
        }

        return instances;
    }
    
    public ExpChart LoadExpChart()
    {
        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/ExpChart.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var entries = csv.GetRecords<CsvExpChart>().ToList();

        var chart = new ExpChart { ExpRequired = new int[100] };
        
        chart.ExpRequired[0] = 0; //should always be true but why not!

        foreach (var e in entries)
        {
            chart.ExpRequired[e.Level] = e.Experience;
        }

        return chart;
    }

    public ElementChart LoadElementChart()
    {
        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/ElementalChart.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var entries = csv.GetRecords<dynamic>().ToList();

        var attackTypes = Enum.GetNames(typeof(AttackElement)).Length;
        var defenseTypes = Enum.GetNames(typeof(CharacterElement)).Length;

        var chart = new int[defenseTypes][];
        for (var i = 0; i < defenseTypes; i++)
        {
            chart[i] = new int[attackTypes];
            IDictionary<string, object> row = entries[i];
            var values = row.Values.ToList();

            for (var j = 0; j < attackTypes; j++)
            {
                if(int.TryParse((string)values[j+1], out var percent))
                   chart[i][j] = percent;
            }

        }

        return new ElementChart(chart);
    }

    public Dictionary<string, SavePosition> LoadSavePoints()
    {
        
        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/SavePoints.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var entries = csv.GetRecords<CsvSavePoints>().ToList();

        var savePoints = new Dictionary<string, SavePosition>();

        foreach (var e in entries)
        {
            savePoints.Add(e.Name, new SavePosition()
            {
                MapName = e.Map,
                Position = new Position(e.X, e.Y),
                Area = e.Area,
            });
        }

        return savePoints;
    
    }

    public Dictionary<string, int> LoadEffectIds()
    {
        var effects = new Dictionary<string, int>();

        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/Effects.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var entries = csv.GetRecords<CsvEffects>().ToList();

        foreach (var entry in entries)
        {
            effects.Add(entry.Name, entry.Id);
        }

        return effects;
    }

    public Dictionary<int,JobInfo> LoadJobs()
    {
        var jobs = new Dictionary<int, JobInfo>();

        var timings = new Dictionary<string, float[]>();

        var timingEntries = File.ReadAllLines(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/WeaponAttackTiming.csv"), Encoding.UTF8);
        foreach (var timingEntry in timingEntries.Skip(1))
        {
            var s = timingEntry.Split(",");
            var cName = s[0];
            var timing = s.Skip(1).Select(f => float.Parse(f)).ToArray();
            timings.Add(cName, timing);
        }

        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/Jobs.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);
        
        var entries = csv.GetRecords<CsvJobs>().ToList();
        
        foreach (var entry in entries)
        {
            if (!timings.ContainsKey(entry.Class))
                throw new Exception($"WeaponAttackTiming.csv does not contain a timing definition for the job {entry.Class}.");

            var job = new JobInfo()
            {
                Id = entry.Id,
                Class = entry.Class,
                HP = entry.HP,
                SP = entry.SP,
                WeaponTimings = timings[entry.Class]
            };
            jobs.Add(job.Id, job);
        }
        return jobs;
    }

    public Dictionary<string, int> GetJobIdLookup(Dictionary<int, JobInfo> jobs)
    {
        var lookup = new Dictionary<string, int>();
        foreach (var j in jobs)
        {
            lookup.Add(j.Value.Class, j.Key);
        }

        return lookup;
    }

    public List<string> LoadMvpList()
    {
        var mvps = new List<string>();

        foreach (var line in File.ReadAllLines(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/MvpList.csv"), Encoding.UTF8).Skip(1))
        {
            mvps.Add(line);
        }
        
        return mvps;
    }

    public Dictionary<int, ItemInfo> LoadItemList()
    {
        var items = new Dictionary<int, ItemInfo>();

        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/Items.csv"), Encoding.UTF8) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var entries = csv.GetRecords<CsvItem>().ToList();
        
        foreach (var entry in entries)
        {
            var item = new ItemInfo()
            {
                Code = entry.Code,
                Id = entry.Id,
                IsUseable = entry.IsUseable,
                Price = entry.Price,
                Weight = entry.Weight,
                Effect = -1,
            };

            if (!string.IsNullOrWhiteSpace(entry.Effect))
            {
                if (DataManager.EffectIdForName.TryGetValue(entry.Effect, out var effectId))
                    item.Effect = effectId;
                else
                    ServerLogger.LogWarning($"Could not find effect '{entry.Effect}' with name '{item.Code}'.");
            }

            items.Add(item.Id, item);
        }

        return items;
    }

    public Dictionary<string, int> GenerateItemIdByNameLookup()
    {
        var lookup = new Dictionary<string, int>();

        foreach (var item in DataManager.ItemList)
        {
            lookup.Add(item.Value.Code, item.Value.Id);
        }

        return lookup;
    }

    public void LoadItemInteractions(Assembly assembly)
    {
        var itemType = typeof(IItemLoader);
        foreach (var type in assembly.GetTypes().Where(t => t.IsAssignableTo(itemType)))
        {
            var handler = (IItemLoader)Activator.CreateInstance(type)!;
            handler.Load();
        }
    }

    public List<MonsterDatabaseInfo> LoadMonsterStats()
    {
        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/Monsters.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var monsters = csv.GetRecords<CsvMonsterData>().ToList();

        var obj = new List<MonsterDatabaseInfo>(monsters.Count);

        foreach (var monster in monsters)
        {
            if (monster.Id <= 0)
                continue;

            obj.Add(new MonsterDatabaseInfo()
            {
                Id = monster.Id,
                Code = monster.Code,
                Level = monster.Level,
                HP = monster.HP,
                Exp = monster.Exp,
                Def = monster.Def,
                Vit = monster.Vit,
                Range = monster.Range > 0 ? monster.Range + 1 : 1,
                ScanDist = monster.ScanDist,
                ChaseDist = monster.ChaseDist,
                AtkMin = monster.AtkMin,
                AtkMax = monster.AtkMax,
                AttackTime = monster.AttackTime / 1000f,
                HitTime = monster.HitTime / 1000f,
                RechargeTime = monster.RechargeTime / 1000f,
                MoveSpeed = monster.MoveSpeed / 1000f,
                SpriteAttackTiming = monster.SpriteAttackTiming / 1000f,
                Element = monster.Element,
                AiType = (MonsterAiType)Enum.Parse(typeof(MonsterAiType), monster.MonsterAi),
                Name = monster.Name
            });
        }

        ServerLogger.Log($"Loading monsters: {obj.Count}");

        using var tr2 = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/Npcs.csv")) as TextReader;
        using var csv2 = new CsvReader(tr2, CultureInfo.InvariantCulture);

        var npcCount = 0;
        var npcs = csv2.GetRecords<CsvNpc>().ToList();

        foreach (var npc in npcs)
        {
            obj.Add(new MonsterDatabaseInfo()
            {
                Id = npc.Id,
                Code = npc.Code,
                Name = npc.Name,
                AiType = MonsterAiType.AiEmpty,
            });
            npcCount++;
        }


        ServerLogger.Log($"Loading npc types: {npcCount}");

        return obj;
    }
    
    public Dictionary<string, string> LoadServerConfig()
    {
        var config = new Dictionary<string, string>();

        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/ServerSettings.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var entries = csv.GetRecords<CsvServerConfig>().ToList();

        foreach (var entry in entries)
        {
            config.Add(entry.Key, entry.Value);
        }

        return config;
    }
    
    public void LoadNpcScripts(Assembly assembly)
    {
        var itemType = typeof(INpcLoader);
        foreach (var type in assembly.GetTypes().Where(t => t.IsAssignableTo(itemType)))
        {
            var handler = (INpcLoader)Activator.CreateInstance(type)!;
            handler.Load();
        }
    }
    
    public Dictionary<string, Action<ServerMapConfig>> LoadMapConfigs(Assembly assembly)
    {
        var configs = new Dictionary<string, Action<ServerMapConfig>>();

        var attr = typeof(ServerMapConfigAttribute);
        var types = assembly.GetTypes().SelectMany(t => t.GetMethods())
            .Where(m => m.GetCustomAttributes(attr, false).Length > 0).ToArray();

        foreach (var type in types)
        {
            var action = (Action<ServerMapConfig>)type.CreateDelegate(typeof(Action<ServerMapConfig>));
            var at = type.GetCustomAttribute<ServerMapConfigAttribute>();

            if (at != null) configs.Add(at.MapName, action);
        }

        return configs;
    }

    public List<List<MonsterAiEntry>> LoadAiStateMachines()
    {
        var aiTypeCount = Enum.GetNames(typeof(MonsterAiType)).Length;
        var entryList = new List<List<MonsterAiEntry>>(aiTypeCount);
        for (var i = 0; i < aiTypeCount; i++)
            entryList.Add(new List<MonsterAiEntry>());

        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/MonsterAI.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var states = csv.GetRecords<CsvMonsterAI>().ToList();

        foreach (var entry in states)
        {
            var hasError = false;
            hasError |= !Enum.TryParse(entry.AiType, out MonsterAiType aiType);
            hasError |= !Enum.TryParse(entry.State, out MonsterAiState inState);
            hasError |= !Enum.TryParse(entry.InputCheck, out MonsterInputCheck inCheck);
            hasError |= !Enum.TryParse(entry.OutputCheck, out MonsterOutputCheck outCheck);
            hasError |= !Enum.TryParse(entry.EndState, out MonsterAiState outState);

            if (hasError)
                throw new Exception($"Could not parse Ai States: {entry.AiType},{entry.State},{entry.InputCheck},{entry.OutputCheck},{entry.EndState}");

            entryList[(int)aiType].Add(new MonsterAiEntry()
            {
                InputState = inState,
                InputCheck = inCheck,
                OutputCheck = outCheck,
                OutputState = outState
            });
        }

        return entryList;
    }

    public void LoadMonsterSpawnMinions()
    {

        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/SpawnMinionTable.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var spawns = csv.GetRecords<CsvMonsterSpawnMinions>().ToList();

        foreach (var entry in spawns)
        {
            if (!DataManager.MonsterCodeLookup.TryGetValue(entry.Monster, out var targetMonster))
                throw new Exception($"Error loading SpawnMinionTable.csv, could not find monster named {entry.Monster}.");

            if (!DataManager.MonsterCodeLookup.TryGetValue(entry.Minion, out var minion))
                throw new Exception($"Error loading SpawnMinionTable.csv, could not find minion named {entry.Minion}.");

            targetMonster.Minions ??= new List<MonsterSpawnMinions>();
            targetMonster.Minions.Add(new MonsterSpawnMinions() {Count = entry.Count, Monster = minion});
        }
    }

    public Dictionary<int, EmoteInfo> LoadEmotes()
    {
        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/Emotes.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var data = new Dictionary<int, EmoteInfo>();
        var emotes = csv.GetRecords<CsvEmote>().ToList();

        foreach (var emote in emotes)
        {

        }

        return data;
    }
}