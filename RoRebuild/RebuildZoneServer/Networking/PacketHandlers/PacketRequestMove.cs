using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RebuildData.Server.Logging;
using RebuildData.Shared.Data;
using RebuildData.Shared.Enum;
using RebuildData.Shared.Networking;
using RebuildZoneServer.Util;

namespace RebuildZoneServer.Networking.PacketHandlers
{
    public class PacketRequestMove : ClientPacketHandler
    {
        public override PacketType PacketType => PacketType.RequestMove;

        public override void Process(InboundMessage msg)
        {
             if (!NetworkManager.QuickMoveEnabled)
                return;

             var player = connection.Player;
             var ce = player.CombatEntity;
             var ch = connection.Character;

             var mapName = msg.ReadString();
             var posX = msg.ReadInt16();
             var posY = msg.ReadInt16();

             if (!ch.Map.World.TryGetMapByName(mapName, out var map))
             {
                 CommandBuilder.SendRequestFailed(player, ClientErrorType.UnknownMap);
                 return;
             }

             if (player.InActionCooldown())
             {
                 CommandBuilder.SendRequestFailed(player, ClientErrorType.TooManyRequests);
                 return;
             }

             player.AddActionDelay(2f); //block character input for 1+ seconds.
             ch.ResetState();
             ch.SpawnImmunity = 5f;

             ce.ClearDamageQueue();
             //ce.Stats.Hp = ce.Stats.MaxHp;
            
            var pos = new Position(posX, posY);
            if (pos.IsValid())
            {
                if (!map.WalkData.IsPositionInBounds(pos) || !map.WalkData.IsCellWalkable(pos))
                {
                    CommandBuilder.SendRequestFailed(player, ClientErrorType.InvalidCoordinates);
                    return;
                }
            }
            else
                pos = map.WalkData.FindWalkdableCellOnMap(); //find a random cell if one wasn't requested

            //CommandBuilder.SendHealSingle(player, 0, HealType.None); //heal amount is 0, but we set hp to max so it will update without the effect

             if (ch.Map.Name == mapName)
                 ch.Map.TeleportEntity(ref connection.Entity, ch, pos, false, CharacterRemovalReason.OutOfSight);
             else
                 ch.Map.World.MovePlayerMap(ref connection.Entity, ch, mapName, pos);
        }

    }
}
