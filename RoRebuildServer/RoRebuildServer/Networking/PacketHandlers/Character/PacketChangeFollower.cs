using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;
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

        var player = connection.Player;

        switch ((FollowerChange)msg.ReadByte())
        {
            case FollowerChange.CartStyle:
                ChangeCartStyle(player, msg.ReadInt32());
                return;
            case FollowerChange.RemoveCart:
                player.RemoveFollower(CharacterFollowerState.AnyCart);
                return;
            case FollowerChange.RemoveFalcon:
                player.RemoveFollower(CharacterFollowerState.Falcon);
                return;
            case FollowerChange.RemovePeco:
                player.RemoveFollower(CharacterFollowerState.Mounted);
                return;
        }
    }

    private static void ChangeCartStyle(Player player, int style)
    {
        if (!player.HasCart)
            return; //can't change cart if they don't have a cart

        //the command counts from 1, the client from 0
        var styleCount = CharacterFollowerStateExtensions.CartStyleCount;
        if (style < 0 || style >= styleCount)
        {
            CommandBuilder.ErrorMessage(player, $"Change cart must be a value between 1 and {styleCount}.");
            ServerLogger.LogWarning($"Player {player.Name} trying to change cart to type {style}, but it is out of bounds.");
            return;
        }

        if (player.GetData(PlayerStat.Level) < CharacterFollowerStateExtensions.CartStyleMinLevel(style))
        {
            CommandBuilder.ErrorMessage(player, $"You are too low level to equip push cart style {style + 1}.");
            return;
        }

        player.SetCart(style);
    }
}