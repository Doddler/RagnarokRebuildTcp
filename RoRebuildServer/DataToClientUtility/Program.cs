using System.Dynamic;
using System.Globalization;
using System.Text.Json;
using CsvHelper;
using Dahomey.Json;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using RebuildSharedData.Util;
using RoRebuildServer.Data;
using RoRebuildServer.Data.CsvDataTypes;
using RoRebuildServer.Data.Map;

namespace DataToClientUtility;

class Program
{
    private const string path = @"..\..\..\..\RoRebuildServer\ServerData\Db\";
    private const string outPath = @"..\..\..\..\..\RebuildClient\Assets\Data\";
    private const string outPathStreaming = @"..\..\..\..\..\RebuildClient\Assets\StreamingAssets\";
    private const string configPath = @"..\..\..\..\..\RebuildServer\";

    static void Main(string[] args)
    {
        WriteMonsterData();
        //WriteServerConfig();
        WriteMapList();
        WriteEffectsList();
        WriteJobDataStuff();
    }

    private static void WriteEffectsList()
    {
        var inPath = Path.Combine(path, "Effects.csv");
        var tempPath = Path.Combine(Path.GetTempPath(), @"Effects.csv"); //copy in case file is locked
        File.Copy(inPath, tempPath, true);

        using (var tr = new StreamReader(tempPath) as TextReader)
        using (var csv = new CsvReader(tr, CultureInfo.CurrentCulture))
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
                    SoundFile = e.SoundFile,
                    Offset = e.Offset,
                    PrefabName = e.PrefabName
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


    private static void WriteServerConfig()
    {
        //Console.WriteLine(AppDomain.CurrentDomain.BaseDirectory);

        //ServerConfig.LoadConfigFromPath(configPath);

        //var config = ServerConfig.OperationConfig;

        var inPath = Path.Combine(path, "ServerSettings.csv");
        var tempPath = Path.Combine(Path.GetTempPath(), @"ServerSettings.csv"); //copy in case file is locked
        File.Copy(inPath, tempPath);

        using (var tr = new StreamReader(tempPath) as TextReader)
        using (var csv = new CsvReader(tr, CultureInfo.CurrentCulture))
        {

            var entries = csv.GetRecords<CsvServerConfig>().ToList();

            //var ip = entries.FirstOrDefault(e => e.Key == "IP").Value;
            //var port = entries.FirstOrDefault(e => e.Key == "Port").Value;
            var url = entries.FirstOrDefault(e => e.Key == "Url").Value;

            var configPath = Path.Combine(outPathStreaming, "serverconfig.txt");

            File.WriteAllText(configPath, $"{url}");
        }

        File.Delete(tempPath);
    }

    private static void WriteExpChart()
    {
        using var tr = new StreamReader(Path.Combine(path, "ExpChart.csv")) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.CurrentCulture);

        var entries = csv.GetRecords<CsvExpChart>().ToList();

        var lines = new List<string>();
        foreach (var entry in entries)
            lines.Add(entry.Experience.ToString());

        File.WriteAllLines(Path.Combine(outPath, "levelchart.txt"), lines);
    }

    private static void WriteMapList()
    {
        ConvertToClient<MapEntry, ClientMapEntry>("Maps.csv", "maps.json", convert =>
            convert.Select(e => new ClientMapEntry()
            {
                Code = e.Code,
                Name = e.Name,
                MapMode = (int)Enum.Parse<MapMinimapType>(e.MapMode),
                Music = e.Music
            }).ToList()
        );

        //using var tempPath = new TempFileCopy(Path.Combine(path, "Maps.csv"));
        //using var tr = new StreamReader(tempPath.Path) as TextReader;
        //using var csv = new CsvReader(tr, CultureInfo.CurrentCulture);

        //var entries = csv.GetRecords<MapEntry>().ToList();
        //var mapList = new ClientMapList();
        //mapList.MapEntries = new List<ClientMapEntry>();

        //foreach (var e in entries)
        //{
        //    var mode = Enum.Parse<MapMinimapType>(e.MapMode);
        //    mapList.MapEntries.Add(new ClientMapEntry()
        //    {
        //        Code = e.Code,
        //        Name = e.Name,
        //        MapMode = (int)mode,
        //        Music = e.Music
        //    });
        //}

        //JsonSerializerOptions options = new JsonSerializerOptions();
        //options.SetupExtensions();
        //options.WriteIndented = true;

        //var json = JsonSerializer.Serialize(mapList, options);

        //var mapDir = Path.Combine(outPath, "maps.json");

        //File.WriteAllText(mapDir, json);
    }

    private static List<MapEntry> GetMapList()
    {
        using var tempPath = new TempFileCopy(Path.Combine(path, "Maps.csv"));
        using var tr = new StreamReader(tempPath.Path) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.CurrentCulture);

        return csv.GetRecords<MapEntry>().ToList();
    }

    //private static List<CsvMapSpawnEntry> GetSpawnEntries()
    //{
    //	var inPath = Path.Combine(path, "MapSpawns.csv");
    //	var tempPath = Path.Combine(Path.GetTempPath(), "MapSpawns.csv"); //copy in case file is locked
    //	File.Copy(inPath, tempPath, true);

    //	List<CsvMapSpawnEntry> monsters;

    //	using (var tr = new StreamReader(tempPath) as TextReader)
    //	using (var csv = new CsvReader(tr, CultureInfo.CurrentCulture))
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

        using (var tr = new StreamReader(tempPath) as TextReader)
        using (var csv = new CsvReader(tr, CultureInfo.CurrentCulture))
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
                    SpriteName = monster.ClientSprite,
                    Offset = monster.ClientOffset,
                    ShadowSize = monster.ClientShadow,
                    Size = monster.ClientSize
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

        using (var tr = new StreamReader(tempPath) as TextReader)
        using (var csv = new CsvReader(tr, CultureInfo.CurrentCulture))
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

        var dbTable = new DatabaseMonsterClassData();
        dbTable.MonsterClassData = mData;

        JsonSerializerOptions options = new JsonSerializerOptions();
        options.SetupExtensions();
        options.WriteIndented = true;

        var json = JsonSerializer.Serialize(dbTable, options);

        var monsterDir = Path.Combine(outPath, "monsterclass.json");

        File.WriteAllText(monsterDir, json);

    }

    private static List<TDst> ConvertToClient<TSrc, TDst>(string csvName, string jsonName, Func<List<TSrc>, List<TDst>> convert)
    {
        var inPath = Path.Combine(path, csvName);
        var tempPath = Path.Combine(Path.GetTempPath(), csvName); //copy in case file is locked
        File.Copy(inPath, tempPath, true);

        using var tr = new StreamReader(tempPath) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.CurrentCulture);
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

        return list;
    }

    private static void WriteJobDataStuff()
    {
        var classes = ConvertToClient<CsvWeaponClass, PlayerWeaponClass>("WeaponClass.csv", "weaponclass.json",
            weapons => weapons.Select(w => new PlayerWeaponClass() { Id = w.Id, Name = w.FullName, WeaponClass = w.WeaponClass, HitSound = w.HitSound }).ToList()
        );

        var jobs = ConvertToClient<CsvJobs, PlayerClassData>("Jobs.csv", "playerclass.json",
            jobs => jobs.Select(j => new PlayerClassData() { Id = j.Id, Name = j.Class, SpriteFemale = j.SpriteFemale, SpriteMale = j.SpriteMale }).ToList()
            );

        PlayerWeaponData CsvWeaponDataToClient(CsvJobWeaponInfo w) => new()
        {
            Job = jobs.First(j => j.Name == w.Job).Id,
            Class = classes.First(c => c.WeaponClass == w.Class).Id,
            AttackMale = w.AttackMale,
            AttackFemale = w.AttackFemale,
            SpriteFemale = string.IsNullOrWhiteSpace(w.SpriteFemale) ? string.Empty : "Assets/Sprites/Weapons/" + w.SpriteFemale,
            SpriteMale = string.IsNullOrWhiteSpace(w.SpriteMale) ? string.Empty : "Assets/Sprites/Weapons/" + w.SpriteMale,
            EffectMale = string.IsNullOrWhiteSpace(w.EffectMale) ? string.Empty : "Assets/Sprites/Weapons/" + w.EffectMale,
            EffectFemale = string.IsNullOrWhiteSpace(w.EffectFemale) ? string.Empty : "Assets/Sprites/Weapons/" + w.EffectFemale
        };

        //takes some extra processing because we're filling in each type that's not included in JobWeaponInfo.csv
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