using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.UseInventoryItem)]
public class PacketUseInventoryItem : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        var character = connection.Character;

        if (character == null
            || character.Map == null
            || character.Player == null
            || character.State == CharacterState.Sitting
            || character.State == CharacterState.Dead
            || character.Player.IsInNpcInteraction)
            return;

        var itemId = msg.ReadInt32();

        //obviously you should check if the item is in your inventory, but we have no inventory!


        if (character.Player.InActionCooldown())
            return;
        character.Player.AddActionDelay(CooldownActionType.UseItem);

        if (!DataManager.ItemList.TryGetValue(itemId, out var item))
        {
            ServerLogger.LogError($"User is attempting to use invalid item id {itemId}. Due to the error, the player will be disconnected.");
            NetworkManager.DisconnectPlayer(connection);
            return;
        }

        if (!item.IsUseable)
        {
            ServerLogger.LogWarning($"User is attempting to use item {item.Code}, but it is not usable.");
            return;
        }

        if (item.Interaction == null)
            return;

        if (item.Effect >= 0)
        {
            character.Map.GatherPlayersForMultiCast(ref character.Entity, character);
            CommandBuilder.SendEffectOnCharacterMulti(character, item.Effect);
            CommandBuilder.ClearRecipients();
        }

        item.Interaction.OnUse(character.Player, character.CombatEntity);
    }
}