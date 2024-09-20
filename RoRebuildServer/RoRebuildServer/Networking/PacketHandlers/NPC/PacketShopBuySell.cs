using RebuildSharedData.Networking;
using System.Diagnostics;
using RebuildSharedData.Enum;

namespace RoRebuildServer.Networking.PacketHandlers.NPC
{
    [ClientPacketHandler(PacketType.ShopBuySell)]
    public class PacketShopBuySell : IClientPacketHandler
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
            var map = connection.Character.Map;

            if (player is not { IsInNpcInteraction: true }
                || player.NpcInteractionState.InteractionResult != NpcInteractionResult.WaitForShop)
                return;


        }
    }
}
