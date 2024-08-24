using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking.PacketHandlers.Admin;

//NOTE! This is a regular client packet handler rather than an admin one
[ClientPacketHandler(PacketType.AdminRequestMove)]
public class PacketAdminRequestMove : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsPlayerAlive)
            return;

        if (!connection.IsAdmin && !ServerConfig.DebugConfig.EnableWarpCommandForEveryone)
            return;

        var player = connection.Player;
        var ce = player.CombatEntity;
        var ch = connection.Character;

        var mapName = msg.ReadString();
        var posX = msg.ReadInt16();
        var posY = msg.ReadInt16();
        var force = msg.ReadBoolean();

        ServerLogger.Log($"Player {connection.Player.Name} requested move to map {mapName}.");

        if (!ch.Map.World.TryGetWorldMapByName(mapName, out var map))
        {
            CommandBuilder.SendRequestFailed(player, ClientErrorType.UnknownMap);
            return;
        }

        if (player.InActionCooldown())
        {
            CommandBuilder.SendRequestFailed(player, ClientErrorType.TooManyRequests);
            return;
        }

        if (player.IsInNpcInteraction)
            return;

        player.AddActionDelay(2f); //block character input for 1+ seconds.
        ch.ResetState();
        ch.SetSpawnImmunity();

        ce.ClearDamageQueue();
        //ce.Stats.Hp = ce.Stats.MaxHp;

        var pos = new Position(posX, posY);
        if (!force)
        {
            if (pos.IsValid())
            {
                if (!map.WalkData.IsPositionInBounds(pos) || !map.WalkData.IsCellWalkable(pos))
                {
                    CommandBuilder.SendRequestFailed(player, ClientErrorType.InvalidCoordinates);
                    return;
                }
            }
            else
                pos = map.WalkData.FindWalkableCellOnMap(); //find a random cell if one wasn't requested
        }
        else
        {
            pos.ClampToArea(map.MapBounds.Shrink(4, 4));
        }

        //CommandBuilder.SendHealSingle(player, 0, HealType.None); //heal amount is 0, but we set hp to max so it will update without the effect
    
        if (ch.Map.Name == mapName)
            ch.Map.TeleportEntity(ref connection.Entity, ch, pos, CharacterRemovalReason.OutOfSight);
        else
            ch.Map.World.MovePlayerMap(ref connection.Entity, ch, map, pos);
    
    }
}