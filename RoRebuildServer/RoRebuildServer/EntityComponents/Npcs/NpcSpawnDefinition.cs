using RebuildSharedData.Data;
using RebuildSharedData.Enum;

namespace RoRebuildServer.EntityComponents.Npcs
{
    public class NpcSpawnDefinition
    {
        public string Name { get; set; }
        public string FullName { get; set; }
        public int SpriteId { get; set; }
        public Position Position { get; set; }
        public Direction FacingDirection { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool HasInteract { get; set; }
        public bool HasTouch { get; set; }
        public NpcBehaviorBase Behavior { get; set; }
    }
}
