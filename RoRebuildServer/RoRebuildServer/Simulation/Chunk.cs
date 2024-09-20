using System.Diagnostics.CodeAnalysis;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Simulation.Items;

namespace RoRebuildServer.Simulation;

public class Chunk
{
    public int Id;
    public int X;
    public int Y;
    public int WalkableTiles;
    public int Size;

    public int MapX => X * Size;
    public int MapY => Y * Size;

    public EntityList AllEntities { get; set; } = new EntityList(16, true);
    public EntityList Players { get; set; } = new EntityList(8, true);
    public EntityList Monsters { get; set; } = new EntityList(8, true);
    public GroundItemList GroundItems { get; set; } = new GroundItemList(8, true);
    public List<AreaOfEffect> AreaOfEffects { get; set; } = new List<AreaOfEffect>(2);

    public override string ToString() => $"Chunk ({X * Size},{Y*Size}-{(X+1)*Size},{(Y+1)*Size})[P:{Players.Count} M:{Monsters.Count} AoE:{AreaOfEffects.Count}]";

    public void AddEntity(ref Entity entity, CharacterType type)
    {
#if DEBUG
        //sanity check
        var character = entity.Get<WorldObject>();
        var chunkX = character.Position.X / Size;
        var chunkY = character.Position.Y / Size;
        if (X != chunkX || Y != chunkY)
            throw new Exception("Sanity check failed: Entity added to incorrect chunk?");
        if (type == CharacterType.Player && Players.Contains(entity))
            throw new Exception("Trying to add player a second time to a chunk!");
        if (type == CharacterType.Monster && Monsters.Contains(entity))
            throw new Exception("Trying to add a monster a second time to a chunk!");
        if (AllEntities.Contains(entity))
            throw new Exception("Somehow we're trying to add an entity to a chunk a second time!");
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
//#if DEBUG
//        if (!AllEntities.Contains(ref entity))
//            throw new Exception($"Cannot remove entity {entity} from chunk {this}!!");
//#endif
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

    public bool AddGroundItem(ref GroundItem item)
    {
        GroundItems.Add(item);
        return true;
    }

    public bool TryGetGroundItem(int itemId, out GroundItem item) => GroundItems.TryGet(itemId, out item);
    
    public bool RemoveGroundItem(int itemId)
    {
        return GroundItems.Remove(itemId);
    }

    public void VerifyChunkData()
    {
        foreach (var entity in AllEntities)
        {
            var character = entity.Get<WorldObject>();
            var chunkX = character.Position.X / Size;
            var chunkY = character.Position.Y / Size;
            if (X != chunkX || Y != chunkY)
                throw new Exception("Sanity check failed: Entity added to incorrect chunk?");
        }
    }
}