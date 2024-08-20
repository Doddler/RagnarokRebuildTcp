using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.Respawn)]
public class PacketRespawn : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (connection.Character == null || connection.Player == null || connection.Character.Map == null || connection.Character.State != CharacterState.Dead)
            return;

        var inPlace = msg.ReadByte() == 1;

        var player = connection.Player;
        var ce = player.CombatEntity;
        var ch = connection.Character;

        if (player.InActionCooldown())
            return;

        player.AddActionDelay(1.1f); //add 1s to the player's cooldown times. Should lock out immediate re-use.
        ch.ResetState(true);
        ch.SetSpawnImmunity();

        ce.ClearDamageQueue();
        ce.SetStat(CharacterStat.Hp, ce.GetStat(CharacterStat.MaxHp));

        CommandBuilder.SendHealSingle(player, 0, HealType.None); //heal amount is 0, but we set hp to max so it will update without the effect

        var savePoint = player.SavePosition.MapName;
        var position = player.SavePosition.Position;
        if (!World.Instance.TryGetWorldMapByName(savePoint, out var targetMap))
        {
            World.Instance.TryGetWorldMapByName("prt_fild08", out targetMap);
            position = new Position(170, 367);
        }

        if (!inPlace && targetMap != null)
        {
            if (ch.Map.Name == savePoint)
            {
                if (!ch.Map.MapBounds.Contains(position) || !ch.Map.WalkData.IsCellWalkable(position))
                    position = ch.Map.FindRandomPositionOnMap();
                ch.Map.TeleportEntity(ref connection.Entity, ch, position, CharacterRemovalReason.OutOfSight);
            }
            else
            {
                if (!targetMap.MapBounds.Contains(position) || !targetMap.WalkData.IsCellWalkable(position))
                    position = targetMap.FindRandomPositionOnMap();
                ch.Map.World.MovePlayerMap(ref connection.Entity, ch, targetMap, position);
            }

        }
        else
        {
            ch.Map.AddVisiblePlayersAsPacketRecipients(ch);
            CommandBuilder.SendPlayerResurrection(ch);
            CommandBuilder.ClearRecipients();
        }
    }
}