using RoRebuildServer.Logging;

namespace RoRebuildServer.Data.Map;

public class MapEntry
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public required string WalkData { get; set; }
    public required string MapMode { get; set; }
    public required string Flags { get; set; }
    public required string Music { get; set; }

    public MapFlags GetFlags()
    {
        if (string.IsNullOrWhiteSpace(Flags))
            return MapFlags.None;

        var fSpan = Flags.AsSpan();
        var flags = MapFlags.None;

        foreach (var chunk in fSpan.Split(','))
        {
            var str = fSpan[chunk].Trim();
            if (Enum.TryParse(str, true, out MapFlags flag))
                flags |= flag;
            else
                ServerLogger.LogWarning($"Could not parse map flag {str} for map {Code}");

        }

        return flags;
    }
}

[Flags]
public enum MapFlags {
    None = 0,
    CanMemo = 1 << 1,
    NoTeleport = 1 << 2,
    NoTeleportEvenMonsters = 1 << 3,
    NoMiniMap = 1 << 4,
    NoLogOut = 1 << 5,
    NoWater = 1 << 6,
    AllWater = 1 << 7
}