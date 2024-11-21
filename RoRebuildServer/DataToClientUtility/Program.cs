using System.Dynamic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CsvHelper;
using Dahomey.Json;
using Microsoft.Extensions.Options;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using RebuildSharedData.Util;
using RoRebuildServer.Data;
using RoRebuildServer.Data.CsvDataTypes;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Player;
using RoRebuildServer.EntityComponents.Monsters;
using Tomlyn;

namespace DataToClientUtility;

class Program
{
    private const string path = @"..\..\..\..\GameConfig\ServerData\Db\";
    private const string outPath = @"..\..\..\..\..\RebuildClient\Assets\Data\";
    private const string outPathStreaming = @"..\..\..\..\..\RebuildClient\Assets\StreamingAssets\";
    private const string configPath = @"..\..\..\..\..\RebuildServer\";

    static void Main(string[] args)
    {
        WriteMonsterData();
        //WriteServerConfig();
        WriteMapList();
        WriteEffectsList();
        WriteItemsList();
        WriteJobDataStuff();
    }

    private static void WriteEffectsList()
    {
        var inPath = Path.Combine(path, "Effects.csv");
        var tempPath = Path.Combine(Path.GetTempPath(), @"Effects.csv"); //copy in case file is locked
        File.Copy(inPath, tempPath, true);

        using (var tr = new StreamReader(tempPath, Encoding.UTF8) as TextReader)
        using (var csv = new CsvReader(tr, CultureInfo.InvariantCulture))
        {
            var entries = csv.GetRecords<CsvEffects>().ToList();

            var effectList = new EffectTypeList();
            effectList.Effects = new List<EffectTypeEntry>();

            foreach (var e in entries)
            {
                effectList.Effects.Add(new EffectTypeEntry()
                {
                    Id = e.Id,
                    Name = e.Name,
                    ImportEffect = e.ImportEffect,
                    Billboard = e.Billboard,
                    StrFile = e.StrFile,
                    Sprite = e.Sprite,
                    SoundFile = e.SoundFile,
                    Offset = e.Offset,
                    PrefabName = e.PrefabName,
                    IsLooping = e.Flags?.Contains("Loop") ?? false,
                });
            }

            JsonSerializerOptions options = new JsonSerializerOptions();
            options.SetupExtensions();
            options.WriteIndented = true;

            var json = JsonSerializer.Serialize(effectList, options);

            var effectDir = Path.Combine(outPath, "effects.json");

            File.WriteAllText(effectDir, json);
        }

        File.Delete(tempPath);
    }

    private static List<T> GetCsvRows<T>(string fileName)
    {
        var inPath = new TemporaryFile(Path.Combine(path, fileName));
        using var tr = new StreamReader(inPath.FilePath, Encoding.UTF8) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);
        
        return csv.GetRecords<T>().ToList();
    }

    private static void WriteItemsList()
    {
        var itemList = new ItemDataList();
        itemList.Items = new List<ItemData>();
        var prefixList = new CardPrefixDataList();
        prefixList.Items = new List<CardPrefixData>();

        var displaySpriteList = new StringBuilder();

        foreach (var entry in GetCsvRows<CsvItemUseable>("ItemsUsable.csv"))
        {
            var item = new ItemData()
            {
                Code = entry.Code,
                Name = entry.Name,
                Id = entry.Id,
                IsUnique = false,
                ItemClass = ItemClass.Useable,
                UseType = entry.UseMode,
                Price = entry.Price,
                Weight = entry.Weight,
                Sprite = entry.Sprite,
            };
            itemList.Items.Add(item);
        }

        foreach (var entry in GetCsvRows<CsvItemRegular>("ItemsRegular.csv"))
        {
            var item = new ItemData()
            {
                Code = entry.Code,
                Name = entry.Name,
                Id = entry.Id,
                IsUnique = false,
                ItemClass = ItemClass.Etc,
                UseType = ItemUseType.NotUsable,
                Price = entry.Price,
                Weight = entry.Weight,
                Sprite = entry.Sprite,
            };
            itemList.Items.Add(item);
        }

        foreach (var entry in GetCsvRows<CsvItemWeapon>("ItemsWeapons.csv"))
        {
            var item = new ItemData()
            {
                Code = entry.Code,
                Name = entry.Name,
                Id = entry.Id,
                IsUnique = true,
                ItemClass = ItemClass.Weapon,
                UseType = ItemUseType.NotUsable,
                Slots = entry.Slot,
                Price = entry.Price,
                Weight = entry.Weight,
                Sprite = entry.Sprite,
                Position = entry.Position == WeaponPosition.MainHand ? EquipPosition.MainHand : EquipPosition.BothHands
            };
            itemList.Items.Add(item);

            if (!string.IsNullOrWhiteSpace(entry.WeaponSprite)) 
                displaySpriteList.AppendLine($"{entry.Code}\t{entry.WeaponSprite}");
        }
        
        foreach (var entry in GetCsvRows<CsvItemEquipment>("ItemsEquipment.csv"))
        {
            var pos = (entry.Type & ~EquipPosition.Headgear) | (EquipPosition) entry.Position;
            
            var item = new ItemData()
            {
                Code = entry.Code,
                Name = entry.Name,
                Id = entry.Id,
                IsUnique = true,
                ItemClass = ItemClass.Equipment,
                UseType = ItemUseType.NotUsable,
                Slots = entry.Slot,
                Price = entry.Price,
                Weight = entry.Weight,
                Sprite = entry.Sprite,
                Position = pos
            };
            itemList.Items.Add(item);

            if (!string.IsNullOrWhiteSpace(entry.DisplaySprite))
                displaySpriteList.AppendLine($"{entry.Code}\t{entry.DisplaySprite.ToLowerInvariant()}");
        }

        foreach (var entry in GetCsvRows<CsvItemCard>("ItemsCards.csv"))
        {
            var item = new ItemData()
            {
                Code = entry.Code,
                Name = entry.Name,
                Id = entry.Id,
                IsUnique = false,
                ItemClass = ItemClass.Card,
                UseType = ItemUseType.NotUsable,
                Price = entry.Price,
                Weight = entry.Weight,
                Sprite = entry.Sprite,
                Position = entry.EquipableSlot
            };
            itemList.Items.Add(item);
            prefixList.Items.Add(new CardPrefixData() {Id = entry.Id, Prefix = entry.Prefix, Postfix = entry.Postfix});
        }

        foreach (var entry in GetCsvRows<CsvItemAmmo>("ItemsAmmo.csv"))
        {
            var item = new ItemData()
            {
                Code = entry.Code,
                Name = entry.Name,
                Id = entry.Id,
                IsUnique = false,
                ItemClass = ItemClass.Ammo,
                UseType = ItemUseType.NotUsable,
                Price = entry.Price,
                Weight = entry.Weight,
                Sprite = entry.Sprite,
            };
            itemList.Items.Add(item);
        }

        JsonSerializerOptions options = new JsonSerializerOptions();
        options.SetupExtensions();
        options.WriteIndented = true;

        var json = JsonSerializer.Serialize(itemList, options);
        var itemDir = Path.Combine(outPath, "items.json");


        var cardJson = JsonSerializer.Serialize(prefixList, options);
        var cardDir = Path.Combine(outPath, "cardprefixes.json");

        File.WriteAllText(itemDir, json);
        File.WriteAllText(cardDir, cardJson);
        File.WriteAllText(Path.Combine(outPath, "displaySpriteTable.txt"), displaySpriteList.ToString());
    }

    private static void WriteServerConfig()
    {
        var inPath = Path.Combine(path, "ServerSettings.csv");
        var tempPath = Path.Combine(Path.GetTempPath(), @"ServerSettings.csv"); //copy in case file is locked
        File.Copy(inPath, tempPath);

        using (var tr = new StreamReader(tempPath, Encoding.UTF8) as TextReader)
        using (var csv = new CsvReader(tr, CultureInfo.InvariantCulture))
        {

            var entries = csv.GetRecords<CsvServerConfig>().ToList();

            //var ip = entries.FirstOrDefault(e => e.Key == "IP").Value;
            //var port = entries.FirstOrDefault(e => e.Key == "Port").Value;
            var url = entries.First(e => e.Key == "Url").Value;

            var configPath = Path.Combine(outPathStreaming, "serverconfig.txt");

            File.WriteAllText(configPath, $"{url}");
        }

        File.Delete(tempPath);
    }

    private static void WriteExpChart()
    {
        using var tr = new StreamReader(Path.Combine(path, "ExpChart.csv"), Encoding.UTF8) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var entries = csv.GetRecords<CsvExpChart>().ToList();

        var lines = new List<string>();
        foreach (var entry in entries)
            lines.Add(entry.Experience.ToString());

        File.WriteAllLines(Path.Combine(outPath, "levelchart.txt"), lines);
    }

    private static List<string> GetActiveInstanceMaps()
    {
        using var tr = new StreamReader(Path.Combine(path, "Instances.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        var mapCodes = new List<string>();

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

            foreach (var m in instance.Maps)
                if (!mapCodes.Contains(m))
                    mapCodes.Add(m);
        }

        return mapCodes;
    }

    private static void WriteMapList()
    {
        var inUseMaps = GetActiveInstanceMaps();

        ConvertToClient<MapEntry, ClientMapEntry>("Maps.csv", "maps.json", convert =>
            {
                var mapOut = new List<MapEntry>();
                foreach (var m in convert)
                    if (inUseMaps.Contains(m.Code))
                        mapOut.Add(m);

                return mapOut.Select(e => new ClientMapEntry()
                {
                    Code = e.Code,
                    Name = e.Name,
                    MapMode = (int)Enum.Parse<MapType>(e.MapMode),
                    Music = e.Music
                }).ToList();
            }
        );
    }

    private static List<MapEntry> GetMapList()
    {
        using var tempPath = new TempFileCopy(Path.Combine(path, "Maps.csv"));
        using var tr = new StreamReader(tempPath.Path, Encoding.UTF8) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        return csv.GetRecords<MapEntry>().ToList();
    }

    //private static List<CsvMapSpawnEntry> GetSpawnEntries()
    //{
    //	var inPath = Path.Combine(path, "MapSpawns.csv");
    //	var tempPath = Path.Combine(Path.GetTempPath(), "MapSpawns.csv"); //copy in case file is locked
    //	File.Copy(inPath, tempPath, true);

    //	List<CsvMapSpawnEntry> monsters;

    //	using (var tr = new StreamReader(tempPath) as TextReader)
    //	using (var csv = new CsvReader(tr, CultureInfo.InvariantCulture))
    //	{
    //		monsters = csv.GetRecords<CsvMapSpawnEntry>().ToList();
    //	}

    //	File.Delete(tempPath);

    //	return monsters;
    //}

    private static void LoadMonsterData(List<MonsterClassData> monsterData)
    {
        var inPath = Path.Combine(path, "Monsters.csv");
        var tempPath = Path.Combine(Path.GetTempPath(), "Monsters.csv"); //copy in case file is locked
        File.Copy(inPath, tempPath, true);

        //var monSpawns = GetSpawnEntries();
        var maps = GetMapList();

        //monSpawns = monSpawns.Where(m => maps.Any(m2 => m2.Code == m.Map)).ToList();

        using (var tr = new StreamReader(tempPath, Encoding.UTF8) as TextReader)
        using (var csv = new CsvReader(tr, CultureInfo.InvariantCulture))
        {
            var monsters = csv.GetRecords<CsvMonsterData>().ToList();

            foreach (var monster in monsters)
            {
                //if (monster.Id >= 4000 && monSpawns.All(m => m.Class != monster.Code))
                //	continue;

                var mc = new MonsterClassData()
                {
                    Id = monster.Id,
                    Name = monster.Name,
                    Code = monster.Code,
                    SpriteName = monster.ClientSprite,
                    Offset = monster.ClientOffset,
                    ShadowSize = monster.ClientShadow,
                    Size = monster.ClientSize,
                    AttackTiming = monster.SpriteAttackTiming / 1000f,
                    Color = monster.Color
                };

                monsterData.Add(mc);
            }
        }

        File.Delete(tempPath);
    }

    private static void LoadNpcData(List<MonsterClassData> monsterData)
    {
        var inPath = Path.Combine(path, "Npcs.csv");
        var tempPath = Path.Combine(Path.GetTempPath(), "Npcs.csv"); //copy in case file is locked
        File.Copy(inPath, tempPath, true);

        using (var tr = new StreamReader(tempPath, Encoding.UTF8) as TextReader)
        using (var csv = new CsvReader(tr, CultureInfo.InvariantCulture))
        {
            var npcs = csv.GetRecords<CsvNpc>().ToList();

            foreach (var npc in npcs)
            {
                //if (monster.Id >= 4000 && monSpawns.All(m => m.Class != monster.Code))
                //	continue;

                var mc = new MonsterClassData()
                {
                    Id = npc.Id,
                    Name = npc.Name,
                    Code = npc.Code,
                    SpriteName = npc.ClientSprite,
                    Offset = npc.ClientOffset,
                    ShadowSize = npc.ClientShadow,
                    Size = npc.ClientSize
                };

                monsterData.Add(mc);
            }
        }

        File.Delete(tempPath);
    }

    private static void WriteMonsterData()
    {
        var mData = new List<MonsterClassData>();
        LoadMonsterData(mData);
        LoadNpcData(mData);
        WriteExpChart();

        SaveToClient("monsterclass.json", mData);

        //var dbTable = new DatabaseMonsterClassData();
        //dbTable.MonsterClassData = mData;

        //JsonSerializerOptions options = new JsonSerializerOptions();
        //options.SetupExtensions();
        //options.WriteIndented = true;

        //var json = JsonSerializer.Serialize(dbTable, options);

        //var monsterDir = Path.Combine(outPath, "monsterclass.json");

        //File.WriteAllText(monsterDir, json);

    }

    private static List<TDst> ConvertToClient<TSrc, TDst>(string csvName, string jsonName, Func<List<TSrc>, List<TDst>> convert)
    {
        var inPath = Path.Combine(path, csvName);
        var tempPath = Path.Combine(Path.GetTempPath(), csvName); //copy in case file is locked
        File.Copy(inPath, tempPath, true);

        using var tr = new StreamReader(tempPath, Encoding.UTF8) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);
        var jobs = csv.GetRecords<TSrc>().ToList();

        var list = convert(jobs);

        dynamic dataObj = new ExpandoObject();
        ((IDictionary<string, Object>)dataObj).Add("Items", list);

        JsonSerializerOptions options = new JsonSerializerOptions();
        options.SetupExtensions();
        options.WriteIndented = true;

        var json = JsonSerializer.Serialize(dataObj, options);
        var targetDir = Path.Combine(outPath, jsonName);

        File.WriteAllText(targetDir, json);
        Console.WriteLine($"Writing data to {targetDir}");

        return list;
    }

    private static void SaveToClient<T>(string fileName, List<T> list)
    {
        dynamic dataObj = new ExpandoObject();
        ((IDictionary<string, Object>)dataObj).Add("Items", list);

        JsonSerializerOptions options = new JsonSerializerOptions();
        options.SetupExtensions();
        options.WriteIndented = true;

        var json = JsonSerializer.Serialize(dataObj, options);
        var targetDir = Path.Combine(outPath, fileName);

        File.WriteAllText(targetDir, json);
        Console.WriteLine($"Writing data to {targetDir}");
    }

    private static void WriteJobDataStuff()
    {
        //weapon class
        var classes = ConvertToClient<CsvWeaponClass, PlayerWeaponClass>("WeaponClass.csv", "weaponclass.json",
            weapons => weapons.Select(w => new PlayerWeaponClass()
            {
                Id = w.Id,
                Name = w.FullName,
                WeaponClass = w.WeaponClass,
                HitSounds = w.HitSound.Split('/').Select(a => a + ".ogg").ToList()
            }).ToList()
        );

        //job list
        var jobs = ConvertToClient<CsvJobs, PlayerClassData>("Jobs.csv", "playerclass.json",
            jobs => jobs.Select(j => new PlayerClassData() { Id = j.Id, Name = j.Class, SpriteFemale = j.SpriteFemale, SpriteMale = j.SpriteMale }).ToList()
            );


        PlayerWeaponData CsvWeaponDataToClient(CsvJobWeaponInfo w) => new()
        {
            Job = jobs.First(j => j.Name == w.Job).Id,
            Class = classes.First(c => c.WeaponClass == w.Class).Id,
            AttackMale = w.AttackMale,
            AttackFemale = w.AttackFemale,
            SpriteFemale = string.IsNullOrWhiteSpace(w.SpriteFemale) ? string.Empty : $"Assets/Sprites/Weapons/{w.Job}/Female/" + w.SpriteFemale,
            SpriteMale = string.IsNullOrWhiteSpace(w.SpriteMale) ? string.Empty : $"Assets/Sprites/Weapons/{w.Job}/Male/" + w.SpriteMale,
            EffectMale = string.IsNullOrWhiteSpace(w.EffectMale) ? string.Empty : $"Assets/Sprites/Weapons/{w.Job}/Male/" + w.EffectMale,
            EffectFemale = string.IsNullOrWhiteSpace(w.EffectFemale) ? string.Empty : $"Assets/Sprites/Weapons/{w.Job}/Female/" + w.EffectFemale
        };

        //skill descriptions

        var lines = File.ReadAllLines(Path.Combine(path, "../Skills/SkillDescriptions.txt"));
        var sb = new StringBuilder();
        var curSkill = CharacterSkill.None;
        var skillDesc = new Dictionary<CharacterSkill, string>();
        foreach (var line in lines)
        {
            if (line.StartsWith("//"))
                continue;
            if (line.StartsWith("::"))
            {
                if (!Enum.TryParse<CharacterSkill>(line.Substring(2), true, out var type))
                    throw new Exception($"Could not parse skill {line} in SkillDescriptions.txt");
                if (curSkill != CharacterSkill.None && sb.Length > 0)
                    skillDesc.Add(curSkill, sb.ToString().Trim());
                curSkill = type;
                sb.Clear();
                continue;
            }

            sb.AppendLine(line);
        }
        if (curSkill != CharacterSkill.None && sb.Length > 0)
            skillDesc.Add(curSkill, sb.ToString().Trim());

        //skill data
        var options = new TomlModelOptions() { ConvertPropertyName = name => name, ConvertFieldName = name => name, IncludeFields = true };
        var skillData = Toml.ToModel<Dictionary<string, SkillData>>(File.ReadAllText(Path.Combine(path, "../Skills/Skills.toml"), Encoding.UTF8), null, options);
        var skillOut = new List<SkillData>();
        foreach (var (id, skill) in skillData)
        {
            skill.SkillId = Enum.Parse<CharacterSkill>(id);
            if (skill.Name == null) skill.Name = id;
            if (skill.Icon == null) skill.Icon = "nv_basic";
            if (skillDesc.TryGetValue(skill.SkillId, out var desc))
                skill.Description = desc;
            skillOut.Add(skill);
        }
        
        SaveToClient("skillinfo.json", skillOut);

        //skill tree
        var skillTreeData = Toml.ToModel<Dictionary<string, PlayerSkillTree>>(File.ReadAllText(Path.Combine(path, "../Skills/SkillTree.toml"), Encoding.UTF8), null, options);
        var skillTreeOut = new List<ClientSkillTree>();

        foreach (var (id, tree) in skillTreeData)
        {
            var job = jobs.FirstOrDefault(j => id == j.Name);
            if (job == null || tree == null) throw new Exception($"SkillTree.toml could not identify job by name {id}");

            int extends = -1;
            if (tree.Extends != null)
            {
                var extendJobClass = jobs.FirstOrDefault(j => j.Name == tree.Extends);
                if (job == null || extendJobClass == null) throw new Exception($"SkillTree.toml could not identify extension job by name {id}");
                extends = extendJobClass.Id;
            }

            var entry = new ClientSkillTree()
            {
                ClassId = job.Id,
                ExtendsClass = extends,
                JobRank = tree.JobRank,
                Skills = new List<ClientSkillTreeEntry>()
            };

            if (tree.SkillTree != null)
                foreach (var skills in tree.SkillTree)
                {
                    var skill = new ClientSkillTreeEntry()
                    {
                        Skill = skillData[skills.Key.ToString()].SkillId,
                        Prerequisites = new ClientPrereq[skills.Value!.Count]
                    };

                    for (var i = 0; i < skills.Value.Count; i++)
                    {
                        var prereq = skills.Value[i];
                        skill.Prerequisites[i] = new ClientPrereq()
                        {
                            Skill = prereq.Skill,
                            Level = prereq.RequiredLevel
                        };
                    }

                    entry.Skills.Add(skill);
                }

            skillTreeOut.Add(entry);
        }
        SaveToClient("skilltree.json", skillTreeOut);

        //job weapon info
        ConvertToClient<CsvJobWeaponInfo, PlayerWeaponData>("JobWeaponInfo.csv", "jobweaponinfo.json",
            wi =>
            {
                var data = new List<PlayerWeaponData>();
                var combos = new List<(int, int)>();
                foreach (var w in wi)
                {
                    var wd = CsvWeaponDataToClient(w);
                    data.Add(wd);
                    combos.Add((wd.Job, wd.Class));
                }

                for (var j = 0; j < jobs.Count; j++)
                {
                    for (var i = 0; i < classes.Count; i++)
                    {
                        if (!combos.Contains((jobs[j].Id, classes[i].Id)))
                        {
                            //try to use the unarmed animation for the job, and barring that fall back to unarmed animation for novice
                            var defaultForClass = data.FirstOrDefault(w => w.Class == 0 && w.Job == j);
                            if (defaultForClass != null)
                            {
                                var wd = defaultForClass.Clone();
                                wd.Job = j;
                                wd.Class = i;
                                data.Add(wd);
                                combos.Add((wd.Job, wd.Class));
                            }
                            else
                            {
                                var wd = data.First(w => w.Class == 0 && w.Job == 0).Clone();
                                wd.Job = j;
                                wd.Class = i;
                                data.Add(wd);
                                combos.Add((wd.Job, wd.Class));
                            }
                        }
                    }
                }
                return data;
            });
    }
}