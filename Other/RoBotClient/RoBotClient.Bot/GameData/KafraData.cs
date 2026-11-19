namespace RoBotClient.Bot.GameData;

/// <summary>One kafra NPC + its full teleport menu. Matches the kafradatabase.json structure written by
/// the server-side <c>KafraExport</c>. Kept independent of the shared <c>NpcEntry</c> type so adding
/// kafra integration doesn't require rebuilding <c>RebuildSharedData.dll</c>.</summary>
public sealed class KafraEntry
{
    public int Id { get; set; }
    public string Map { get; set; } = "";
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    /// <summary>Option-index of "Teleport Service" in the kafra's first menu. 2 for @Kafra (Save, Use
    /// Storage, Teleport, [cart], Cancel), 1 for @KafraNoSave (Use Storage, Teleport, Cancel).</summary>
    public int TopMenuOption { get; set; }
    public List<KafraWarp> Warps { get; set; } = new();
}

/// <summary>One destination offered by a kafra. <see cref="OptionIndex"/> is the index the bot must
/// pass to <c>NpcSelectOptionAsync</c> in the destination menu (the second menu after picking
/// "Teleport Service" in the kafra's first menu).</summary>
public sealed class KafraWarp
{
    public string DestMap { get; set; } = "";
    public int DestX { get; set; }
    public int DestY { get; set; }
    public int Zeny { get; set; }
    public string OptionLabel { get; set; } = "";
    public int OptionIndex { get; set; }
}

public sealed class KafraDbFile
{
    public List<KafraEntry> Items { get; set; } = new();
}
