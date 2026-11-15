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
    // Captures every Warp(...) signature variant including the destination cell (dx, dy) which the
    // pathfinder needs to predict landing region.
    //   Warp("src", "sig"[, "displayName"], x, y, w, h, "dst", dx, dy [, dw, dh])
    private static readonly Regex WarpPattern = new(
        @"Warp\(\s*""(?<src>[^""]+)""\s*,\s*""[^""]+""\s*,\s*(?:""[^""]+""\s*,\s*)?" +
        @"(?<sx>-?\d+)\s*,\s*(?<sy>-?\d+)\s*,\s*-?\d+\s*,\s*-?\d+\s*,\s*" +
        @"""(?<dst>[^""]+)""\s*,\s*(?<dx>-?\d+)\s*,\s*(?<dy>-?\d+)",
        RegexOptions.Compiled);

    // Strip // line comments before regex match. Some .txt files keep legacy or arena warps as commented
    // entries (e.g. //Warp("izlude", "welcome_arena", …)). Without stripping, the regex matches these
    // and they enter mapwarps.json as live portals.
    private static readonly Regex LineCommentPattern = new(@"//[^\r\n]*", RegexOptions.Compiled);

    public static void Write(string warpsSourcePath, string outPath)
    {
        var warpsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, warpsSourcePath));

        if (!Directory.Exists(warpsDir))
        {
            ServerLogger.LogWarning($"Warps directory not found: {warpsDir}");
            return;
        }

        // Allowlist: only emit warps for maps the server has actually registered. maps.json is written
        // earlier in the same Main() so we read it back here. Falls back to "accept all" if maps.json
        // isn't readable (preserves old behaviour for any out-of-order runs).
        var knownMaps = TryReadKnownMaps(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, outPath)));
        var dropped = 0;

        var perMap = new Dictionary<string, MapWarpEntry>();

        foreach (var path in Directory.GetFiles(warpsDir, "*.txt", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(path);
            // Strip // comments so disabled warps don't get parsed as live portals.
            var stripped = LineCommentPattern.Replace(content, "");
            foreach (Match m in WarpPattern.Matches(stripped))
            {
                var from = m.Groups["src"].Value;
                var sx = int.Parse(m.Groups["sx"].Value);
                var sy = int.Parse(m.Groups["sy"].Value);
                var to = m.Groups["dst"].Value;
                var dx = int.Parse(m.Groups["dx"].Value);
                var dy = int.Parse(m.Groups["dy"].Value);
                if (from == to) continue;
                if (knownMaps != null && (!knownMaps.Contains(from) || !knownMaps.Contains(to)))
                {
                    dropped++;
                    continue;
                }

                if (!perMap.TryGetValue(from, out var entry))
                    perMap[from] = entry = new MapWarpEntry { Map = from, Portals = new List<PortalEntry>() };

                _ = dx; _ = dy; // capture & ignore until PortalEntry gains Dx/Dy fields (needs a server-side RebuildSharedData rebuild — blocked while server is running).
                entry.Portals.Add(new PortalEntry { To = to, X = sx, Y = sy });
            }
        }
        if (dropped > 0)
            Console.WriteLine($"MapWarpExport: dropped {dropped} warps referencing maps not in maps.json (commented-out / legacy guild maps / disabled portals).");

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

    /// <summary>Read maps.json (already written earlier in Main()) to get the canonical set of map
    /// codes the server has registered. Returns null on any I/O / parse failure — callers treat that as
    /// "no allowlist, accept every warp."</summary>
    private static HashSet<string>? TryReadKnownMaps(string outDir)
    {
        try
        {
            var mapsPath = Path.Combine(outDir, "maps.json");
            if (!File.Exists(mapsPath)) return null;
            var json = File.ReadAllText(mapsPath);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Items", out var items)) return null;
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var el in items.EnumerateArray())
            {
                if (el.TryGetProperty("Code", out var code) && code.ValueKind == JsonValueKind.String)
                    set.Add(code.GetString() ?? "");
            }
            return set.Count > 0 ? set : null;
        }
        catch (Exception ex)
        {
            ServerLogger.LogWarning($"MapWarpExport: couldn't read maps.json allowlist ({ex.Message}); falling back to accept-all.");
            return null;
        }
    }
}
