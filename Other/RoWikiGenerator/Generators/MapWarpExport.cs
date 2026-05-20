using System.Text.Json;
using System.Text.RegularExpressions;
using RoRebuildServer.Logging;

namespace RoWikiGenerator.Generators;

public class PortalEntry
{
    public string To { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
}

public class MapWarpEntry
{
    public string Map { get; set; } = "";
    public List<string> ConnectedTo { get; set; } = new();
    public List<PortalEntry> Portals { get; set; } = new();
}

public class MapWarpFile
{
    public List<MapWarpEntry> Items { get; set; } = new();
}

internal static class MapWarpExport
{
    private static readonly Regex WarpPattern = new(
        @"Warp\(\s*""([^""]+)""\s*,\s*""[^""]+""\s*,\s*(?:""[^""]+""\s*,\s*)?(-?\d+)\s*,\s*(-?\d+)\s*,\s*-?\d+\s*,\s*-?\d+\s*,\s*""([^""]+)""",
        RegexOptions.Compiled);

    public static void Write()
    {
        var warpsDir = Path.GetFullPath(Path.Combine(
            AppSettings.ServerPath, "../GameConfig/ServerData/Script/Warps"));

        if (!Directory.Exists(warpsDir))
        {
            ServerLogger.LogWarning($"Warps directory not found: {warpsDir}");
            return;
        }

        var perMap = new Dictionary<string, MapWarpEntry>();
        var fileCount = 0;
        var warpCount = 0;

        foreach (var path in Directory.GetFiles(warpsDir, "*.txt", SearchOption.AllDirectories))
        {
            fileCount++;
            var content = File.ReadAllText(path);
            foreach (Match m in WarpPattern.Matches(content))
            {
                var from = m.Groups[1].Value;
                var x = int.Parse(m.Groups[2].Value);
                var y = int.Parse(m.Groups[3].Value);
                var to = m.Groups[4].Value;
                if (from == to) continue;

                if (!perMap.TryGetValue(from, out var entry))
                    perMap[from] = entry = new MapWarpEntry { Map = from };

                entry.Portals.Add(new PortalEntry { To = to, X = x, Y = y });
                warpCount++;
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

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(output, options);

        var outDir = Path.Combine(
            AppSettings.ClientProjectPath,
            "Assets", "StreamingAssets", "ClientConfigGenerated");
        Directory.CreateDirectory(outDir);

        var outPath = Path.Combine(outDir, "mapwarps.json");
        File.WriteAllText(outPath, json);

        Console.WriteLine(
            $"Wrote map warps: parsed {warpCount} portals from {fileCount} files, " +
            $"{output.Items.Count} maps → {outPath}");
    }
}
