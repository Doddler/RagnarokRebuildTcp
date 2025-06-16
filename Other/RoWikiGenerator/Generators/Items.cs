using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Storage.Json;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.Data.CsvDataTypes;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Data.Player;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Monsters;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking.PacketHandlers.NPC;
using RoWikiGenerator.Pages;

namespace RoWikiGenerator.Generators;

public record ItemSource(
    ItemInfo Item,
    Dictionary<string, List<MonsterDropData.MonsterDropEntry>> DropsFromMonsters,
    Dictionary<string, string> NpcSales,
    Dictionary<string, string> NpcTrades,
    List<string> BoxSources);

internal class FakeNpc : Npc
{
    public override void SellItem(string itemName)
    {
        if (DataManager.ItemIdByName.TryGetValue(itemName, out var id))
        {
            if (DataManager.ItemList.TryGetValue(id, out var info))
            {
                Items.RegisterNpcSellingItem(this, info, -1);
            }
        }
    }
}

public class Items
{
    public static Dictionary<int, ItemSource> AvailableItems = new();
    public static Dictionary<string, string> ItemDescLookup = new();
    public static Dictionary<string, string> SharedIcons = new();
    public static Dictionary<int, List<string>> ItemUses = new();


    public class ItemModel
    {
        public Dictionary<string, string> CategoryLookup;
        public Dictionary<string, List<object>> ItemByCategory;
    }

    public record WeaponEntry(ItemData weapon, ItemSource source, CsvItemWeapon csvData, PlayerWeaponClass weaponClass, string description, string availableJobs);
    public record UseableItemEntry(ItemData item, ItemSource source, CsvItemUseable csvData, string description, List<string> itemUsage);
    public record RegularItemEntry(ItemData item, ItemSource source, CsvItemRegular csvData, string description, List<string> itemUsage);
    public record EquipmentEntry(ItemData equipment, ItemSource source, CsvItemEquipment csvData, string equipPosition, string description, string availableJobs);

    private static List<PlayerWeaponClass>? weaponClasses;
    private static Dictionary<string, string> equipGroupDescriptions = new();
    
    private static ItemSource NewItemSource(ItemInfo info) => new(info, new(), new(), new(), new());

    public static void RegisterItemSource(MonsterDatabaseInfo monster, MonsterDropData.MonsterDropEntry dropEntry)
    {
        var data = DataManager.GetItemInfoById(dropEntry.Id);
        if (data == null || monster.Code == "RANDGRIS" || monster.Code == "ICE_TITAN")
            return;

        if (!AvailableItems.TryGetValue(dropEntry.Id, out var itemEntry))
        {
            var source = NewItemSource(data);

            source.DropsFromMonsters.Add(monster.Code, new List<MonsterDropData.MonsterDropEntry>() { dropEntry });

            AvailableItems.Add(dropEntry.Id, source);
            return;
        }

        if (itemEntry.DropsFromMonsters.TryGetValue(monster.Code, out var entry))
            entry.Add(dropEntry);
        else
            itemEntry.DropsFromMonsters.Add(monster.Code, new List<MonsterDropData.MonsterDropEntry> { dropEntry });
    }

    public static void RegisterNpcSellingItem(Npc npc, ItemInfo info, int price)
    {

    }

    public static void LoadItemSourceFromBoxes()
    {
        foreach (var (boxType, itemList) in DataManager.ItemBoxSummonList)
        {

            if (boxType == "Gift Box_1" || boxType == "Gift Box_2" || boxType == "Gift Box_3" || boxType == "Gift Box_4" || boxType == "Cookie_Bag")
                continue;

            var boxName = boxType switch
            {
                "Old_Violet_Box" => "Old Purple Box",
                "Gift_Box_1" => "Gift Box",
                "Gift_Box_2" => "Gift Box",
                "Gift_Box_3" => "Gift Box",
                "Gift_Box_4" => "Gift Box",
                _ => boxType.Replace("_", " ")
            };


            foreach (var item in itemList)
            {
                var data = DataManager.GetItemInfoById(item);
                if (data == null)
                    continue;

                if (!AvailableItems.TryGetValue(item, out var itemEntry))
                {
                    var source = NewItemSource(data);

                    source.BoxSources.Add(boxName);

                    AvailableItems.Add(item, source);
                    continue;
                }

                if(!itemEntry.BoxSources.Contains(boxName))
                    itemEntry.BoxSources.Add(boxName);
            }
        }
    }

    public static void LoadItemSourceFromNpcs()
    {
        foreach (var instance in WikiData.World.Instances)
        {
            foreach (var map in instance.Maps)
            {
                foreach (var entity in instance.Entities)

                    //foreach (var chunk in map.Chunks)
                    //{
                    //    foreach (var entity in chunk.AllEntities)
                {
                    if (!entity.TryGet<Npc>(out var npc))
                        continue;

                    if (npc.ItemsForSale != null && npc.ItemsForSale.Count > 0)
                    {

                        foreach (var (item, price) in npc.ItemsForSale)
                        {
                            var data = DataManager.GetItemInfoById(item);
                            if (data == null)
                                continue;

                            var npcName = $"{npc.Name} ({npc.Character.Map.Name})";
                            if(npc.Character.Map.Name == "prt_fild08")
                                npcName = $"????? ({npc.Character.Map.Name})";

                            if (!AvailableItems.TryGetValue(item, out var itemEntry))
                            {
                                var source = NewItemSource(data);

                                source.NpcSales.Add(npcName, $"{price:N0}z");

                                AvailableItems.Add(item, source);
                                continue;
                            }

                            itemEntry.NpcSales[npcName] = $"{price:N0}z";
                        }
                    }

                    if (npc.TradeItemSets != null && npc.TradeItemSets.Count > 0)
                    {
                        foreach (var (setName, set) in npc.TradeItemSets)
                        {
                            foreach (var trade in set)
                            {
                                var item = trade.ItemId;
                                var data = DataManager.GetItemInfoById(item);
                                if (data == null)
                                    continue;

                                var npcName = $"????? ({npc.Character.Map.Name})";

                                foreach (var (req, _) in trade.ItemRequirements)
                                {
                                    if (!ItemUses.TryGetValue(req, out var useList))
                                    {
                                        useList = new List<string>();
                                        ItemUses.Add(req, useList);
                                    }
                                    if(!useList.Contains(npcName))
                                        useList.Add(npcName);
                                }
                                
                                if (!AvailableItems.TryGetValue(item, out var itemEntry))
                                {
                                    var source = NewItemSource(data);

                                    source.NpcTrades.Add(npcName, "");
                                    //source.NpcSales.Add(npcName, new List<string> { $"{price:N0}z" });

                                    AvailableItems.Add(item, source);
                                    continue;
                                }

                                itemEntry.NpcTrades[npcName] = "";
                            }
                        }
                        //    }
                        //}
                    }
                }
            }
        }
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

    public static void LoadItemDescriptions()
    {

        foreach (var descFile in Directory.GetFiles(Path.Combine(ServerConfig.DataConfig.DataPath, "ItemDescriptions/")))
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
                        ItemDescLookup.Add(curItem, sb.ToString().Trim());
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
                ItemDescLookup.Add(curItem, sb.ToString().Trim());
        }

        foreach (var (id, desc) in ItemDescLookup)
        {
            var d2 = desc;
            if (desc.Contains("<color"))
            {
                d2 = Regex.Replace(desc, "<color=#([^\\\"]{6})>", "<span style=\"color: $1\">");
            }

            ItemDescLookup[id] = d2.Replace("</color>", "</span>");
        }
    }


    private static List<T> GetCsvRows<T>(string fileName)
    {
        var inPath = new TemporaryFile(Path.Combine(ServerConfig.DataConfig.DataPath, "Db/", fileName));
        using var tr = new StreamReader(inPath.FilePath, Encoding.UTF8) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        return csv.GetRecords<T>().ToList();
    }

    private static List<TDst> ConvertToClient<TSrc, TDst>(string csvName, Func<List<TSrc>, List<TDst>> convert)
    {
        var inPath = Path.Combine(ServerConfig.DataConfig.DataPath, "Db/", csvName);
        var tempPath = Path.Combine(Path.GetTempPath(), csvName); //copy in case file is locked
        File.Copy(inPath, tempPath, true);

        using var tr = new StreamReader(tempPath, Encoding.UTF8) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);
        var jobs = csv.GetRecords<TSrc>().ToList();

        var list = convert(jobs);

        return list;
    }

    public static void PrepareSharedItemIcons()
    {
        foreach (var l in File.ReadAllLines(Path.Combine(AppSettings.ClientProjectPath, "Assets/Data/SharedItemIcons.txt")))
        {
            var s = l.Split('\t');
            SharedIcons.Add(s[0], s[1]);
        }

    }


    public static void PrepareItems()
    {
        BuildJobMatrix();
        LoadItemDescriptions();


        //weapon class
        weaponClasses = ConvertToClient<CsvWeaponClass, PlayerWeaponClass>("WeaponClass.csv",
            weapons => weapons.Select(w => new PlayerWeaponClass()
            {
                Id = w.Id,
                Name = w.FullName,
                WeaponClass = w.WeaponClass,
                HitSounds = w.HitSound.Split('/').Select(a => a + ".ogg").ToList()
            }).ToList()
        );

    }



    private static void BuildJobMatrix()
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false };
        using var tr = new StreamReader(Path.Combine(ServerConfig.DataConfig.DataPath, "Db/EquipmentGroups.csv")) as TextReader;
        using var csv = new CsvReader(tr, config);

        var entries = csv.GetRecords<dynamic>().ToList();
        var autoDesc = new StringBuilder();


        var equipableJobs = new Dictionary<string, HashSet<int>>();

        foreach (IDictionary<string, object> e in entries)
        {
            var s = e.Values.ToList();
            var jobGroup = (string)s[0];
            var desc = (string)s[1];

            //if (desc.Contains("Transcended") || desc.Contains("ArcherHigh"))
            //    desc = desc;

            var hasExisting = equipableJobs.TryGetValue(jobGroup, out var set);
            if (!hasExisting || set == null)
                set = new HashSet<int>();

            var parentGroups = new List<string>();
            for (var i = 2; i < s.Count; i++)
            {
                var job = (string)s[i];
                if (equipableJobs.TryGetValue(job, out var refSet))
                {
                    foreach (var r in refSet)
                        set.Add(r);
                    parentGroups.Add(job);
                }
                else if (DataManager.JobIdLookup.TryGetValue(job, out var jobId))
                {
                    set.Add(jobId);
                    parentGroups.Add(job);
                }
            }

            if (set.Count == 0)
                continue;

            if (!hasExisting)
                equipableJobs.Add(jobGroup, set);

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

    public static async Task<string> GetEquipmentPage()
    {
        var itemsByCategory = new Dictionary<string, List<object>>();
        var categoryLookup = new Dictionary<string, string>();

        categoryLookup.Add("Headgear", "Headgear");
        categoryLookup.Add("Armor", "Armor");
        categoryLookup.Add("Shield", "Shield");
        categoryLookup.Add("Garment", "Garment");
        categoryLookup.Add("Footgear", "Footgear");
        categoryLookup.Add("Accessory", "Accessory");

        var desc = "";

        foreach (var entry in GetCsvRows<CsvItemEquipment>("ItemsEquipment.csv"))
        {
            if (!AvailableItems.TryGetValue(entry.Id, out var itemSource))
                continue;

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
            //itemList.Items.Add(item);

            //if (!string.IsNullOrWhiteSpace(entry.DisplaySprite))
            //    displaySpriteList.AppendLine($"{entry.Code}\t{entry.DisplaySprite.ToLowerInvariant()}");

            //fill in item description data
            if (!ItemDescLookup.TryGetValue(item.Code, out var curDesc))
                desc = $"<color=#080808><i>A piece of equipment with unknown properties.</i></color>";
            else
                desc = curDesc;


            var equipGroup = equipGroupDescriptions.TryGetValue(entry.EquipGroup, out var groupName)
                ? groupName
                : "<i>Currently unequippable by any job</i>";
            var type = entry.Type switch
            {
                EquipPosition.Headgear => "Headgear",
                EquipPosition.Armor => "Armor",
                EquipPosition.Boots => "Footgear",
                EquipPosition.Garment => "Garment",
                EquipPosition.Accessory => "Accessory",
                EquipPosition.Shield => "Shield",
                _ => "Unknown"
            };

            if (!itemsByCategory.TryGetValue(type, out var itemList))
                itemsByCategory.Add(type, new List<object>());

            var headPos = "";
            if (entry.Type == EquipPosition.Headgear)
            {
                headPos = entry.Position switch
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
                //type += $" ({headPosition})";
            }

            itemsByCategory[type].Add(new EquipmentEntry(item, itemSource, entry, headPos, desc, equipGroup));
        }

        int HeadValue(string text) => text switch
        {
            "Top" => 0,
            "Mid" => 1,
            "Lower" => 3,
            "Top + Mid" => 4,
            "Top + Lower" => 5,
            "Mid + Lower" => 6,
            "All" => 7,
            _ => -1
        };

        itemsByCategory["Headgear"] = itemsByCategory["Headgear"]
            .OrderBy(i => HeadValue(((EquipmentEntry)i).equipPosition))
            .ThenBy(i => ((EquipmentEntry)i).source.Item.Name).ToList();

        var itemModel = new ItemModel()
        {
            CategoryLookup = categoryLookup,
            ItemByCategory = itemsByCategory
        };


        return await Program.RenderPage<ItemModel, Equipment>(itemModel);
    }


    public static async Task<string> GetWeaponPage()
    {

        var itemsByCategory = new Dictionary<string, List<object>>();
        var categoryLookup = new Dictionary<string, string>();

        categoryLookup.Add("Dagger", "Daggers");
        categoryLookup.Add("Sword", "Swords");
        categoryLookup.Add("2HSword", "Two-Handed Swords");
        categoryLookup.Add("Spear", "Spears");
        categoryLookup.Add("2HSpear", "Two-Handed Spears");
        categoryLookup.Add("Axe", "Axes");
        categoryLookup.Add("2HAxe", "Two-Handed Axes");
        categoryLookup.Add("Mace", "Maces");
        //categoryLookup.Add("2HMace", "Two-Handed Maces");
        categoryLookup.Add("Rod", "Rods");
        //categoryLookup.Add("2HRod", "Two-Handed Rods");
        categoryLookup.Add("Bow", "Bows");

        //var itemList = new ItemDataList();
        //itemList.Items = new List<ItemData>();
        var descLookup = new Dictionary<string, string>();
        var desc = "";

        var weapons = new List<WeaponEntry>();

        foreach (var entry in GetCsvRows<CsvItemWeapon>("ItemsWeapons.csv"))
        {
            if (!AvailableItems.TryGetValue(entry.Id, out var itemSource))
                continue;

            var itemData = DataManager.ItemList[entry.Id];
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
                Position = entry.Position == WeaponPosition.MainHand ? EquipPosition.MainHand : EquipPosition.BothHands
            };
            //itemList.Items.Add(item);

            //fill in item description data
            if (!ItemDescLookup.TryGetValue(item.Code, out var curDesc))
            {
                desc = $"<color=#080808><i>A weapon with unknown properties.</i></color>";
            }
            else
                desc = curDesc;

            var classDef = weaponClasses.FirstOrDefault(w => w.WeaponClass == entry.Type, new PlayerWeaponClass() { Name = entry.Type });

            if (!equipGroupDescriptions.TryGetValue(entry.EquipGroup, out var equipGroup))
                continue;

            weapons.Add(new WeaponEntry(item, itemSource, entry, classDef, desc, equipGroup));
        }

        itemsByCategory.Add("Dagger", weapons.Where(w => w.weaponClass.Id == 1).OrderBy(w => w.csvData.Rank).ThenBy(w => w.csvData.Attack).Select(w => (object)w).ToList());
        itemsByCategory.Add("Sword", weapons.Where(w => w.weaponClass.Id == 2).OrderBy(w => w.csvData.Rank).ThenBy(w => w.csvData.Attack).Select(w => (object)w).ToList());
        itemsByCategory.Add("2HSword", weapons.Where(w => w.weaponClass.Id == 3).OrderBy(w => w.csvData.Rank).ThenBy(w => w.csvData.Attack).Select(w => (object)w).ToList());
        itemsByCategory.Add("Spear", weapons.Where(w => w.weaponClass.Id == 4).OrderBy(w => w.csvData.Rank).ThenBy(w => w.csvData.Attack).Select(w => (object)w).ToList());
        itemsByCategory.Add("2HSpear", weapons.Where(w => w.weaponClass.Id == 5).OrderBy(w => w.csvData.Rank).ThenBy(w => w.csvData.Attack).Select(w => (object)w).ToList());
        itemsByCategory.Add("Axe", weapons.Where(w => w.weaponClass.Id == 6).OrderBy(w => w.csvData.Rank).ThenBy(w => w.csvData.Attack).Select(w => (object)w).ToList());
        itemsByCategory.Add("2HAxe", weapons.Where(w => w.weaponClass.Id == 7).OrderBy(w => w.csvData.Rank).ThenBy(w => w.csvData.Attack).Select(w => (object)w).ToList());
        itemsByCategory.Add("Mace", weapons.Where(w => w.weaponClass.Id == 8).OrderBy(w => w.csvData.Rank).ThenBy(w => w.csvData.Attack).Select(w => (object)w).ToList());
        itemsByCategory.Add("Rod", weapons.Where(w => w.weaponClass.Id == 10).OrderBy(w => w.csvData.Rank).ThenBy(w => w.csvData.Attack).Select(w => (object)w).ToList());
        itemsByCategory.Add("Bow", weapons.Where(w => w.weaponClass.Id == 12).OrderBy(w => w.csvData.Rank).ThenBy(w => w.csvData.Attack).Select(w => (object)w).ToList());


        var itemModel = new ItemModel()
        {
            CategoryLookup = categoryLookup,
            ItemByCategory = itemsByCategory
        };


        return await Program.RenderPage<ItemModel, RebuildWeapons>(itemModel);
    }

    public record CardEntry(ItemInfo Item, string Description, string Prefix, int DropLvl);

    private static int GetCardDropLevel(ItemInfo card)
    {
        var available = AvailableItems[card.Id];
        var monster1 = available.DropsFromMonsters.First();
        var monster = DataManager.MonsterCodeLookup[monster1.Key];
        return monster.Level;
    }

    public static async Task<string> GetUsableItemPage()
    {
        var itemsByCategory = new Dictionary<string, List<object>>();
        var items = new List<UseableItemEntry>();

        var descLookup = new Dictionary<string, string>();
        var desc = "";

        foreach (var entry in GetCsvRows<CsvItemUseable>("ItemsUsable.csv"))
        {
            if (!AvailableItems.TryGetValue(entry.Id, out var itemSource))
                continue;

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

            //fill in item description data
            if (!ItemDescLookup.TryGetValue(item.Code, out var curDesc))
            {
                desc = $"<color=#080808><i>An item with unknown properties.</i></color>";
            }
            else
                desc = curDesc;
            //desc += $"<line-height=120%>\n</line-height=100%>Weight: <color=#777777>{item.Weight / 10f:0.#}</color>";
            descLookup.Add(item.Code, desc.Replace("\n", "<br>"));

            if (!ItemUses.TryGetValue(item.Id, out var uses))
                uses = new();
            items.Add(new UseableItemEntry(item, itemSource, entry, desc, uses));
        }

        var categoryLookup = new Dictionary<string, string>();

        categoryLookup.Add("Consumeables", "Consumable Items");
        itemsByCategory.Add("Consumeables", items.OrderBy(i => i.csvData.Id).Select(i => (object)i).ToList());

        var itemModel = new ItemModel()
        {
            CategoryLookup = categoryLookup,
            ItemByCategory = itemsByCategory
        };


        return await Program.RenderPage<ItemModel, ItemsUseable>(itemModel);
    }


    public static async Task<string> GetEtcItemPage()
    {
        var itemsByCategory = new Dictionary<string, List<object>>();
        var items = new List<RegularItemEntry>();

        var descLookup = new Dictionary<string, string>();
        var desc = "";

        foreach (var entry in GetCsvRows<CsvItemRegular>("ItemsRegular.csv"))
        {
            if (!AvailableItems.TryGetValue(entry.Id, out var itemSource))
                continue;

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

            //fill in item description data
            if (!ItemDescLookup.TryGetValue(item.Code, out var curDesc))
            {
                desc = $"<color=#080808><i>An item with unknown properties.</i></color>";
            }
            else
                desc = curDesc;
            //desc += $"<line-height=120%>\n</line-height=100%>Weight: <color=#777777>{item.Weight / 10f:0.#}</color>";
            descLookup.Add(item.Code, desc.Replace("\n", "<br>"));

            if (!ItemUses.TryGetValue(item.Id, out var uses))
                uses = new();
            items.Add(new RegularItemEntry(item, itemSource, entry, desc, uses));
        }

        var categoryLookup = new Dictionary<string, string>();

        categoryLookup.Add("Catalysts", "Skill Catalysts");
        categoryLookup.Add("Refine", "Refining Items");
        categoryLookup.Add("Normal", "Regular Items");
        
        itemsByCategory.Add("Refine", items.Where(i => i.csvData.Usage.Contains("Refine")).OrderBy(i => i.csvData.Id).Select(i => (object)i).ToList());
        itemsByCategory.Add("Catalysts", items.Where(i => i.csvData.Usage.Contains("Catalyst")).OrderBy(i => i.csvData.Id).Select(i => (object)i).ToList());
        itemsByCategory.Add("Normal", items.Where(i => !i.csvData.Usage.Contains("Refine") && !i.csvData.Usage.Contains("Catalyst")).OrderBy(i => i.csvData.Id).Select(i => (object)i).ToList());

        var itemModel = new ItemModel()
        {
            CategoryLookup = categoryLookup,
            ItemByCategory = itemsByCategory
        };


        return await Program.RenderPage<ItemModel, ItemsEtc>(itemModel);
    }


    public static async Task<string> GetCardPage()
    {
        var itemList = new ItemDataList();
        itemList.Items = new List<ItemData>();
        var prefixList = new CardPrefixDataList();
        prefixList.Items = new List<CardPrefixData>();

        var prefixLookup = new Dictionary<string, string>();
        var descLookup = new Dictionary<string, string>();

        var itemDescLookup = new Dictionary<string, string>();
        var itemDescriptions = new List<ItemDescription>();
        var desc = "";

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
            //prefixList.Items.Add(new CardPrefixData() { Id = entry.Id, Prefix = entry.Prefix, Postfix = entry.Postfix });
            if (!string.IsNullOrWhiteSpace(entry.Prefix))
                prefixLookup.Add(item.Code, entry.Prefix);
            else
                prefixLookup.Add(item.Code, entry.Postfix);

            //var type = entry.EquipableSlot switch
            //{
            //    EquipPosition.Weapon => "Weapon",
            //    EquipPosition.Headgear => "Headgear",
            //    EquipPosition.Armor => "Armor",
            //    EquipPosition.Boots => "Footgear",
            //    EquipPosition.Garment => "Garment",
            //    EquipPosition.Accessory => "Accessory",
            //    EquipPosition.Shield => "Shield",
            //    EquipPosition.Any => "Any",
            //    _ => "Unknown"
            //};

            //fill in item description data
            if (!ItemDescLookup.TryGetValue(item.Code, out var curDesc))
            {
                desc = $"<color=#080808><i>A card with unknown properties.</i></color>";
            }
            else
                desc = curDesc;
            //desc = "<color=#808080>A card with an illustration of a monster on it.</color>";
            //desc += "<line-height=120%>\n</line-height=100%>";
            //desc += $"Sockets In: <color=#777777>{type}</color>";
            //desc += $"\nWeight: <color=#777777>{item.Weight / 10f}</color>";
            descLookup.Add(item.Code, desc.Replace("\n", "<br>"));
        }


        var items = DataManager.ItemList.Values.Where(item => item.ItemClass == ItemClass.Card && AvailableItems.ContainsKey(item.Id)).ToList();

        var itemsByCategory = new Dictionary<string, List<object>>();

        itemsByCategory.Add("Headgear", new List<object>());
        itemsByCategory.Add("Armor", new List<object>());
        itemsByCategory.Add("Weapon", new List<object>());
        itemsByCategory.Add("Shield", new List<object>());
        itemsByCategory.Add("Garment", new List<object>());
        itemsByCategory.Add("Footgear", new List<object>());
        itemsByCategory.Add("Accessory", new List<object>());
        itemsByCategory.Add("Any", new List<object>());

        foreach (var item in items)
        {
            var card = DataManager.CardInfo[item.Id];
            if (AvailableItems[item.Id].DropsFromMonsters.Count <= 0)
                continue;

            var lvl = GetCardDropLevel(item);

            switch (card.EquipPosition)
            {
                case EquipPosition.Headgear:
                    itemsByCategory["Headgear"].Add(new CardEntry(item, descLookup[item.Code], prefixLookup[item.Code], lvl));
                    break;
                case EquipPosition.Armor:
                    itemsByCategory["Armor"].Add(new CardEntry(item, descLookup[item.Code], prefixLookup[item.Code], lvl));
                    break;
                case EquipPosition.Weapon:
                    itemsByCategory["Weapon"].Add(new CardEntry(item, descLookup[item.Code], prefixLookup[item.Code], lvl));
                    break;
                case EquipPosition.Shield:
                    itemsByCategory["Shield"].Add(new CardEntry(item, descLookup[item.Code], prefixLookup[item.Code], lvl));
                    break;
                case EquipPosition.Garment:
                    itemsByCategory["Garment"].Add(new CardEntry(item, descLookup[item.Code], prefixLookup[item.Code], lvl));
                    break;
                case EquipPosition.Footgear:
                    itemsByCategory["Footgear"].Add(new CardEntry(item, descLookup[item.Code], prefixLookup[item.Code], lvl));
                    break;
                case EquipPosition.Accessory:
                    itemsByCategory["Accessory"].Add(new CardEntry(item, descLookup[item.Code], prefixLookup[item.Code], lvl));
                    break;
                case EquipPosition.Any:
                    itemsByCategory["Any"].Add(new CardEntry(item, descLookup[item.Code], prefixLookup[item.Code], lvl));
                    break;
            }
        }

        foreach (var (_, list) in itemsByCategory)
        {
            list.Sort((l, r) => ((CardEntry)l).DropLvl.CompareTo(((CardEntry)r).DropLvl));
        }

        var itemModel = new ItemModel()
        {
            ItemByCategory = itemsByCategory
        };

        return await Program.RenderPage<ItemModel, RebuildItemCards>(itemModel);
    }
}