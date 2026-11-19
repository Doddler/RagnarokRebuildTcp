using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DataToClientUtility;

// Extracts kafra warp data (NPC location + destination list with option indices + zeny cost) from the
// Kafra.txt macro DSL into a separate kafradatabase.json. Kept independent of NpcJsonExport so we
// don't have to expand the shared NpcEntry type — the bot deserializes this JSON into its own local
// types, no RebuildSharedData rebuild required.
//
// Server DSL shape:
//   macro KafraTeleportList<Region>() {
//       Option("a", "b", ..., "Cancel");
//       switch(Result) {
//           case 0: @TeleportCase("destMap", x, y, zeny);
//           case 1: @TeleportCase(...);
//           ...
//       }
//   }
//   @Kafra("map", x, y, facing, "npcName", "chatName", "sprite", "cutin", "saveLoc", "saveDesc", <teleportMacro>);
//   @KafraNoSave("map", x, y, facing, "npcName", "chatName", "sprite", "cutin", <teleportMacro>);
//
// @Kafra's top-level Option() puts "Teleport Service" at index 2 (Save, Use Storage, Teleport Service, ...).
// @KafraNoSave puts it at index 1 (Use Storage, Teleport Service, Cancel).
internal static class KafraExport
{
    // KafraTeleportList<Name>() macro header. Body is found by walking balanced braces from the open '{'.
    private static readonly Regex TeleportListMacro = new(
        @"\bmacro\s+(KafraTeleportList\w+)\s*\(\s*\)\s*\{",
        RegexOptions.Compiled);

    private static readonly Regex TeleportCasePattern = new(
        @"\bcase\s+(?<idx>\d+)\s*:\s*@TeleportCase\s*\(\s*""(?<map>[^""]+)""\s*,\s*(?<x>-?\d+)\s*,\s*(?<y>-?\d+)\s*,\s*(?<zeny>\d+)\s*\)",
        RegexOptions.Compiled);

    // Option("a", "b", ...) — extracts the literal string options in order.
    private static readonly Regex OptionListPattern = new(
        @"\bOption\s*\(\s*(?<args>[^)]*)\)",
        RegexOptions.Compiled);

    private static readonly Regex OptionStringPattern = new(@"""([^""]+)""", RegexOptions.Compiled);

    // @Kafra(map, x, y, facing, npcName, chatName, sprite, cutin, saveLoc, saveDesc, teleportMacroName)
    private static readonly Regex KafraInstantiation = new(
        @"@Kafra\s*\(\s*""(?<map>[^""]+)""\s*,\s*(?<x>-?\d+)\s*,\s*(?<y>-?\d+)\s*,\s*[A-Za-z]+\s*,\s*" +
        @"""[^""]*""\s*,\s*""(?<chatName>[^""]+)""\s*,\s*""[^""]*""\s*,\s*""[^""]*""\s*,\s*" +
        @"""[^""]*""\s*,\s*""[^""]*""\s*,\s*(?<macro>\w+)\s*\)",
        RegexOptions.Compiled);

    // @KafraNoSave(map, x, y, facing, npcName, chatName, sprite, cutin, teleportMacroName)
    private static readonly Regex KafraNoSaveInstantiation = new(
        @"@KafraNoSave\s*\(\s*""(?<map>[^""]+)""\s*,\s*(?<x>-?\d+)\s*,\s*(?<y>-?\d+)\s*,\s*[A-Za-z]+\s*,\s*" +
        @"""[^""]*""\s*,\s*""(?<chatName>[^""]+)""\s*,\s*""[^""]*""\s*,\s*""[^""]*""\s*,\s*(?<macro>\w+)\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex LineComment = new(@"//[^\n]*", RegexOptions.Compiled);
    private static readonly Regex BlockComment = new(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

    // The TopMenuOption index for "Teleport Service" in each macro's outer Option() call.
    // @Kafra:       Save (0), Use Storage (1), Teleport Service (2), [cartOption] (3), Cancel (4)
    // @KafraNoSave: Use Storage (0), Teleport Service (1), Cancel (2)
    private const int KafraTeleportOption = 2;
    private const int KafraNoSaveTeleportOption = 1;

    public static void Write(string npcsSourcePath, string outPath)
    {
        var sourceDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, npcsSourcePath));
        if (!Directory.Exists(sourceDir))
        {
            Console.WriteLine($"KafraExport: source dir not found ({sourceDir}); skipping.");
            return;
        }

        // 1. Find every KafraTeleportList<X>() macro body and parse it into a destination list.
        var teleportLists = new Dictionary<string, List<KafraWarp>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.GetFiles(sourceDir, "*.txt", SearchOption.AllDirectories))
        {
            var raw = File.ReadAllText(file);
            // Strip comments so commented-out @TeleportCase lines and braces inside comments are skipped.
            var content = BlockComment.Replace(raw, "");
            content = LineComment.Replace(content, "");

            foreach (Match m in TeleportListMacro.Matches(content))
            {
                var macroName = m.Groups[1].Value;
                var bodyStart = content.IndexOf('{', m.Index + m.Length - 1);
                if (bodyStart < 0) continue;

                // Walk balanced braces to find the macro body.
                var depth = 0;
                var bodyEnd = -1;
                for (var i = bodyStart; i < content.Length; i++)
                {
                    if (content[i] == '{') depth++;
                    else if (content[i] == '}')
                    {
                        depth--;
                        if (depth == 0) { bodyEnd = i; break; }
                    }
                }
                if (bodyEnd < 0) continue;

                var body = content.Substring(bodyStart + 1, bodyEnd - bodyStart - 1);

                // Option() defines the destination labels in order; @TeleportCase maps case N to a warp.
                // We use case N as the OptionIndex the bot needs to send (NpcSelectOptionAsync(N)).
                var labels = new List<string>();
                var optMatch = OptionListPattern.Match(body);
                if (optMatch.Success)
                {
                    foreach (Match s in OptionStringPattern.Matches(optMatch.Groups["args"].Value))
                        labels.Add(s.Groups[1].Value);
                }

                var warps = new List<KafraWarp>();
                foreach (Match c in TeleportCasePattern.Matches(body))
                {
                    var idx = int.Parse(c.Groups["idx"].Value);
                    var dst = c.Groups["map"].Value;
                    var dx = int.Parse(c.Groups["x"].Value);
                    var dy = int.Parse(c.Groups["y"].Value);
                    var zeny = int.Parse(c.Groups["zeny"].Value);
                    var label = idx < labels.Count ? labels[idx] : dst;
                    warps.Add(new KafraWarp
                    {
                        DestMap = dst,
                        DestX = dx,
                        DestY = dy,
                        Zeny = zeny,
                        OptionLabel = label,
                        OptionIndex = idx,
                    });
                }
                if (warps.Count > 0) teleportLists[macroName] = warps;
            }
        }

        // 2. For every @Kafra / @KafraNoSave instantiation, attach the matching teleport list.
        var entries = new List<KafraEntry>();
        var nextId = 1;
        var unresolved = 0;

        foreach (var file in Directory.GetFiles(sourceDir, "*.txt", SearchOption.AllDirectories))
        {
            var raw = File.ReadAllText(file);
            var content = BlockComment.Replace(raw, "");
            content = LineComment.Replace(content, "");

            foreach (Match m in KafraInstantiation.Matches(content))
                AddEntry(m, KafraTeleportOption);
            foreach (Match m in KafraNoSaveInstantiation.Matches(content))
                AddEntry(m, KafraNoSaveTeleportOption);

            void AddEntry(Match m, int topMenuOption)
            {
                var macroName = m.Groups["macro"].Value;
                if (!teleportLists.TryGetValue(macroName, out var warps))
                {
                    unresolved++;
                    return;
                }
                entries.Add(new KafraEntry
                {
                    Id = nextId++,
                    Map = m.Groups["map"].Value,
                    Name = m.Groups["chatName"].Value,
                    X = int.Parse(m.Groups["x"].Value),
                    Y = int.Parse(m.Groups["y"].Value),
                    TopMenuOption = topMenuOption,
                    Warps = warps, // shared list per macro — read-only by the bot
                });
            }
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(new KafraDbFile { Items = entries }, options);

        var outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, outPath));
        Directory.CreateDirectory(outDir);
        var kafraPath = Path.Combine(outDir, "kafradatabase.json");
        File.WriteAllText(kafraPath, json);

        var totalWarps = entries.Sum(e => e.Warps.Count);
        Console.WriteLine($"Writing data to {kafraPath} ({entries.Count} kafras, {totalWarps} warp options" +
                          (unresolved > 0 ? $", {unresolved} unresolved instantiations" : "") + ")");
    }

    // Local DTOs — kept here so the exporter doesn't need to add fields to the shared NpcEntry assembly.
    // The bot side has matching types in RoBotClient.Bot.GameData.KafraData; JSON is the wire format.
    private sealed class KafraEntry
    {
        public int Id { get; set; }
        public string Map { get; set; } = "";
        public string Name { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public int TopMenuOption { get; set; }
        public List<KafraWarp> Warps { get; set; } = new();
    }

    private sealed class KafraWarp
    {
        public string DestMap { get; set; } = "";
        public int DestX { get; set; }
        public int DestY { get; set; }
        public int Zeny { get; set; }
        public string OptionLabel { get; set; } = "";
        public int OptionIndex { get; set; }
    }

    private sealed class KafraDbFile
    {
        public List<KafraEntry> Items { get; set; } = new();
    }
}
