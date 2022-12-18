using RebuildSharedData.Data;
using RebuildSharedData.Enum;

namespace RoRebuildServer.EntityComponents.Npcs;

public interface INpcLoader
{
    public void Load();
}

public class NpcBehaviorManager
{
    public Dictionary<string, List<NpcSpawnDefinition>> NpcSpawnsForMaps = new();
    public Dictionary<string, NpcBehaviorBase> EventBehaviorLookup = new();

    public void RegisterNpc(string name, string map, string? signalName, int spriteId, int x, int y, Direction facing, int w, int h, bool hasInteract, bool hasTouch, NpcBehaviorBase behavior)
    {
        var shortName = name;
        if(name.Contains("#"))
            shortName = name.Substring(0, name.IndexOf("#", StringComparison.Ordinal));

        var npc = new NpcSpawnDefinition()
        {
            FullName = name,
            Name = shortName,
            SignalName = signalName,
            SpriteId = spriteId,
            Position = new Position(x, y),
            FacingDirection = facing,
            Width = w,
            Height = h,
            HasInteract = hasInteract,
            HasTouch = hasTouch,
            Behavior = behavior
        };

        if(!NpcSpawnsForMaps.ContainsKey(map))
            NpcSpawnsForMaps.Add(map, new List<NpcSpawnDefinition>());

        NpcSpawnsForMaps[map].Add(npc);
    }

    public void RegisterEvent(string name, NpcBehaviorBase behavior)
    {
        EventBehaviorLookup.Add(name, behavior);
    }
}