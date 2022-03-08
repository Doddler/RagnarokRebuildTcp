using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.Simulation;

public class Chunk
{
    public int X;
    public int Y;
    public int WalkableTiles;

    public int MapX => X * 8;
    public int MapY => Y * 8;

    public EntityList AllEntities { get; set; } = new EntityList(32);
    public EntityList Players { get; set; } = new EntityList(16);
    public EntityList Monsters { get; set; } = new EntityList(16);

    public List<AreaOfEffect> AreaOfEffects = new List<AreaOfEffect>(16);

    public void AddEntity(ref Entity entity, CharacterType type)
    {
#if DEBUG
        //sanity check
        var character = entity.Get<WorldObject>();
        var chunkX = character.Position.X / 8;
        var chunkY = character.Position.Y / 8;
        if (X != chunkX || Y != chunkY)
            throw new Exception("Sanity check failed: Entity added to incorrect chunk?");
#endif
        AllEntities.Add(ref entity);

        switch (type)
        {
            case CharacterType.Player:
                Players.Add(ref entity);
                break;
            case CharacterType.Monster:
                Monsters.Add(ref entity);
                break;
            case CharacterType.NPC:
                break;
            default:
                throw new Exception("Unhandled character type: " + type);
        }
    }

    public bool RemoveEntity(ref Entity entity, CharacterType type)
    {
        AllEntities.Remove(ref entity);

        switch (type)
        {
            case CharacterType.Player:
                return Players.Remove(ref entity);
            case CharacterType.Monster:
                return Monsters.Remove(ref entity);
            case CharacterType.NPC:
                return true;
            default:
                throw new Exception("Unhandled character type: " + type);
        }
    }

    public void VerifyChunkData()
    {
        foreach (var entity in AllEntities)
        {
            var character = entity.Get<WorldObject>();
            var chunkX = character.Position.X / 8;
            var chunkY = character.Position.Y / 8;
            if (X != chunkX || Y != chunkY)
                throw new Exception("Sanity check failed: Entity added to incorrect chunk?");
        }
    }
}