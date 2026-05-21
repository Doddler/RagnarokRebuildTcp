using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using RebuildSharedData.ClientTypes;
using RoRebuildServer.Data;
using RoRebuildServer.Logging;

namespace DataToClientUtility;

internal static class NpcJsonExport
{
    private static readonly Regex HeaderPattern = new(
        @"\b(Npc|Trader)\s*\(\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*([A-Za-z]+)\s*(?:,[^)]*)?\)",
        RegexOptions.Compiled);

    private static readonly Regex KafraMacroPattern = new(
        @"@(Kafra|KafraNoSave)\s*\(\s*""([^""]+)""\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*([A-Za-z]+)\s*,\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex SellItemPattern = new(
        @"\bSellItem\s*\(\s*""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex LineComment = new(@"//[^\n]*", RegexOptions.Compiled);
    private static readonly Regex BlockComment = new(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

    public static Dictionary<int, List<int>> Write(string outPath, params string[] npcsSourcePaths)
    {
        var itemToNpcIds = new Dictionary<int, List<int>>();

        var resolvedDirs = new List<string>();
        foreach (var p in npcsSourcePaths)
        {
            var full = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, p));
            if (!Directory.Exists(full))
            {
                ServerLogger.LogWarning($"NPCs directory not found: {full}");
                continue;
            }
            resolvedDirs.Add(full);
        }

        var entries = new List<NpcEntry>();
        var nextId = 1;
        var sellItemCount = 0;

        var scriptFiles = resolvedDirs.SelectMany(d => Directory.GetFiles(d, "*.txt", SearchOption.AllDirectories));
        foreach (var scriptPath in scriptFiles)
        {
            var raw = File.ReadAllText(scriptPath);
            // Strip comments so commented-out SellItem lines and braces inside comments don't confuse the scan.
            var content = BlockComment.Replace(raw, "");
            content = LineComment.Replace(content, "");

            foreach (Match m in HeaderPattern.Matches(content))
            {
                var blockType = m.Groups[1].Value;
                var map = m.Groups[2].Value;
                var name = m.Groups[3].Value;
                var sprite = m.Groups[4].Value;
                var x = int.Parse(m.Groups[5].Value);
                var y = int.Parse(m.Groups[6].Value);
                var facing = m.Groups[7].Value;

                var afterHeader = m.Index + m.Length;
                var openBrace = content.IndexOf('{', afterHeader);
                if (openBrace < 0) continue;

                var depth = 0;
                var bodyEnd = -1;
                for (var i = openBrace; i < content.Length; i++)
                {
                    if (content[i] == '{') depth++;
                    else if (content[i] == '}')
                    {
                        depth--;
                        if (depth == 0) { bodyEnd = i; break; }
                    }
                }
                if (bodyEnd < 0) continue;

                var body = content.Substring(openBrace + 1, bodyEnd - openBrace - 1);

                var entry = new NpcEntry
                {
                    Id = nextId++,
                    Map = map,
                    Name = name,
                    SpriteCode = sprite,
                    X = x,
                    Y = y,
                    Facing = facing,
                    IsTrader = blockType == "Trader",
                    SellsItems = new List<int>(),
                };

                foreach (Match sm in SellItemPattern.Matches(body))
                {
                    var code = sm.Groups[1].Value;
                    if (!DataManager.ItemIdByName.TryGetValue(code, out var itemId))
                    {
                        ServerLogger.LogWarning($"NPC '{name}' on '{map}' sells unknown item '{code}'");
                        continue;
                    }

                    if (!entry.SellsItems.Contains(itemId))
                        entry.SellsItems.Add(itemId);

                    if (!itemToNpcIds.TryGetValue(itemId, out var list))
                        itemToNpcIds[itemId] = list = new List<int>();
                    if (!list.Contains(entry.Id))
                        list.Add(entry.Id);

                    sellItemCount++;
                }

                entries.Add(entry);
            }

            foreach (Match m in KafraMacroPattern.Matches(content))
            {
                var map = m.Groups[2].Value;
                var x = int.Parse(m.Groups[3].Value);
                var y = int.Parse(m.Groups[4].Value);
                var facing = m.Groups[5].Value;
                var chatName = m.Groups[7].Value;
                var sprite = m.Groups[8].Value;

                entries.Add(new NpcEntry
                {
                    Id = nextId++,
                    Map = map,
                    Name = chatName,
                    SpriteCode = sprite,
                    X = x,
                    Y = y,
                    Facing = facing,
                    IsTrader = false,
                    SellsItems = new List<int>(),
                });
            }
        }

        var output = new NpcDbFile
        {
            Items = entries.OrderBy(e => e.Map).ThenBy(e => e.Name).ToList()
        };

        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
        var json = JsonSerializer.Serialize(output, options);

        var outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, outPath));
        Directory.CreateDirectory(outDir);
        var npcPath = Path.Combine(outDir, "npcdatabase.json");
        File.WriteAllText(npcPath, json);

        Console.WriteLine($"Writing data to {npcPath} ({entries.Count} NPCs, {sellItemCount} SellItem entries)");

        return itemToNpcIds;
    }
}
