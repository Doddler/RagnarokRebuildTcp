using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using RebuildSharedData.ClientTypes;
using RoRebuildServer.Logging;

namespace DataToClientUtility;

internal static class MapWarpExport
{
    private static readonly Regex WarpPattern = new(
        @"Warp\(\s*""([^""]+)""\s*,\s*""[^""]+""\s*,\s*(?:""[^""]+""\s*,\s*)?(-?\d+)\s*,\s*(-?\d+)\s*,\s*-?\d+\s*,\s*-?\d+\s*,\s*""([^""]+)""",
        RegexOptions.Compiled);

    public static void Write(string warpsSourcePath, string outPath)
    {
        var warpsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, warpsSourcePath));

        if (!Directory.Exists(warpsDir))
        {
            ServerLogger.LogWarning($"Warps directory not found: {warpsDir}");
            return;
        }

        var perMap = new Dictionary<string, MapWarpEntry>();

        foreach (var path in Directory.GetFiles(warpsDir, "*.txt", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(path);
            foreach (Match m in WarpPattern.Matches(content))
            {
                var from = m.Groups[1].Value;
                var x = int.Parse(m.Groups[2].Value);
                var y = int.Parse(m.Groups[3].Value);
                var to = m.Groups[4].Value;
                if (from == to) continue;

                if (!perMap.TryGetValue(from, out var entry))
                    perMap[from] = entry = new MapWarpEntry { Map = from, Portals = new List<PortalEntry>() };

                entry.Portals.Add(new PortalEntry { To = to, X = x, Y = y });
            }
        }

        foreach (var entry in perMap.Values)
        {
            entry.ConnectedTo = entry.Portals
                .Select(p => p.To)
                .Distinct()
                .OrderBy(s => s)
                .ToList();
        }

        var output = new MapWarpFile
        {
            Items = perMap.Values.OrderBy(e => e.Map).ToList()
        };

        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
        var json = JsonSerializer.Serialize(output, options);

        var outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, outPath));
        Directory.CreateDirectory(outDir);

        var warpsPath = Path.Combine(outDir, "mapwarps.json");
        File.WriteAllText(warpsPath, json);

        Console.WriteLine($"Writing data to {warpsPath}");
    }
}
