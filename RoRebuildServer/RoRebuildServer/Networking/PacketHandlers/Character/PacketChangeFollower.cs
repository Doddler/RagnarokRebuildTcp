using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Networking;
using RoRebuildServer.Logging;
using System.Diagnostics;

namespace RoRebuildServer.Networking.PacketHandlers.Character;


[ClientPacketHandler(PacketType.ChangeFollower)]
public class PacketChangeFollower : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsPlayerAlive)
            return;

        Debug.Assert(connection.Player != null);
        Debug.Assert(connection.Character != null);
        Debug.Assert(connection.Character.Map != null);

        if (!connection.Player.CanPerformCharacterActions())
            return;

        //for now we assume the only use for ChangeFollower is to change your cart appearance (or remove it)

        var player = connection.Player;
        var map = connection.Character.Map;

        var id = msg.ReadInt32();
        var isRemove = id < 0;
        var isPecoChange = player.JobId == 7 || player.JobId == 13;

        if (isRemove)
        {
            //player.PlayerFollower &= ~PlayerFollower.AnyCart;
            player.PlayerFollower = 0; //this call will double for removing the bird too
            player.SetData(PlayerStat.FollowerType, 0);

            if (isPecoChange)
            {
                player.CombatEntity.RemoveStatusOfTypeIfExists(CharacterStatusEffect.PecoRiding); //this will call StopRidingMount as well, but we're already off the peco so it's fine.
                return;
            }
        }
        else
        {
            if (isPecoChange)
            {
                if (player.HasPeco)
                    return;

                player.StartRidingMount();
                return;
            }

            if (!player.HasCart)
                return; //can't change cart if they don't have a cart

            //the /changecart command gets a value 1-5, but the client will change the range to 0-4.
            if (id < 0 || id >= 5)
            {
                CommandBuilder.ErrorMessage(player, $"Change cart must be a value between 1 and 5.");
                ServerLogger.LogWarning(
                    $"Player {player.Name} trying to change cart to type {id}, but it is out of bounds.");
                return;
            }

            var minLvl = id switch
            {
                1 => 41,
                2 => 66,
                3 => 81,
                4 => 91,
                _ => 0
            };

            if (player.GetData(PlayerStat.Level) < minLvl)
            {
                CommandBuilder.ErrorMessage(player, $"You are too low level to equip push cart style {id + 1}.");
                return;
            }

            player.PlayerFollower &= ~PlayerFollower.AnyCart;
            player.SetData(PlayerStat.FollowerType, id);
        }

        CommandBuilder.UpdatePlayerFollowerStateAutoVis(player);

    }
}