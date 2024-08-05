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

        if (!inPlace)
        {
            if (ch.Map.Name == "prt_fild08")
                ch.Map.TeleportEntity(ref connection.Entity, ch, new Position(170, 367), CharacterRemovalReason.OutOfSight);
            else
            {
                if(World.Instance.TryGetWorldMapByName("prt_fild08", out var dest))
                    ch.Map.World.MovePlayerMap(ref connection.Entity, ch, dest, new Position(170, 367));
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