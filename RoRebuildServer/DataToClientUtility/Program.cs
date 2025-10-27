﻿using System.Dynamic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Dahomey.Json;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Util;
using RoRebuildServer.Data;
using RoRebuildServer.Data.CsvDataTypes;
using RoRebuildServer.Data.Map;
using Tomlyn;

namespace DataToClientUtility;

class Program
{
    private const string path = @"..\..\..\..\GameConfig\ServerData\Db\";
    private const string outPath = @"..\..\..\..\..\RebuildClient\Assets\StreamingAssets\ClientConfigGenerated\";
    private const string outPathStreaming = @"..\..\..\..\..\RebuildClient\Assets\StreamingAssets\";
    private const string configPath = @"..\..\..\..\..\RebuildServer\";

    private static List<PlayerWeaponClass>? weaponClasses;
    private static Dictionary<string, string> equipGroupDescriptions = new();

    static void Main(string[] args)
    {
        if (!Directory.Exists(outPath))
            Directory.CreateDirectory(outPath);

        AppSettings.LoadConfigFromServerPath();
        DataManager.Initialize();

        WriteVersionInfo();
        WriteMonsterData();
        //WriteServerConfig();
        WriteMapList();
        WriteEffectsList();
        WriteJobDataStuff();
        WriteItemsList();
        WritePatchNotes();
        WriteEmoteData();
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

    private static void WritePatchNotes()
    {

        var patchNotes = new List<PatchNotes>();

        var lines = File.ReadAllLines(Path.Combine(path, "../Config/PatchNotes.txt"));
        var sb = new StringBuilder();
        var curItem = "";
        var lineNum = 0;
        foreach (var line in lines)
        {
            if (line.StartsWith("//"))
                continue;
            if (line.StartsWith("::"))
            {
                var l = line;
                if (line.Contains("//"))
                    l = line.Split("//")[0].Trim();
                var newItem = l.Substring(2);
                if (curItem != "" && sb.Length > 0)
                    patchNotes.Add(new PatchNotes { Date = curItem, Desc = sb.ToString().Trim() });
                curItem = newItem;
                sb.Clear();
                lineNum = 0;
                continue;
            }

            sb.AppendLine(line);

            lineNum++;
        }

        if (!string.IsNullOrWhiteSpace(curItem) && sb.Length > 0)
            patchNotes.Add(new PatchNotes { Date = curItem, Desc = sb.ToString().Trim() });

        SaveToClient("PatchNotes.txt", patchNotes);

    }

    private static void BuildJobMatrix()
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false };
        using var tr = new StreamReader(Path.Combine(path, "EquipmentGroups.csv")) as TextReader;
        using var csv = new CsvReader(tr, config);

        var entries = csv.GetRecords<dynamic>().ToList();
        var autoDesc = new StringBuilder();

        foreach (IDictionary<string, object> e in entries)
        {
            var s = e.Values.ToList();
            var jobGroup = (string)s[0];
            var desc = (string)s[1];

            var parentGroups = new List<string>();
            for (var i = 2; i < s.Count; i++)
            {
                var job = (string)s[i];
                parentGroups.Add(job);
            }

            if (desc != "<Auto>")
            {
                equipGroupDescriptions[jobGroup] = desc;
            }
            else
            {
                autoDesc.Clear();
                foreach (var p in parentGroups)
                {
                    if (autoDesc.Length > 0)
                        autoDesc.Append(", ");
                    if (equipGroupDescriptions.TryGetValue(p, out var existingDesc))
                        autoDesc.Append(existingDesc);
                    else
                        autoDesc.Append(p);
                }

                equipGroupDescriptions[jobGroup] = autoDesc.ToString();
            }
        }

        //Console.WriteLine($"Finished building job matrix");
    }

    private static string FixDescriptionTags(string line)
    {

        return line.Replace("<skill>", "<color=#0000FF>")
                   .Replace("<status>", "<color=#800000>")
                   .Replace("</skill>", "</color>")
                   .Replace("</status>", "</color>")
                   .Replace("<desc>", "<color=#808080>")
                   .Replace("</desc>", "</color>")
            ;
    }

    private static void WriteItemsList()
    {
        var itemList = new ItemDataList();
        itemList.Items = new List<ItemData>();
        var prefixList = new CardPrefixDataList();
        prefixList.Items = new List<CardPrefixData>();

        //item descriptions
        var itemDescLookup = new Dictionary<string, string>();
        var itemDescriptions = new List<ItemDescription>();
        var missingItemDescriptions = new List<string>();

        if (weaponClasses == null)
            throw new Exception($"Weapon class information must be loaded before this function call.");

        BuildJobMatrix();

        foreach (var descFile in Directory.GetFiles(Path.Combine(path, "../ItemDescriptions/")))
        {
            var lines = File.ReadAllLines(descFile);
            var sb = new StringBuilder();
            var curItem = "";
            var itemDesc = new Dictionary<string, string>();
            var lineNum = 0;
            var hasDescription = false;
            foreach (var line in lines)
            {
                if (line.StartsWith("//"))
                    continue;
                if (line.StartsWith("::"))
                {
                    var l = line;
                    if (line.Contains("//"))
                        l = line.Split("//")[0].Trim();
                    var newItem = l.Substring(2);
                    if (curItem != "" && sb.Length > 0)
                        itemDescLookup.Add(curItem, sb.ToString().Trim());
                    curItem = newItem;
                    sb.Clear();
                    hasDescription = false;
                    lineNum = 0;
                    continue;
                }

                var l2 = FixDescriptionTags(line);

                if (l2.StartsWith("<color=#808080>"))
                    hasDescription = true;
                if (lineNum > 0)
                {
                    if (hasDescription && lineNum == 1 && !string.IsNullOrWhiteSpace(l2.Trim()))
                        sb.Append("<line-height=120%>\n<line-height=100%>");
                    else
                        sb.Append("\n");
                }

                sb.Append(l2);

                lineNum++;
            }

            if (!string.IsNullOrWhiteSpace(curItem) && sb.Length > 0)
                itemDescLookup.Add(curItem, sb.ToString().Trim());
        }

        var displaySpriteList = new StringBuilder();
        var desc = "";

        foreach (var entry in GetCsvRows<CsvItemUseable>("ItemsUsable.csv"))
        {
            var itemData = DataManager.ItemList[entry.Id];
            var item = new ItemData()
            {
                Code = entry.Code,
                Name = entry.Name,
                Id = entry.Id,
                IsUnique = false,
                ItemClass = ItemClass.Useable,
                UseType = entry.UseMode,
                Price = itemData.Price,
                SellPrice = itemData.SellToStoreValue,
                Weight = entry.Weight,
                Sprite = entry.Sprite,
            };
            itemList.Items.Add(item);

            //fill in item description data
            if (!itemDescLookup.TryGetValue(item.Code, out var curDesc))
            {
                missingItemDescriptions.Add(entry.Code);
                desc = $"<color=#080808><i>An item with unknown properties.</i></color>";
            }
            else
                desc = curDesc;

            if (desc.Contains("[Properties]"))
            {
                var s = desc.Split("[Properties]");
                if (s[0].EndsWith("<line-height=120%>\n<line-height=100%>"))
                    s[0] = s[0].Substring(0, s[0].Length - "<line-height=120%>\n<line-height=100%>".Length);
                desc = $"{s[0].TrimEnd()}<line-height=120%>\n<line-height=100%>{s[1].Trim()}\nWeight: <color=#777777>{item.Weight / 10f:0.#}</color>";
            }
            else
                desc += $"<line-height=120%>\n</line-height=100%>Weight: <color=#777777>{item.Weight / 10f:0.#}</color>";
            itemDescriptions.Add(new ItemDescription() { Code = item.Code, Description = desc });
        }

        foreach (var entry in GetCsvRows<CsvItemRegular>("ItemsRegular.csv"))
        {
            var itemData = DataManager.ItemList[entry.Id];
            var item = new ItemData()
            {
                Code = entry.Code,
                Name = entry.Name,
                Id = entry.Id,
                IsUnique = false,
                ItemClass = ItemClass.Etc,
                UseType = ItemUseType.NotUsable,
                Price = itemData.Price,
                SellPrice = itemData.SellToStoreValue,
                Weight = entry.Weight,
                Sprite = entry.Sprite,
            };
            itemList.Items.Add(item);

            //fill in item description data
            if (!itemDescLookup.TryGetValue(item.Code, out var curDesc))
            {
                missingItemDescriptions.Add(entry.Code);
                desc = $"<color=#080808><i>An item with unknown properties.</i></color>";
            }
            else
                desc = curDesc;
            desc += $"<line-height=120%>\n</line-height=100%>Weight: <color=#777777>{item.Weight / 10f}</color>";
            itemDescriptions.Add(new ItemDescription() { Code = item.Code, Description = desc });
        }

        foreach (var entry in GetCsvRows<CsvItemWeapon>("ItemsWeapons.csv"))
        {
            var itemData = DataManager.ItemList[entry.Id];
            var classDef = weaponClasses.FirstOrDefault(w => w.WeaponClass == entry.Type, new PlayerWeaponClass() { Name = entry.Type });
            var item = new ItemData()
            {
                Code = entry.Code,
                Name = entry.Name,
                Id = entry.Id,
                ItemRank = entry.Rank - 1,
                IsUnique = true,
                IsRefinable = entry.Refinable.ToLower() == "yes",
                ItemClass = ItemClass.Weapon,
                UseType = ItemUseType.NotUsable,
                Slots = entry.Slot,
                Price = itemData.Price,
                SellPrice = itemData.SellToStoreValue,
                Weight = entry.Weight,
                Sprite = entry.Sprite,
                Position = entry.Position == WeaponPosition.MainHand ? EquipPosition.MainHand : EquipPosition.BothHands,
                SubType = classDef.Id
            };
            itemList.Items.Add(item);

            if (!string.IsNullOrWhiteSpace(entry.WeaponSprite))
                displaySpriteList.AppendLine($"{entry.Code}\t{entry.WeaponSprite}");

            //fill in item description data
            if (!itemDescLookup.TryGetValue(item.Code, out var curDesc))
            {
                missingItemDescriptions.Add(entry.Code);
                desc = $"<color=#080808><i>A weapon with unknown properties.</i></color>";
            }
            else
                desc = curDesc;

            var breakable = entry.Breakable.ToLower() == "yes";
            var refinable = entry.Refinable.ToLower() == "yes";

            
            var equipGroup = equipGroupDescriptions.TryGetValue(entry.EquipGroup, out var groupName) ? groupName : "<i>Currently unequippable by any job</i>";
            desc += $"<line-height=120%>\n</line-height=100%>";
            //desc += $"<line-height=120%>\n</line-height=100%>Type: <color=#777777>Weapon</color>";
            desc += $"Class: <color=#777777>{classDef.Name}</color>\n";

            if (classDef.Name == "Bow" && classDef.Name == "Rod")
                breakable = true; //this isn't actually true but we don't need to show this value for bows and rods

            if (!breakable && !refinable)
                desc += "Durability: <color=#777777>Unbreakable, Unrefinable</color>\n";
            else if (!refinable)
                desc += "Durability: <color=#777777>Unrefinable</color>\n";
            else if (!breakable)
                desc += "Durability: <color=#777777>Unbreakable</color>\n";

            desc += $"Attack: <color=#777777>{entry.Attack}</color>\n";
            if (entry.Property != AttackElement.Neutral && entry.Property != AttackElement.None)
                desc += $"Property: <color=#777777>{entry.Property}</color>\n";
            desc += $"Weight: <color=#777777>{item.Weight / 10f}</color>\n";
            desc += $"Weapon Level: <color=#777777>{entry.Rank}</color>\n";
            if (entry.MinLvl > 1)
                desc += $"Required Level: <color=#777777>{entry.MinLvl}</color>\n";
            desc += $"Jobs: <color=#777777>{equipGroup}</color>";
            itemDescriptions.Add(new ItemDescription() { Code = item.Code, Description = desc });
        }

        foreach (var entry in GetCsvRows<CsvItemEquipment>("ItemsEquipment.csv"))
        {
            var pos = (entry.Type & ~EquipPosition.Headgear) | (EquipPosition)entry.Position;
            var itemData = DataManager.ItemList[entry.Id];

            var item = new ItemData()
            {
                Code = entry.Code,
                Name = entry.Name,
                Id = entry.Id,
                IsUnique = true,
                ItemRank = 4,
                IsRefinable = entry.Refinable.ToLower() == "yes",
                ItemClass = ItemClass.Equipment,
                UseType = ItemUseType.NotUsable,
                Slots = entry.Slot,
                Price = itemData.Price,
                SellPrice = itemData.SellToStoreValue,
                Weight = entry.Weight,
                Sprite = entry.Sprite,
                Position = pos
            };
            itemList.Items.Add(item);

            if (!string.IsNullOrWhiteSpace(entry.DisplaySprite))
                displaySpriteList.AppendLine($"{entry.Code}\t{entry.DisplaySprite.ToLowerInvariant()}");

            //fill in item description data
            if (!itemDescLookup.TryGetValue(item.Code, out var curDesc))
            {
                missingItemDescriptions.Add(entry.Code);
                desc = $"<color=#080808><i>A piece of equipment with unknown properties.</i></color>";
            }
            else
                desc = curDesc;


            var equipGroup = equipGroupDescriptions.TryGetValue(entry.EquipGroup, out var groupName) ? groupName : "<i>Currently unequippable by any job</i>";
            var type = entry.Type switch
            {
                EquipPosition.Headgear => "Headgear",
                EquipPosition.Armor => "Body",
                EquipPosition.Boots => "Footgear",
                EquipPosition.Garment => "Garment",
                EquipPosition.Accessory => "Accessory",
                EquipPosition.Shield => "Shield",
                _ => "Unknown"
            };

            if (entry.Type == EquipPosition.Headgear)
            {
                var headPosition = entry.Position switch
                {
                    HeadgearPosition.Top => "Top",
                    HeadgearPosition.Mid => "Mid",
                    HeadgearPosition.Bottom => "Lower",
                    HeadgearPosition.TopMid => "Top + Mid",
                    HeadgearPosition.TopBottom => "Top + Lower",
                    HeadgearPosition.MidBottom => "Mid + Lower",
                    HeadgearPosition.All => "All",
                    _ => "N/A"
                };
                type += $" ({headPosition})";
            }

            desc += $"<line-height=120%>\n</line-height=100%>Type: <color=#777777>{type}</color>";

            //this is a mess, but basically you can't refine or break accessories or headgear that don't occupy the top headgear slot
            bool IsMidOrLower(HeadgearPosition p) => ((p & HeadgearPosition.Mid) > 0 || (p & HeadgearPosition.Bottom) > 0);

            if (entry.Type != EquipPosition.Accessory)
            {
                if (entry.Breakable.ToLower() == "no" && entry.Refinable.ToLower() == "no" && !IsMidOrLower(entry.Position))
                    desc += "\nDurability: <color=#777777>Unbreakable, Unrefinable</color>";
                else if (entry.Refinable.ToLower() == "no" && (entry.Type != EquipPosition.Headgear || (entry.Position & HeadgearPosition.Top) > 0))
                    desc += "\nDurability: <color=#777777>Unrefinable</color>";
                else if (entry.Breakable.ToLower() == "no" && !IsMidOrLower(entry.Position))
                    desc += "\nDurability: <color=#777777>Unbreakable</color>";
            }

            desc += $"\nDefense: <color=#777777>{entry.Defense}</color>";
            if (entry.MagicDef > 0)
                desc += $"\nMagic Defense: <color=#777777>{entry.MagicDef}</color>";
            desc += $"\nWeight: <color=#777777>{item.Weight / 10f}</color>";
            if (entry.MinLvl > 1)
                desc += $"\nRequired Level: <color=#777777>{entry.MinLvl}</color>";
            if (entry.EquipGroup != "AllJobs")
                desc += $"\nJobs: <color=#777777>{equipGroup}</color>";
            itemDescriptions.Add(new ItemDescription() { Code = item.Code, Description = desc });
        }

        foreach (var entry in GetCsvRows<CsvItemCard>("ItemsCards.csv"))
        {
            var itemData = DataManager.ItemList[entry.Id];
            var item = new ItemData()
            {
                Code = entry.Code,
                Name = entry.Name,
                Id = entry.Id,
                IsUnique = false,
                ItemClass = ItemClass.Card,
                UseType = ItemUseType.NotUsable,
                Price = itemData.Price,
                SellPrice = itemData.SellToStoreValue,
                Weight = entry.Weight,
                Sprite = entry.Sprite,
                Position = entry.EquipableSlot
            };
            itemList.Items.Add(item);
            prefixList.Items.Add(new CardPrefixData() { Id = entry.Id, Prefix = entry.Prefix, Postfix = entry.Postfix });

            var type = entry.EquipableSlot switch
            {
                EquipPosition.Weapon => "Weapon",
                EquipPosition.Headgear => "Headgear",
                EquipPosition.Armor => "Armor",
                EquipPosition.Boots => "Footgear",
                EquipPosition.Garment => "Garment",
                EquipPosition.Accessory => "Accessory",
                EquipPosition.Shield => "Shield",
                EquipPosition.Any => "Any",
                _ => "Unknown"
            };

            //fill in item description data
            if (!itemDescLookup.TryGetValue(item.Code, out var curDesc))
            {
                missingItemDescriptions.Add(entry.Code);
                desc = $"<color=#080808><i>A card with unknown properties.</i></color>";
            }
            else
                desc = curDesc;
            //desc = "<color=#808080>A card with an illustration of a monster on it.</color>";
            desc += "<line-height=120%>\n</line-height=100%>";
            desc += $"Sockets In: <color=#777777>{type}</color>";
            desc += $"\nWeight: <color=#777777>{item.Weight / 10f}</color>";
            itemDescriptions.Add(new ItemDescription() { Code = item.Code, Description = desc });
        }

        foreach (var entry in GetCsvRows<CsvNonCardPrefixes>("NonCardPrefixes.csv"))
        {
            prefixList.Items.Add(new CardPrefixData() { Id = DataManager.ItemIdByName[entry.Code], Prefix = entry.Prefix, Postfix = entry.Postfix });
        }

        foreach (var entry in GetCsvRows<CsvItemAmmo>("ItemsAmmo.csv"))
        {
            var itemData = DataManager.ItemList[entry.Id];
            var item = new ItemData()
            {
                Code = entry.Code,
                Name = entry.Name,
                Id = entry.Id,
                IsUnique = false,
                ItemClass = ItemClass.Ammo,
                UseType = ItemUseType.NotUsable,
                Price = itemData.Price,
                SellPrice = itemData.SellToStoreValue,
                Weight = entry.Weight,
                Sprite = entry.Sprite,
            };
            itemList.Items.Add(item);

            //fill in item description data
            if (!itemDescLookup.TryGetValue(item.Code, out var curDesc))
            {
                missingItemDescriptions.Add(entry.Code);
                desc = $"<color=#080808><i>A projectile with unknown properties.</i></color>";
            }
            else
                desc = curDesc;

            desc += $"<line-height=120%>\n</line-height=100%>Attack: <color=#777777>{entry.Attack}</color>\n";
            if (entry.Property != AttackElement.Neutral && entry.Property != AttackElement.None)
                desc += $"Property: <color=#777777>{entry.Property}</color>\n";
            desc += $"Weight: <color=#777777>{item.Weight / 10f:0.#}</color>";
            itemDescriptions.Add(new ItemDescription() { Code = item.Code, Description = desc });
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

        File.WriteAllText(Path.Combine(outPath, "missingItemDescriptions.txt"), string.Join("\n", missingItemDescriptions));
        SaveToClient("itemDescriptions.json", itemDescriptions);
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

    private static void WriteJobExpChart()
    {
        var txtOut = new StringBuilder();
        for (var i = 0; i < 70; i++)
        {
            var j0 = DataManager.ExpChart.RequiredJobExp(0, i);
            var j1 = DataManager.ExpChart.RequiredJobExp(1, i);
            var j2 = DataManager.ExpChart.RequiredJobExp(9, i);
            txtOut.AppendLine($"{j0},{j1},{j2}");
        }
        File.WriteAllText(Path.Combine(outPath, "jobexpchart.txt"), txtOut.ToString());
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


    private static void WriteEmoteData()
    {
        ConvertToClient<CsvEmote, EmoteData>("Emotes.csv", "emotes.json", convert =>
            {
                return convert.Select(e => new EmoteData()
                {
                    Id = e.Id,
                    Sprite = e.Sprite,
                    Frame = e.Frame,
                    Size = e.Size,
                    X = e.X,
                    Y = e.Y,
                    Commands = e.Commands ?? ""
                }).ToList();
            }
        );
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
                    CanMemo = (e.GetFlags() & MapFlags.CanMemo) > 0,
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
        WriteJobExpChart();

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

    public static void WriteVersionInfo()
    {
        File.WriteAllText(Path.Combine(outPath, "ServerVersion.txt"), DataManager.ServerVersionNumber.ToString());
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

        weaponClasses = classes;

        //job list
        var jobs = ConvertToClient<CsvJobs, PlayerClassData>("Jobs.csv", "playerclass.json",
            jobs => jobs.Select(j => new PlayerClassData() { Id = j.Id, Name = j.Class, SpriteFemale = j.SpriteFemale, SpriteMale = j.SpriteMale, ExpChart = j.ExpChart}).ToList()
            );


        PlayerWeaponData CsvWeaponDataToClient(CsvJobWeaponInfo w) => new()
        {
            Job = jobs.First(j => j.Name == w.Job).Id,
            Class = classes.First(c => c.WeaponClass == w.Class).Id,
            Class2 = !string.IsNullOrWhiteSpace(w.Class2) ? classes.First(c => c.WeaponClass == w.Class2).Id : -1,
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

        //status descriptions

        lines = File.ReadAllLines(Path.Combine(path, "../Skills/StatusEffectDescriptions.txt"));
        sb.Clear();
        var curStatus = CharacterStatusEffect.None;
        var statusDesc = new Dictionary<CharacterStatusEffect, string>();
        foreach (var line in lines)
        {
            if (line.StartsWith("//"))
                continue;
            if (line.StartsWith("::"))
            {
                if (!Enum.TryParse<CharacterStatusEffect>(line.Substring(2), true, out var type))
                    throw new Exception($"Could not parse status {line} in StatusEffectDescriptions.txt");
                if (curSkill != CharacterSkill.None && sb.Length > 0)
                    statusDesc.Add(curStatus, sb.ToString().Trim());
                curStatus = type;
                sb.Clear();
                continue;
            }

            sb.AppendLine(line);
        }
        if (curStatus != CharacterStatusEffect.None && sb.Length > 0)
            statusDesc.Add(curStatus, sb.ToString().Trim());

        //status data
        var options = new TomlModelOptions() { ConvertPropertyName = name => name, ConvertFieldName = name => name, IncludeFields = true };
        var statusData = Toml.ToModel<Dictionary<string, StatusEffectData>>(File.ReadAllText(Path.Combine(path, "../Skills/StatusEffects.toml"), Encoding.UTF8), null, options);
        var statusOut = new List<StatusEffectData>();
        foreach (var (id, status) in statusData)
        {
            status.StatusEffect = Enum.Parse<CharacterStatusEffect>(id);
            if (statusDesc.TryGetValue(status.StatusEffect, out var desc))
            {
                var baseName = status.Name;
                if (string.IsNullOrWhiteSpace(baseName))
                    baseName = status.StatusEffect.ToString();
                var name = status.Type == "Debuff"
                    ? $"<color=#FF3300>{baseName}</color>\n"
                    : $"<color=#FFA300>{baseName}</color>\n";
                status.Description = name + desc;
            }
            statusOut.Add(status);
        }

        SaveToClient("statusinfo.json", statusOut);


        //skill data
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
        var skillTreeData = Toml.ToModel<Dictionary<string, CsvPlayerSkillTree>>(File.ReadAllText(Path.Combine(path, "../Skills/SkillTree.toml"), Encoding.UTF8), null, options);
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