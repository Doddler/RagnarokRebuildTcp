using RebuildSharedData.Data;
using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.Simulation.Util;

public enum MoveRequestType
{
    InitialSpawn,
    MapMove
}

public struct MapMoveRequest
{
    public Entity Player;
    public MoveRequestType MoveRequestType;
    public Map? SrcMap;
    public Map DestMap;
    public Position Position;

    public MapMoveRequest(Entity player, MoveRequestType moveRequestType, Map? srcMap, Map destMap, Position position)
    {
        Player = player;
        MoveRequestType = moveRequestType;
        SrcMap = srcMap;
        DestMap = destMap;
        Position = position;
    }
}