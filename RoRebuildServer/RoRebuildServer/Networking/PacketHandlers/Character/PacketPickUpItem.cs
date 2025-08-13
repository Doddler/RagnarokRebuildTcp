using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents.Util;
using System.Diagnostics;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Networking.PacketHandlers.Character
{
    [ClientPacketHandler(PacketType.PickUpItem)]
    public class PacketPickUpItem : IClientPacketHandler
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

            if (!map.TryGetGroundItemByDropId(id, out var item))
            {
                //ServerLogger.LogWarning($"Player {player.Character.Name} trying to pick up item {id} but it does not currently exist on map {map.Name}.");
                CommandBuilder.RemoveDropItemForSinglePlayerByGroundId(id, player); //remove the item for the client, it shouldn't exist
                return;
            }

            if (player.Character.Position.SquareDistance(item.Position) <= 1)
            {
                if (player.Character.InAttackCooldown) //yes, we use attack cooldown to throttle pickup speed
                {
                    player.Character.QueuedAction = QueuedAction.PickUpItem;
                    player.Character.ItemTarget = id;
                    return;
                }
                player.Character.StopMovingImmediately();
                if(player.TryPickup(item))
                    connection.Player.AddInputActionDelay(InputActionCooldownType.PickUpItem);
                else
                    CommandBuilder.ErrorMessage(player, "Your inventory is too full to pick up this item.");
            }
            else
            {
                if (player.Character.TryMove(item.Position, 1))
                {
                    player.Character.QueuedAction = QueuedAction.PickUpItem;
                    player.Character.ItemTarget = id;
                    player.ClearTarget();
                }
            }
        }
    }
}
