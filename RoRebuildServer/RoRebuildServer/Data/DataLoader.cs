using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Text;
using Antlr4.Runtime.Tree.Xpath;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Extensions;
using RoRebuildServer.Data.Config;
using RoRebuildServer.Data.CsvDataTypes;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Data.Player;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.EntityComponents.Monsters;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.ScriptSystem;
using RoRebuildServer.Simulation;
using Tomlyn;

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
            mvps.Add(line);
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
        var skillTreeData = Toml.ToModel<Dictionary<string, PlayerSkillTree>>(File.ReadAllText(path, Encoding.UTF8), null, options);
        var extendList = new Dictionary<int, int>();
        var treeOut = new Dictionary<int, PlayerSkillTree>();

        foreach (var (id, tree) in skillTreeData)
        {
            treeOut.Add(DataManager.JobIdLookup[id], tree);
            if (tree.Extends != null)
                extendList.Add(DataManager.JobIdLookup[id], DataManager.JobIdLookup[tree.Extends]); //save this so we can chase skill requirements from previous jobs
        }

        DataManager.JobExtendsList = extendList.AsReadOnly();

        return treeOut.AsReadOnly();
    }

    private new Dictionary<(int, int), (int, int)> LoadRemapDrops()
    {
        var remap = new Dictionary<(int, int), (int, int)>();
        var remapFile = new Dictionary<string, MonsterDropData>();
        var remapPath = Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/DropRateRemapping.csv");

        using var tr = new StreamReader(remapPath, Encoding.UTF8) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var entries = csv.GetRecords<dynamic>();
        foreach (var entry in entries)
        {
            if (entry is IDictionary<string, object> obj)
            {
                var origMin = int.Parse((string)obj["OriginalMin"]);
                var origMax = int.Parse((string)obj["OriginalMax"]);
                var afterMin = int.Parse((string)obj["AfterMin"]);
                var afterMax = int.Parse((string)obj["AfterMax"]);
                remap.Add((origMin, origMax), (afterMin, afterMax));
            }
        }

        return remap;
    }

    public ReadOnlyDictionary<string, MonsterDropData> LoadMonsterDropChanceData()
    {
        var remap = new Dictionary<(int, int), (int, int)>();

        var remapDrops = ServerConfig.OperationConfig.RemapDropRates;

        if (remapDrops)
            remap = LoadRemapDrops();

        var drops = new Dictionary<string, MonsterDropData>();
        using var inPath = new TemporaryFile(Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/DropData.csv"));
        using var tr = new StreamReader(inPath.FilePath, Encoding.UTF8) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);
        var entries = csv.GetRecords<dynamic>();
        foreach (var entry in entries)
        {
            if (entry is IDictionary<string, object> obj)
            {
                var monster = ((string)obj["Monster"]).Replace(" ", "_").ToUpper();
                var data = new MonsterDropData();

                if (!DataManager.MonsterCodeLookup.ContainsKey(monster))
                    ServerLogger.LogWarning($"Item drops defined for monster {monster} but that monster could not be found.");

                for (var i = 1; i <= 12; i++)
                {
                    var key = $"Item{i}";
                    if (obj.ContainsKey(key))
                    {
                        var itemName = (string)obj[key];
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

                        var chance = (int)int.Parse((string)obj[$"Chance{i}"]);
                        if (chance <= 0)
                            continue;

                        if (remapDrops)
                        {
                            foreach (var range in remap)
                            {
                                if (chance >= range.Key.Item1 && chance < range.Key.Item2)
                                {
                                    chance = (int)((float)chance).Remap(range.Key.Item1, range.Key.Item2, range.Value.Item1, range.Value.Item2);
                                    break;
                                }
                            }
                        }

                        if (item > 0) //for debug reasons mostly
                            data.DropChances.Add(new MonsterDropData.MonsterDropEntry(item, chance, rangeMin, rangeMax));
                    }
                }

                drops.Add(monster, data);
            }
        }

        return drops.AsReadOnly();
    }

    public ReadOnlyDictionary<int, int[]> LoadMaxHpChart()
    {
        var dict = new Dictionary<int, int[]>();

        var inPath = Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/JobHpChart.csv");

        using var tr = new StreamReader(inPath, Encoding.UTF8) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);
        var entries = csv.GetRecords<CsvJobMaxHp>().ToList();

        for (var i = 0; i < 7; i++)
            dict.Add(i, new int[100]);

        foreach (var entry in entries)
        {
            var lvl = entry.Level;
            dict[0][lvl] = entry.Novice;
            dict[1][lvl] = entry.Swordsman;
            dict[2][lvl] = entry.Archer;
            dict[3][lvl] = entry.Mage;
            dict[4][lvl] = entry.Acolyte;
            dict[6][lvl] = entry.Merchant;
            dict[5][lvl] = entry.Thief;
        }

        return dict.AsReadOnly();
    }


    public ReadOnlyDictionary<int, int[]> LoadMaxSpChart()
    {
        var dict = new Dictionary<int, int[]>();

        var inPath = Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/JobSpChart.csv");

        using var tr = new StreamReader(inPath, Encoding.UTF8) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);
        var entries = csv.GetRecords<CsvJobMaxHp>().ToList();

        for (var i = 0; i < 7; i++)
            dict.Add(i, new int[100]);

        foreach (var entry in entries)
        {
            var lvl = entry.Level;
            dict[0][lvl] = entry.Novice;
            dict[1][lvl] = entry.Swordsman;
            dict[2][lvl] = entry.Archer;
            dict[3][lvl] = entry.Mage;
            dict[4][lvl] = entry.Acolyte;
            dict[6][lvl] = entry.Merchant;
            dict[5][lvl] = entry.Thief;
        }

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
                Id = entry.Id,
                IsUnique = false,
                ItemClass = ItemClass.Useable,
                Price = entry.Price,
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
                Id = entry.Id,
                IsUnique = true,
                ItemClass = ItemClass.Equipment,
                Price = entry.Price,
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
                Id = entry.Id,
                IsUnique = true,
                ItemClass = ItemClass.Weapon,
                Price = entry.Price,
                Weight = entry.Weight,
            };
            itemList.Add(item.Id, item);

            if (!DataManager.WeaponClasses.TryGetValue(entry.Type, out var weaponType))
                weaponType = 0;

            var weaponInfo = new WeaponInfo()
            {
                Attack = entry.Attack,
                CardSlots = entry.Slot,
                Element = entry.Property,
                WeaponClass = weaponType,
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
                Id = entry.Id,
                IsUnique = false,
                ItemClass = ItemClass.Useable,
                Price = entry.Price,
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
                Id = entry.Id,
                IsUnique = false,
                ItemClass = ItemClass.Useable,
                Price = entry.Price,
                Weight = entry.Weight,
            };
            itemList.Add(item.Id, item);

            var ammoInfo = new AmmoInfo() { Type = entry.Type, Attack = entry.Attack, MinLvl = entry.EquipLevel};
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
                Id = entry.Id,
                IsUnique = false,
                ItemClass = ItemClass.Useable,
                Price = entry.Price,
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

            if(!returnList.ContainsKey(entry.Type))
                returnList.Add(entry.Type, new List<string>());

            for(var i = 0; i < entry.Chance; i++)
                returnList[entry.Type].Add(entry.Monster);
        }

        return returnList.AsReadOnly();
    }

    //    public Dictionary<int, ItemInfo> LoadItemList()
    //    {
    //        var items = new Dictionary<int, ItemInfo>();

    //        var inPath = Path.Combine(ServerConfig.DataConfig.DataPath, @"Db/Items.csv");

    //#if DEBUG
    //        //if the file is open in excel, we can't read it... so while in debug build we'll make a copy
    //        var tempPath = Path.Combine(Path.GetTempPath(), @"Items.csv");
    //        File.Copy(inPath, tempPath, true);
    //        inPath = tempPath;
    //#endif

    //        using (var tr = new StreamReader(inPath, Encoding.UTF8) as TextReader)
    //        using (var csv = new CsvReader(tr, CultureInfo.InvariantCulture))
    //        {
    //            var entries = csv.GetRecords<CsvItem>().ToList();

    //            foreach (var entry in entries)
    //            {
    //                var itemClass = entry.ItemClass;
    //                var item = new ItemInfo()
    //                {
    //                    Code = entry.Code,
    //                    Id = entry.Id,
    //                    IsUnique = itemClass == ItemClass.Equipment || itemClass == ItemClass.Weapon,
    //                    IsUseable = itemClass == ItemClass.Useable,
    //                    ItemClass = itemClass,
    //                    Price = entry.Price,
    //                    Weight = entry.Weight,
    //                    Effect = -1,
    //                };

    //                if (!string.IsNullOrWhiteSpace(entry.Effect))
    //                {
    //                    if (DataManager.EffectIdForName.TryGetValue(entry.Effect, out var effectId))
    //                        item.Effect = effectId;
    //                    else
    //                        ServerLogger.LogWarning($"Could not find effect '{entry.Effect}' with name '{item.Code}'.");
    //                }

    //                items.Add(item.Id, item);
    //            }


    //        }
    //#if DEBUG
    //        File.Delete(tempPath);
    //#endif
    //        return items;
    //    }

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
                MDef = monster.MDef,
                Str = monster.Str,
                Agi = monster.Agi,
                Vit = monster.Vit,
                Int = monster.Int,
                Dex = monster.Dex,
                Luk = monster.Luk,
                Range = monster.Range > 0 ? monster.Range : 1,
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
            if (type == itemType)
                continue;
            var handler = (INpcLoader)Activator.CreateInstance(type)!;
            handler.Load();
        }
    }

    public ReadOnlyDictionary<string, Action<ServerMapConfig>> LoadMapConfigs(Assembly assembly)
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
            targetMonster.Minions.Add(new MonsterSpawnMinions() { Count = entry.Count, Monster = minion });
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