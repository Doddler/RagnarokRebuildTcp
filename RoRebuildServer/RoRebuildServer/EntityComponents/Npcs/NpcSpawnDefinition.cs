using RebuildSharedData.Data;
using RebuildSharedData.Enum;

namespace RoRebuildServer.EntityComponents.Npcs;

public class NpcSpawnDefinition
{
    public required string Name { get; set; }
    public required string FullName { get; set; }
    public required string? SignalName { get; set; }
    public required int SpriteId { get; set; }
    public required Position Position { get; set; }
    public required Direction FacingDirection { get; set; }
    public required int Width { get; set; }
    public required int Height { get; set; }
    public required bool HasInteract { get; set; }
    public required bool HasTouch { get; set; }
    public required NpcBehaviorBase Behavior { get; set; }
}