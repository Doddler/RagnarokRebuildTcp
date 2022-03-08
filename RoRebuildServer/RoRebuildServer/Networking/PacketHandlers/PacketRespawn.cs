using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Simulation;

namespace RoRebuildServer.Networking.PacketHandlers;

[ClientPacketHandler(PacketType.Respawn)]
public class PacketRespawn : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (connection.Character == null || connection.Character.Map == null || connection.Character.State != CharacterState.Dead)
            return;

        var inPlace = msg.ReadByte() == 1;

        var player = connection.Player;
        var ce = player.CombatEntity;
        var ch = connection.Character;

        if (player.InActionCooldown())
            return;

        player.AddActionDelay(1.1f); //add 1s to the player's cooldown times. Should lock out immediate re-use.
        ch.ResetState();
        ch.SpawnImmunity = 5f;

        ce.ClearDamageQueue();
        ce.Stats.Hp = ce.Stats.MaxHp;

        CommandBuilder.SendHealSingle(player, 0, HealType.None); //heal amount is 0, but we set hp to max so it will update without the effect

        if (!inPlace)
        {
            if (ch.Map.Name == "prt_fild08")
                ch.Map.TeleportEntity(ref connection.Entity, ch, new Position(170, 367), false,
                    CharacterRemovalReason.OutOfSight);
            else
            {
                if(World.Instance.TryGetWorldMapByName("prt_fild08", out var dest))
                    ch.Map.World.MovePlayerMap(ref connection.Entity, ch, dest, new Position(170, 367));
            }
        }
        else
        {
            ch.Map.GatherPlayersForMultiCast(ref connection.Entity, ch);
            CommandBuilder.SendPlayerResurrection(ch);
            CommandBuilder.ClearRecipients();
        }
    }
}