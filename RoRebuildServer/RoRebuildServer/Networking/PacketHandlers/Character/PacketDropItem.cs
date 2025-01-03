using RebuildSharedData.Networking;
using System.Diagnostics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Simulation.Items;

namespace RoRebuildServer.Networking.PacketHandlers.Character
{
    [ClientPacketHandler(PacketType.DropItem)]
    public class PacketDropItem : IClientPacketHandler
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

            var id = msg.ReadInt32();
            var count = (int)msg.ReadInt16();

            if (player.Inventory == null || count < 0) return;

            if (player.Equipment.IsItemEquipped(id))
                return;

            if (player.Inventory.RemoveItemByBagIdAndGetRemovedItem(id, count, out var removedItem))
            {
                var dropPos = map.GetRandomWalkablePositionInArea(Area.CreateAroundPoint(player.Character.Position, 2));
                var groundItem = new GroundItem(dropPos, ref removedItem);
                map.DropGroundItem(ref groundItem);

                CommandBuilder.RemoveItemFromInventory(player, id, count);
            }
            else
            {
                CommandBuilder.ErrorMessage(player, $"Failed to remove item from inventory.");
                return;
            }

        }
    }
}
