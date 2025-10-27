﻿using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data.CsvDataTypes;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Data.Player;
using RoRebuildServer.Data.ServerConfigScript;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.EntityComponents.Monsters;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.Logging;
using Tomlyn;

namespace RoRebuildServer.Data;

internal class DataLoader
{
    public int LoadVersionInfo()
    {
        var lines = File.ReadAllLines(Path.Combine(ServerConfig.DataConfig.DataPath, @"Config/ServerClientConfig.txt"));
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("::ServerVersion") && i + 1 < lines.Length)
            {
                if (!int.TryParse(lines[i + 1], out var version))
                {
                    ServerLogger.LogWarning($"Could not read version info from ServerClientConfig.txt");
                    return 0;
                }

                return version;
            }
        }

        ServerLogger.LogWarning($"Did not find ::ServerVersion section in ServerClientConfig.txt");
        return 0;
    }

    public (ReadOnlyDictionary<string, MapEntry>, ReadOnlyDictionary<string, MapFlags>) LoadMaps()
    {
        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/Maps.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var maps = csv.GetRecords<MapEntry>().ToList();
        var mapsLookup = new Dictionary<string, MapEntry>();
        var mapFlags = new Dictionary<string, MapFlags>();

        ServerLogger.Log($"Loading maps: {maps.Count}");

        foreach (var map in maps)
        {
            mapsLookup.Add(map.Code, map);
            mapFlags.Add(map.Code, map.GetFlags());
        }

        return (mapsLookup.AsReadOnly(), mapFlags.AsReadOnly());
    }

    public List<InstanceEntry> LoadInstances()
    {
        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/Instances.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var instances = new List<InstanceEntry>();

        csv.Read(); //skip header row, we aren't using it

        while (csv.Read())
        {

            if (csv.Context?.Parser?.Record == null)
                continue; //piss off possible null exceptions
            var instance = new InstanceEntry
            {
                Name = csv.Context.Parser.Record.ElementAt(0),
                IsWorldInstance = csv.Context.Parser.Record.ElementAt(1) == "true",
                Maps = csv.Context.Parser.Record.Skip(2).ToList()
            };
            if (instance.Name.StartsWith("//")) //special case for commented out instance
                continue;
            instances.Add(instance);
        }

        return instances;
    }

    public ExpChart LoadExpChart()
    {
        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/ExpChart.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var entries = csv.GetRecords<CsvExpChart>().ToList();

        var chart = new ExpChart { ExpRequired = new int[100], JobExpRequired = new int[3 * 70] };

        chart.ExpRequired[0] = 0; //should always be true but why not!
        chart.JobExpRequired[0] = -1;
        chart.JobExpRequired[70] = -1;
        chart.JobExpRequired[140] = -1;

        foreach (var e in entries)
        {
            chart.ExpRequired[e.Level] = e.Experience;
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = context =>
        {
            Console.WriteLine(context.Field);
        }
        };

        using var tr2 = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/ExpJobChart.csv")) as TextReader;
        using var csv2 = new CsvReader(tr2, config);

        var entries2 = csv2.GetRecords<CsvJobExpChart>();

        foreach (var e in entries2)
        {
            if (e.JobLvl > 69)
                continue;
            chart.JobExpRequired[e.JobLvl] = e.Novice;
            chart.JobExpRequired[70 + e.JobLvl] = e.FirstJob;
            chart.JobExpRequired[140 + e.JobLvl] = e.SecondJob;
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
                if (int.TryParse((string)values[j + 1], out var percent))
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

    public ReadOnlyDictionary<int, JobInfo> LoadJobs()
    {
        var jobs = new Dictionary<int, JobInfo>();

        var timings = new Dictionary<string, float[]>();

        var timingEntries = File.ReadAllLines(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/WeaponAttackTiming.csv"), Encoding.UTF8);
        foreach (var timingEntry in timingEntries.Skip(1))
        {
            var s = timingEntry.Split(",");
            var cName = s[0];
            var timing = s.Skip(1).Select(f => float.Parse(f, CultureInfo.InvariantCulture)).ToArray();
            timings.Add(cName, timing);
        }

        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/Jobs.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var entries = csv.GetRecords<CsvJobs>().ToList();

        foreach (var entry in entries)
        {
            float[] timing;
            if (timings.ContainsKey(entry.Class))
                timing = timings[entry.Class];
            else
                timing = timings["NoClass"];

            var job = new JobInfo()
            {
                Id = entry.Id,
                Class = entry.Class,
                MaxJobLevel = entry.MaxJobLevel,
                ExpChart = entry.ExpChart,
                WeaponTimings = timing
            };
            jobs.Add(job.Id, job);
        }
        return jobs.AsReadOnly();
    }

    public ReadOnlyDictionary<string, int> GetJobIdLookup(ReadOnlyDictionary<int, JobInfo> jobs)
    {
        var lookup = new Dictionary<string, int>();
        foreach (var j in jobs)
        {
            lookup.Add(j.Value.Class, j.Key);
        }

        return lookup.AsReadOnly();
    }

    public List<string> LoadMvpList()
    {
        var mvps = new List<string>();

        foreach (var line in File.ReadAllLines(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/MvpList.csv"), Encoding.UTF8).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var s = line.Split(',');
            mvps.Add(s[0]);
            var itemCount = (s.Length - 1) / 2;
        }

        return mvps;
    }

    public ReadOnlyDictionary<CharacterSkill, SkillData> LoadSkillData()
    {
        var path = Path.Combine(ServerConfig.DataConfig.DataPath, @"Skills/Skills.toml");
        var options = new TomlModelOptions() { ConvertPropertyName = name => name, ConvertFieldName = name => name, IncludeFields = true };
        var skillData = Toml.ToModel<Dictionary<string, SkillData>>(File.ReadAllText(path, Encoding.UTF8), null, options);
        var skillOut = new Dictionary<CharacterSkill, SkillData>();
        foreach (var (id, skill) in skillData)
        {
            skill.SkillId = Enum.Parse<CharacterSkill>(id);
            if (skill.Name == null) skill.Name = id;
            if (skill.Icon == null) skill.Icon = "nv_basic";
            skillOut.Add(skill.SkillId, skill);
        }

        return skillOut.AsReadOnly();
    }

    private CharacterSkill LookupSkillIdByName(string skill)
    {
        if (!Enum.TryParse<CharacterSkill>(skill, out var skillOut))
            throw new Exception($"Could not find a skill with the name {skill}");

        return skillOut;
    }

    //SkillTree[Job][Skill] { (Prereq, lvl) } 
    public ReadOnlyDictionary<int, PlayerSkillTree> LoadSkillTree()
    {
        var path = Path.Combine(ServerConfig.DataConfig.DataPath, @"Skills/SkillTree.toml");
        var options = new TomlModelOptions() { ConvertPropertyName = name => name, ConvertFieldName = name => name, IncludeFields = true };
        var skillTreeData = Toml.ToModel<Dictionary<string, CsvPlayerSkillTree>>(File.ReadAllText(path, Encoding.UTF8), null, options);
        var extendList = new Dictionary<int, int>();
        var treeOut = new Dictionary<int, PlayerSkillTree>();
        var parentList = new Dictionary<int, int>();

        foreach (var (id, tree) in skillTreeData)
        {
            var jobId = DataManager.JobIdLookup[id];
            
            var treeData = tree.SkillTree;
            if (treeData == null)
                treeData = new();

            treeOut.Add(jobId, new PlayerSkillTree()
            {
                JobId = jobId,
                JobRank = tree.JobRank,
            
                SkillTree = treeData
            });

            if (tree.Extends != null)
                parentList.Add(jobId, DataManager.JobIdLookup[tree.Extends]);
        }

        foreach (var (job, prereq) in parentList)
        {
            treeOut[job].Parent = treeOut[prereq];
        }

        //calculate how many skill points a job will have from previous jobs
        foreach (var (jobId, tree) in treeOut)
        {
            var p = tree.Parent;
            var skillPoints = 0;
            while (p != null)
            {
                var job = DataManager.JobInfo[p.JobId];
                skillPoints += job.MaxJobLevel - 1;
                p = p.Parent;
            }

            tree.PrereqSkillPoints = skillPoints;
        }

        return treeOut.AsReadOnly();
    }

    //private new Dictionary<(int, int), (int, int)> LoadRemapDrops()
    //{
    //    var remap = new Dictionary<(int, int), (int, int)>();
    //    var remapFile = new Dictionary<string, MonsterDropData>();
    //    var remapPath = Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/DropRateRemapping.csv");

    //    using var tr = new StreamReader(remapPath, Encoding.UTF8) as TextReader;
    //    using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

    //    var entries = csv.GetRecords<dynamic>();
    //    foreach (var entry in entries)
    //    {
    //        if (entry is IDictionary<string, object> obj)
    //        {
    //            var origMin = int.Parse((string)obj["OriginalMin"]);
    //            var origMax = int.Parse((string)obj["OriginalMax"]);
    //            var afterMin = int.Parse((string)obj["AfterMin"]);
    //            var afterMax = int.Parse((string)obj["AfterMax"]);
    //            remap.Add((origMin, origMax), (afterMin, afterMax));
    //        }
    //    }

    //    return remap;
    //}

    public ReadOnlyDictionary<string, MonsterDropData> LoadMonsterDropChanceData(ServerConfigScriptManager config)
    {
        var remapDrops = ServerConfig.OperationConfig.RemapDropRates;

        var drops = new Dictionary<string, MonsterDropData>();
        using var inPath = new TemporaryFile(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/DropData.csv"));
        //using var tr = new StreamReader(inPath.FilePath, Encoding.UTF8) as TextReader;
        //using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var lineNum = 0;
        foreach (var line in File.ReadLines(inPath.FilePath))
        {
            lineNum++;
            if (lineNum <= 1)
                continue;
            var s = line.Split(",");

            var monster = s[0].Replace(" ", "_").ToUpper();
            var data = new MonsterDropData();

            if (!DataManager.MonsterCodeLookup.ContainsKey(monster))
                ServerLogger.LogWarning($"Item drops defined for monster {monster} but that monster could not be found.");

            for (var pos = 1; pos < s.Length; pos += 2)
            {
                var itemName = s[pos];
                if (string.IsNullOrWhiteSpace(itemName))
                    continue;

                var rangeMin = 1;
                var rangeMax = 1;
                if (itemName.Contains("#"))
                {
                    var countSection = itemName.AsSpan(itemName.IndexOf('#') + 1);
                    if (countSection.Contains('-'))
                    {
                        rangeMin = int.Parse(countSection[..countSection.IndexOf('-')]);
                        rangeMax = int.Parse(countSection[(countSection.IndexOf('-') + 1)..]);
                    }

                    itemName = itemName.Substring(0, itemName.IndexOf('#'));
                }


                if (!DataManager.ItemIdByName.TryGetValue(itemName, out var item))
                {
                    ServerLogger.LogWarning($"Monster {monster} dropped item {itemName} was not found in the item list.");
                    //continue;
                }

                var chance = int.Parse(s[pos + 1]);
                if (chance <= 0)
                    continue;


                if (remapDrops)
                {
                    var itemInfo = DataManager.GetItemInfoById(item);
                    if (itemInfo != null)
                        chance = config.UpdateDropData(itemInfo.ItemClass, itemInfo.Code, itemInfo.SubCategory, chance);
                }

                if (item > 0) //for debug reasons mostly
                    data.DropChances.Add(new MonsterDropData.MonsterDropEntry(item, chance, rangeMin, rangeMax));
            }

            drops.Add(monster, data);
        }
        
        return drops.AsReadOnly();
    }

    private ReadOnlyDictionary<int, int[]> ReadHpSpChart(string path)
    {
        var dict = new Dictionary<int, int[]>();
        var inPath = Path.Combine(ServerConfig.DataConfig.DataPath, path);

        using var tr = new StreamReader(inPath, Encoding.UTF8) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        foreach (var job in DataManager.JobInfo)
            dict[job.Key] = new int[100];

        csv.Read();
        csv.ReadHeader();

        if (csv.HeaderRecord == null)
            throw new Exception($"Could not read header on csv {path}!");

        var headers = csv.HeaderRecord;
        var jobList = new Dictionary<int, int>();
        var colCount = 1;
        foreach (var header in headers)
        {
            if (header == "Level")
                continue;
            if (DataManager.JobIdLookup.TryGetValue(header, out var jobId))
                jobList.Add(colCount, jobId);
            colCount++;
        }

        while (csv.Read())
        {
            var lvl = csv.GetField<int>("Level");
            foreach (var (col, job) in jobList)
                dict[job][lvl] = csv.GetField<int>(col);
        }

        return dict.AsReadOnly();
    }

    public ReadOnlyDictionary<int, int[]> LoadMaxHpChart()
    {
        var dict = ReadHpSpChart(@"Db/JobHpChart.csv");
        return dict.AsReadOnly();
    }

    public ReadOnlyDictionary<int, int[]> LoadMaxSpChart()
    {
        var dict = ReadHpSpChart(@"Db/JobSpChart.csv");
        return dict.AsReadOnly();
    }

    private static List<T> GetCsvRows<T>(string fileName, bool hasHeader = true)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = hasHeader };

        var inPath = new TemporaryFile(Path.Combine(ServerConfig.DataConfig.DataPath, fileName));
        using var tr = new StreamReader(inPath.FilePath, Encoding.UTF8) as TextReader;
        using var csv = new CsvReader(tr, config);

        return csv.GetRecords<T>().ToList();
    }

    public ReadOnlyDictionary<string, int> LoadWeaponClasses()
    {
        var weaponClasses = new Dictionary<string, int>();

        var csv = GetCsvRows<CsvWeaponClass>("Db/WeaponClass.csv");
        foreach (var entry in csv)
            weaponClasses.Add(entry.WeaponClass, entry.Id);

        return weaponClasses.AsReadOnly();
    }

    public ReadOnlyDictionary<string, HashSet<int>> LoadEquipGroups()
    {
        var equipableJobs = new Dictionary<string, HashSet<int>>();

        //var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false };
        //using var tr = new StreamReader(@"F:\ProjectsSSD\RagnarokRebuildTcp\RoRebuildServer\GameConfig\ServerData\Db\EquipmentGroups.csv") as TextReader;
        //using var csv = new CsvReader(tr, config);

        //var entries = csv.GetRecords<dynamic>().ToList();
        var entries = GetCsvRows<dynamic>("Db/EquipmentGroups.csv", false);
        var autoDesc = new StringBuilder();

        foreach (IDictionary<string, object> e in entries)
        {
            var s = e.Values.ToList();
            var jobGroup = (string)s[0];
            var hasExisting = equipableJobs.TryGetValue(jobGroup, out var set);
            if (!hasExisting || set == null)
                set = new HashSet<int>();

            for (var i = 2; i < s.Count; i++)
            {
                var job = (string)s[i];
                if (equipableJobs.TryGetValue(job, out var refSet))
                {
                    foreach (var r in refSet)
                        set.Add(r);
                }
                else if (DataManager.JobIdLookup.TryGetValue(job, out var jobId))
                    set.Add(jobId);
                else
                    ServerLogger.Debug($"LoadEquipmentGroups: Could not find job with the name of {job}");
            }

            if (!hasExisting)
                equipableJobs.Add(jobGroup, set);
        }

        return equipableJobs.AsReadOnly();
    }

    public Dictionary<int, ItemInfo> LoadItemsRegular()
    {
        var items = new Dictionary<int, ItemInfo>();

        foreach (var entry in GetCsvRows<CsvItemRegular>("Db/ItemsRegular.csv"))
        {
            var item = new ItemInfo()
            {
                Code = entry.Code,
                Name = entry.Name,
                Id = entry.Id,
                IsUnique = false,
                ItemClass = ItemClass.Etc,
                Price = entry.Price,
                SubCategory = entry.Usage,
                SellToStoreValue = entry.Price / 2,
                Weight = entry.Weight,
            };
            items.Add(item.Id, item);
        }

        return items;
    }

    public ReadOnlyDictionary<int, ArmorInfo> LoadItemsArmor(Dictionary<int, ItemInfo> itemList)
    {
        var returnList = new Dictionary<int, ArmorInfo>();
        foreach (var entry in GetCsvRows<CsvItemEquipment>("Db/ItemsEquipment.csv"))
        {
            var item = new ItemInfo()
            {
                Code = entry.Code,
                Name = entry.Name,
                Id = entry.Id,
                IsUnique = true,
                ItemClass = ItemClass.Equipment,
                Price = entry.Price,
                SubCategory = "Equipment",
                SellToStoreValue = entry.Price / 2,
                Weight = entry.Weight,
            };
            itemList.Add(item.Id, item);

            var armorInfo = new ArmorInfo()
            {
                Defense = entry.Defense,
                MagicDefense = entry.MagicDef,
                CardSlots = entry.Slot,
                Element = entry.Property ?? CharacterElement.Neutral1,
                EquipGroup = entry.EquipGroup,
                IsBreakable = entry.Breakable == "Yes",
                IsRefinable = entry.Refinable == "Yes",
                EquipPosition = entry.Type,
                HeadPosition = entry.Position,
                MinLvl = entry.MinLvl
            };
            returnList.Add(entry.Id, armorInfo);
        }

        return returnList.AsReadOnly();
    }

    public ReadOnlyDictionary<int, WeaponInfo> LoadItemsWeapon(Dictionary<int, ItemInfo> itemList)
    {
        var returnList = new Dictionary<int, WeaponInfo>();
        foreach (var entry in GetCsvRows<CsvItemWeapon>("Db/ItemsWeapons.csv"))
        {
            var item = new ItemInfo()
            {
                Code = entry.Code,
                Name = entry.Name,
                Id = entry.Id,
                IsUnique = true,
                ItemClass = ItemClass.Weapon,
                Price = entry.Price,
                SubCategory = "Weapon",
                SellToStoreValue = entry.Price / 2,
                Weight = entry.Weight,
            };
            itemList.Add(item.Id, item);

            if (!DataManager.WeaponClasses.TryGetValue(entry.Type, out var weaponType))
                weaponType = 0;

            var weaponInfo = new WeaponInfo()
            {
                Attack = entry.Attack,
                Range = entry.Range,
                CardSlots = entry.Slot,
                Element = entry.Property,
                WeaponClass = weaponType,
                WeaponLevel = entry.Rank,
                EquipGroup = entry.EquipGroup,
                IsTwoHanded = entry.Position == WeaponPosition.BothHands,
                IsBreakable = entry.Breakable == "Yes",
                IsRefinable = entry.Refinable == "Yes"
            };
            returnList.Add(entry.Id, weaponInfo);
        }

        return returnList.AsReadOnly();
    }

    public ReadOnlyDictionary<int, CardInfo> LoadItemsCards(Dictionary<int, ItemInfo> itemList)
    {
        var returnList = new Dictionary<int, CardInfo>();
        foreach (var entry in GetCsvRows<CsvItemCard>("Db/ItemsCards.csv"))
        {
            var item = new ItemInfo()
            {
                Code = entry.Code,
                Name = entry.Name,
                Id = entry.Id,
                IsUnique = false,
                ItemClass = ItemClass.Card,
                Price = entry.Price,
                SubCategory = "Card",
                SellToStoreValue = entry.Price / 2,
                Weight = entry.Weight,
            };
            itemList.Add(item.Id, item);

            var cardInfo = new CardInfo() { EquipPosition = entry.EquipableSlot };

            returnList.Add(entry.Id, cardInfo);
        }

        return returnList.AsReadOnly();
    }

    public ReadOnlyDictionary<int, AmmoInfo> LoadItemsAmmo(Dictionary<int, ItemInfo> itemList)
    {
        var returnList = new Dictionary<int, AmmoInfo>();
        foreach (var entry in GetCsvRows<CsvItemAmmo>("Db/ItemsAmmo.csv"))
        {
            var item = new ItemInfo()
            {
                Code = entry.Code,
                Name = entry.Name,
                Id = entry.Id,
                IsUnique = false,
                ItemClass = ItemClass.Ammo,
                Price = entry.Price,
                SubCategory = "Ammo",
                SellToStoreValue = entry.Price / 2,
                Weight = entry.Weight,
            };
            itemList.Add(item.Id, item);

            var ammoInfo = new AmmoInfo() { Type = entry.Type, Attack = entry.Attack, MinLvl = entry.MinLvl, Element = entry.Property };
            returnList.Add(entry.Id, ammoInfo);
        }

        return returnList.AsReadOnly();
    }

    public ReadOnlyDictionary<int, UseItemInfo> LoadUseableItems(Dictionary<int, ItemInfo> itemList)
    {
        var returnList = new Dictionary<int, UseItemInfo>();
        foreach (var entry in GetCsvRows<CsvItemUseable>("Db/ItemsUsable.csv"))
        {
            var item = new ItemInfo()
            {
                Code = entry.Code,
                Name = entry.Name,
                Id = entry.Id,
                IsUnique = false,
                ItemClass = ItemClass.Useable,
                Price = entry.Price,
                SubCategory = "Useable",
                SellToStoreValue = entry.Price / 2,
                Weight = entry.Weight,
            };
            itemList.Add(item.Id, item);

            var useItem = new UseItemInfo() { UseType = entry.UseMode, Effect = -1 };
            if (DataManager.EffectIdForName.TryGetValue(entry.UseEffect, out var effectId))
                useItem.Effect = effectId;
            returnList.Add(entry.Id, useItem);
        }

        return returnList.AsReadOnly();
    }

    public ReadOnlyDictionary<string, List<string>> LoadMonsterSummonItemList()
    {
        var returnList = new Dictionary<string, List<string>>();
        foreach (var entry in GetCsvRows<CsvItemMonsterSummonEntry>("Db/ItemMonsterSummonList.csv"))
        {
            if (!DataManager.MonsterCodeLookup.TryGetValue(entry.Monster, out var _))
            {
                ServerLogger.LogWarning($"ItemMonsterSummonList.csv references the monster {entry.Monster}, but that monster could not be found.");
                continue;
            }

            if (!returnList.ContainsKey(entry.Type))
                returnList.Add(entry.Type, new List<string>());

            for (var i = 0; i < entry.Chance; i++)
                returnList[entry.Type].Add(entry.Monster);
        }

        return returnList.AsReadOnly();
    }

    public ReadOnlyDictionary<string, List<int>> LoadItemBoxSummonList()
    {
        var returnList = new Dictionary<string, List<int>>();

        foreach (var entry in GetCsvRows<CsvItemBoxSummonEntry>("Db/ItemBoxSummonList.csv"))
        {
            if (!DataManager.ItemIdByName.TryGetValue(entry.Code, out var id))
            {
                ServerLogger.LogWarning($"ItemBoxSummonList.csv references the item {entry.Code}, but that item could not be found.");
                continue;
            }

            if (!returnList.ContainsKey(entry.Type))
                returnList.Add(entry.Type, new List<int>());

            for (var i = 0; i < entry.Chance; i++)
                returnList[entry.Type].Add(id);
        }

        return returnList.AsReadOnly();
    }

    public int[] LoadJobBonusTable()
    {
        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/JobStatBonuses.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);


        var entries = csv.GetRecords<dynamic>().ToList();

        Span<int> tempTable = stackalloc int[6];

        int maxJobs = DataManager.JobInfo.Count;

        var fullBonusTable = new int[maxJobs * 70 * 6]; //70 levels for maxJobs jobs with 6 stats each level

        foreach (var entry in entries)
        {
            tempTable.Clear();
            if (entry is IDictionary<string, object> obj)
            {
                var jobName = (string)obj["Job"];
                if (!DataManager.JobIdLookup.TryGetValue(jobName, out var jobId))
                {
                    ServerLogger.LogWarning($"Job {jobName} specified in JobStatBonuses.csv could not be found.");
                    continue;
                }

                for (var i = 1; i < 71; i++)
                {
                    var stat = (string)obj[i.ToString()];
                    switch (stat)
                    {
                        case "str": tempTable[0] += 1; break;
                        case "agi": tempTable[1] += 1; break;
                        case "dex": tempTable[2] += 1; break;
                        case "int": tempTable[3] += 1; break;
                        case "vit": tempTable[4] += 1; break;
                        case "luk": tempTable[5] += 1; break;
                        case "0": break;
                        default: throw new Exception($"Unexpected stat value {stat} when loading job {jobName} on JobStatBonuses.csv!");
                    }

                    var index = (jobId * 70 * 6) + (i - 1) * 6;
                    var target = new Span<int>(fullBonusTable, index, 6);
                    tempTable.CopyTo(target);
                }
            }
        }

        return fullBonusTable;
    }

    public int[] LoadRefineSuccessTable()
    {
        var table = new int[20 * 5]; //4 weapon levels + armor, 20 refine.
        using var inPath = new TemporaryFile(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/RefineSuccess.csv"));
        using var tr = new StreamReader(inPath.FilePath, Encoding.UTF8) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);
        var entries = csv.GetRecords<dynamic>();
        var offset = 0;
        foreach (var entry in entries)
        {
            if (entry is IDictionary<string, object> obj)
            {
                table[offset + 0] = int.Parse((string)obj["Level1"]);
                table[offset + 1] = int.Parse((string)obj["Level2"]);
                table[offset + 2] = int.Parse((string)obj["Level3"]);
                table[offset + 3] = int.Parse((string)obj["Level4"]);
                table[offset + 4] = int.Parse((string)obj["Armor"]);

                offset += 5;
            }
        }

        return table;
    }

    public ReadOnlyDictionary<string, int> GenerateItemIdByNameLookup()
    {
        var lookup = new Dictionary<string, int>();

        foreach (var item in DataManager.ItemList)
        {
            lookup.Add(item.Value.Code, item.Value.Id);
        }

        return lookup.AsReadOnly();
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

    public void LoadMonsterSkillAi(Assembly assembly)
    {
        DataManager.MonsterSkillAiHandlers = new Dictionary<string, MonsterSkillAiBase>();
        var itemType = typeof(IMonsterLoader);
        foreach (var type in assembly.GetTypes().Where(t => t.IsAssignableTo(itemType)))
        {
            var handler = (IMonsterLoader)Activator.CreateInstance(type)!;
            handler.Load();
        }

        foreach (var entry in DataManager.MonsterSkillAiHandlers)
        {
            if (!entry.Value.IsUnassignedAiType && !DataManager.MonsterCodeLookup.TryGetValue(entry.Key, out _))
                ServerLogger.LogWarning($"Ai skill handler exists for monster {entry.Key}, but a monster by that name does not exist.");
        }
    }

    public List<MonsterDatabaseInfo> LoadMonsterStats()
    {
        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/Monsters.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var monsters = csv.GetRecords<CsvMonsterData>().ToList();
        var tagLookup = new Dictionary<string, int>();
        var lastTag = 0;

        var obj = new List<MonsterDatabaseInfo>(monsters.Count);

        foreach (var monster in monsters)
        {
            if (monster.Id <= 0)
                continue;

            HashSet<int>? tags = null;
            var flags = MonsterSpecialFlags.None;
            if (!string.IsNullOrWhiteSpace(monster.Tags))
            {
                tags = new HashSet<int>();
                var s = monster.Tags.Split(',');
                foreach (var tag in s)
                {
                    if (tag == "Flying")
                        flags |= MonsterSpecialFlags.Flying;
                    if (tagLookup.TryGetValue(tag, out var tagId))
                        tags.Add(tagId);
                    else
                    {
                        tagLookup.Add(tag, lastTag);
                        tags.Add(lastTag);
                        lastTag++;
                    }
                }

            }

            obj.Add(new MonsterDatabaseInfo()
            {
                Id = monster.Id,
                Code = monster.Code,
                Level = monster.Level,
                HP = monster.HP,
                Exp = monster.Exp,
                JobExp = monster.JExp,
                Def = monster.Def,
                MDef = monster.MDef,
                Str = monster.Str,
                Agi = monster.Agi,
                Vit = monster.Vit,
                Int = monster.Int,
                Dex = monster.Dex,
                Luk = monster.Luk,
                Range = monster.Range > 0 ? monster.Range : 1,
                Size = monster.Size,
                ScanDist = monster.ScanDist,
                ChaseDist = monster.ChaseDist,
                AtkMin = monster.AtkMin,
                AtkMax = monster.AtkMax,
                AttackLockTime = monster.AttackTime / 1000f,
                HitTime = monster.HitTime / 1000f,
                RechargeTime = monster.RechargeTime / 1000f,
                MoveSpeed = monster.MoveSpeed / 1000f,
                AttackDamageTiming = monster.SpriteAttackTiming / 1000f,
                Element = monster.Element,
                Race = monster.Race,
                AiType = (MonsterAiType)Enum.Parse(typeof(MonsterAiType), monster.MonsterAi),
                Special = monster.Special,
                Name = monster.Name,
                Tags = tags,
                SpecialFlags = flags
            });
        }

        DataManager.TagToIdLookup = tagLookup.AsReadOnly();

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
            if (type == itemType)
                continue;
            var handler = (INpcLoader)Activator.CreateInstance(type)!;
            handler.Load();
        }
    }

    public ReadOnlyDictionary<string, Action<IServerMapConfig>> LoadMapConfigs(Assembly assembly)
    {
        var configs = new Dictionary<string, Action<IServerMapConfig>>();

        var attr = typeof(ServerMapConfigAttribute);
        var types = assembly.GetTypes().SelectMany(t => t.GetMethods())
            .Where(m => m.GetCustomAttributes(attr, false).Length > 0).ToArray();

        foreach (var type in types)
        {
            var action = (Action<IServerMapConfig>)type.CreateDelegate(typeof(Action<IServerMapConfig>));
            var at = type.GetCustomAttribute<ServerMapConfigAttribute>();

            if (at != null) configs.Add(at.MapName, action);
        }

        return configs.AsReadOnly();
    }

    public List<List<MonsterAiEntry>> LoadAiStateMachines()
    {
        var aiTypeCount = Enum.GetNames(typeof(MonsterAiType)).Length;
        var entryList = new List<List<MonsterAiEntry>>(aiTypeCount);
        for (var i = 0; i < aiTypeCount; i++)
            entryList.Add(new List<MonsterAiEntry>());

        //most of this nonsense is because I want comments in a csv file...
        var updatedCsv = new StringBuilder();
        foreach (var l in File.ReadAllLines(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/MonsterAI.csv"), Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(l) || l.Trim().StartsWith("//"))
                continue;
            updatedCsv.AppendLine(l);
        }

        var updated = updatedCsv.ToString();

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(updated));
        using var tr = new StreamReader(ms) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var states = csv.GetRecords<CsvMonsterAI>().ToList();

        foreach (var entry in states)
        {
            if (entry.AiType.Contains("//"))
                continue;

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
            targetMonster.Minions.Add(new MonsterSpawnMinions() { Count = entry.Count, Monster = minion, InitialGivesExp = entry.InitialGivesExp });
        }
    }

    public HashSet<int> LoadEmotes()
    {
        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/Emotes.csv"), Encoding.UTF8) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var data = new HashSet<int>();
        var emotes = csv.GetRecords<CsvEmote>().ToList();

        foreach (var emote in emotes)
        {
            data.Add(emote.Id);
        }

        return data;
    }

    public Dictionary<CharacterStatusEffect, StatusEffectData> GetStatusEffectData()
    {
        var path = Path.Combine(ServerConfig.DataConfig.DataPath, @"Skills/StatusEffects.toml");
        var options = new TomlModelOptions() { ConvertPropertyName = name => name, ConvertFieldName = name => name, IncludeFields = true };
        var statusData = Toml.ToModel<Dictionary<string, StatusEffectData>>(File.ReadAllText(path, Encoding.UTF8), null, options);
        var statusOut = new Dictionary<CharacterStatusEffect, StatusEffectData>();

        foreach (var data in statusData)
        {
            if (!Enum.TryParse<CharacterStatusEffect>(data.Key, out var status))
            {
                ServerLogger.LogWarning($"Could not match status effect name {data.Key} to an existing type.");
                continue;
            }

            statusOut.Add(status, data.Value);
        }

        return statusOut;
    }

    public ReadOnlyDictionary<int, int> CreateFlippedLookupTable(ReadOnlyDictionary<int, int> orig)
    {
        var dictOut = new Dictionary<int, int>();
        foreach (var (key, val) in orig)
        {
            dictOut.Add(val, key);
        }

        return dictOut.AsReadOnly();
    }
}