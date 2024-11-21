using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Simulation;
using System.Diagnostics;
using RebuildSharedData.Enum.EntityStats;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.Respawn)]
public class PacketRespawn : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsConnectedAndInGame)
            return;

        Debug.Assert(connection.Player != null);
        Debug.Assert(connection.Character != null);
        Debug.Assert(connection.Character.Map != null);

        var inPlace = msg.ReadByte() == 1;

        var player = connection.Player;
        var ce = player.CombatEntity;
        var ch = connection.Character;

        if (player.InActionCooldown())
            return;
        
        var isDead = ch.State == CharacterState.Dead;
        
        ch.ResetState(true);
        ch.SetSpawnImmunity();

        ce.ClearDamageQueue();

        if (isDead)
        {
            var recoverHp = ce.GetStat(CharacterStat.MaxHp);
            ce.SetStat(CharacterStat.Hp, recoverHp);
        }
        else
            inPlace = false;

        var savePoint = player.SavePosition.MapName;
        var position = player.SavePosition.Position;
        if (!World.Instance.TryGetWorldMapByName(savePoint, out var targetMap))
        {
            World.Instance.TryGetWorldMapByName("prt_fild08", out targetMap);
            position = new Position(170, 367);
        }

        if (!inPlace)
        {
            player.ReturnToSavePoint();
            //player.AddActionDelay(CooldownActionType.Teleport);

            //if (ch.Map.Name == savePoint)
            //{
            //    if (!ch.Map.MapBounds.Contains(position) || !ch.Map.WalkData.IsCellWalkable(position))
            //        position = ch.Map.FindRandomPositionOnMap();
            //    ch.Map.TeleportEntity(ref connection.Entity, ch, position, CharacterRemovalReason.OutOfSight);
            //}
            //else
            //{
            //    if (!targetMap.MapBounds.Contains(position) || !targetMap.WalkData.IsCellWalkable(position))
            //        position = targetMap.FindRandomPositionOnMap();
            //    ch.Map.World.MovePlayerMap(ref connection.Entity, ch, targetMap, position);
            //}
        }
        else
        {
            player.AddActionDelay(CooldownActionType.Click);

            ch.Map.AddVisiblePlayersAsPacketRecipients(ch);
            CommandBuilder.SendPlayerResurrection(ch);
            CommandBuilder.ClearRecipients();
        }
    }
}