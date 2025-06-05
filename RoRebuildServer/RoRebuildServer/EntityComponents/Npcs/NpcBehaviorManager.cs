using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace RoRebuildServer.EntityComponents.Npcs;

[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public interface INpcLoader
{
    public void Load();
}



public class NpcBehaviorManager
{
    public Dictionary<string, List<NpcSpawnDefinition>> NpcSpawnsForMaps { get; } = new();
    public Dictionary<string, NpcBehaviorBase> EventBehaviorLookup { get; } = new();

    public void RegisterNpc(string name, string map, string? signalName, int spriteId, int x, int y, Direction facing, int w, int h, bool hasInteract, bool hasTouch, NpcBehaviorBase behavior)
    {
        var shortName = name;
        if (name.Contains("#"))
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
            Behavior = behavior,
            DisplayType = spriteId == 1000 ? CharacterDisplayType.Portal : CharacterDisplayType.None,
        };

        if (!NpcSpawnsForMaps.ContainsKey(map))
            NpcSpawnsForMaps.Add(map, new List<NpcSpawnDefinition>());

        NpcSpawnsForMaps[map].Add(npc);
    }

    public void RegisterEvent(string name, NpcBehaviorBase behavior)
    {
        EventBehaviorLookup.Add(name, behavior);
    }
}