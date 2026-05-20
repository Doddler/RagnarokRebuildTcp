using System.Text.Json;
using System.Text.Json.Serialization;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.Data.MapData;

namespace RoWikiGenerator.Generators;

public class MonsterDbDropEntry
{
    public int ItemId { get; set; }
    public int Chance { get; set; }
    public int CountMin { get; set; }
    public int CountMax { get; set; }
}

public class MonsterDbSpawnEntry
{
    public string Map { get; set; } = "";
    public int Count { get; set; }
    public int RespawnMin { get; set; }
    public int RespawnMax { get; set; }
    public bool IsBoss { get; set; }
    public bool IsMvp { get; set; }
}

public class MonsterDbEntry
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public int HP { get; set; }
    public int Exp { get; set; }
    public int JExp { get; set; }
    public int AtkMin { get; set; }
    public int AtkMax { get; set; }
    public int Def { get; set; }
    public int MDef { get; set; }
    public int Str { get; set; }
    public int Agi { get; set; }
    public int Vit { get; set; }
    public int Int { get; set; }
    public int Dex { get; set; }
    public int Luk { get; set; }
    public int Range { get; set; }
    public int ScanDist { get; set; }
    public float MoveSpeed { get; set; }
    public string Size { get; set; } = "";
    public string Element { get; set; } = "";
    public string Race { get; set; } = "";
    public string Ai { get; set; } = "";
    public string Special { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public List<MonsterDbDropEntry> Drops { get; set; } = new();
    public List<MonsterDbSpawnEntry> Spawns { get; set; } = new();
}

public class MonsterDbFile
{
    public List<MonsterDbEntry> Items { get; set; } = new();
}

internal static class MonsterJsonExport
{
    public static async Task WriteAsync()
    {
        if (Monsters.MonsterModel == null)
            throw new InvalidOperationException(
                "MonsterJsonExport must run after Monsters.RenderMonsterPage().");

        var spawnLookup = Monsters.MonsterModel.MonsterMapSpawns;

        var output = new MonsterDbFile();

        foreach (var mob in Monsters.MonsterModel.Monsters)
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
            };

            if (mob.Tags != null)
            {
                foreach (var tagId in mob.Tags)
                {
                    if (Monsters.IdToTagLookup.TryGetValue(tagId, out var tagName))
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

            if (spawnLookup.TryGetValue(mob.Id, out var spawns))
            {
                foreach (var (mapCode, rule) in spawns)
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
            Converters = { new JsonStringEnumConverter() }
        };

        var json = JsonSerializer.Serialize(output, options);

        var outDir = Path.Combine(
            AppSettings.ClientProjectPath,
            "Assets", "StreamingAssets", "ClientConfigGenerated");
        Directory.CreateDirectory(outDir);

        var outPath = Path.Combine(outDir, "monsterdatabase.json");
        await File.WriteAllTextAsync(outPath, json);

        Console.WriteLine(
            $"Wrote monster database: {output.Items.Count} monsters → {outPath}");
    }
}
