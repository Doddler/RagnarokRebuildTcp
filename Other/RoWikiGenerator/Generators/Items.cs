using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.Data.CsvDataTypes;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Data.Player;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Monsters;
using RoRebuildServer.Logging;
using RoWikiGenerator.Pages;

namespace RoWikiGenerator.Generators;

public record ItemSource(
    ItemInfo Item,
    Dictionary<string, List<MonsterDropData.MonsterDropEntry>> DropsFromMonsters,
    Dictionary<string, List<string>> NpcSales);

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

    public static void RegisterItemSource(MonsterDatabaseInfo monster, MonsterDropData.MonsterDropEntry dropEntry)
    {
        var data = DataManager.GetItemInfoById(dropEntry.Id);
        if (data == null)
            return;

        if (!AvailableItems.TryGetValue(dropEntry.Id, out var itemEntry))
        {
            var source = new ItemSource(data,
                new Dictionary<string, List<MonsterDropData.MonsterDropEntry>>(),
                new Dictionary<string, List<string>>());

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

    public static void LoadItemSourceFromNpcs()
    {
        foreach (var instance in WikiData.World.Instances)
        {
            foreach (var map in instance.Maps)
            {
                foreach (var chunk in map.Chunks)
                {
                    foreach (var entity in chunk.AllEntities)
                    {
                        if (!entity.TryGet<Npc>(out var npc))
                            continue;

                        if (npc.ItemsForSale == null || npc.ItemsForSale.Count == 0)
                            continue;

                        foreach (var (item, price) in npc.ItemsForSale)
                        {
                            var data = DataManager.GetItemInfoById(item);
                            if (data == null)
                                continue;

                            var npcName = $"{npc.Name} ({npc.Character.Map.Name})";

                            if (!AvailableItems.TryGetValue(item, out var itemEntry))
                            {
                                var source = new ItemSource(data,
                                    new Dictionary<string, List<MonsterDropData.MonsterDropEntry>>(),
                                    new Dictionary<string, List<string>>());

                                source.NpcSales.Add(npcName, new List<string> { $"{price:N}z" });

                                AvailableItems.Add(item, source);
                                continue;
                            }

                            if (itemEntry.NpcSales.TryGetValue(npcName, out var saleList))
                                saleList.Add($"{price:N}z");
                            else
                                itemEntry.NpcSales.Add(npcName, new List<string> { $"{price:N}z" });
                        }
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
    }


    private static List<T> GetCsvRows<T>(string fileName)
    {
        var inPath = new TemporaryFile(Path.Combine(ServerConfig.DataConfig.DataPath, "Db/", fileName));
        using var tr = new StreamReader(inPath.FilePath, Encoding.UTF8) as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.InvariantCulture);

        return csv.GetRecords<T>().ToList();
    }

    public static void PrepareItems()
    {
        LoadItemDescriptions();
    }

    public class ItemModel
    {
        public Dictionary<string, List<object>> ItemByCategory;
    }

    public record CardEntry(ItemInfo Item, string Description, string Prefix, int DropLvl);

    private static int GetCardDropLevel(ItemInfo card)
    {
        var available = AvailableItems[card.Id];
        var monster1 = available.DropsFromMonsters.First();
        var monster = DataManager.MonsterCodeLookup[monster1.Key];
        return monster.Level;
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
            if(!string.IsNullOrWhiteSpace(entry.Prefix))
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
            list.Sort((l, r) => ((CardEntry)l).DropLvl.CompareTo(((CardEntry)r).DropLvl) );
        }

        var itemModel = new ItemModel()
        {
            ItemByCategory = itemsByCategory
        };

        return await Program.RenderPage<ItemModel, RebuildItemCards>(itemModel);
    }
}