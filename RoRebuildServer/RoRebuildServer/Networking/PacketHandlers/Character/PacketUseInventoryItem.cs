using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation;
using System.Diagnostics;
using RoRebuildServer.EntityComponents.Character;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.UseInventoryItem)]
public class PacketUseInventoryItem : IClientPacketHandler
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

        var character = connection.Character;
        var player = connection.Player;

        var itemId = msg.ReadInt32();

        if (player.CombatEntity.HasBodyState(BodyStateFlags.Hidden))
            return;

        //obviously you should check if the item is in your inventory, but we have no inventory!

        player.AddActionDelay(CooldownActionType.UseItem);
        
        if (!DataManager.ItemList.TryGetValue(itemId, out var item) || !DataManager.UseItemInfo.TryGetValue(itemId, out var useInfo))
        {
            ServerLogger.LogError($"User is attempting to use invalid item id {itemId}. Due to the error, the player will be disconnected.");
            NetworkManager.DisconnectPlayer(connection);
            return;
        }

        if (item.ItemClass != ItemClass.Useable)
        {
            ServerLogger.LogWarning($"User is attempting to use item {item.Code}, but it is not usable.");
            return;
        }

        if (useInfo.UseType == ItemUseType.NotUsable || item.Interaction == null)
        {
            CommandBuilder.ErrorMessage(player, $"This item is not currently usable.");
            return;
        }

        var targetedItem = false;

        if (!item.Interaction.OnValidate(character.Player, character.CombatEntity))
            return;

        if (useInfo.UseType == ItemUseType.UseOnAlly || useInfo.UseType == ItemUseType.UseOnEnemy)
        {
            var targetId = msg.ReadInt32();
            var targetEntity = World.Instance.GetEntityById(targetId);

            if (targetEntity.TryGet<CombatEntity>(out var target))
            {
                //this will return false if the use fails OR if the result is the player starts casting a skill
                if (!item.Interaction.OnUseTargeted(itemId, character.Player, character.CombatEntity, target))
                    return;
            }
        }

        if (!player.TryRemoveItemFromInventory(itemId, 1))
        {
            CommandBuilder.SkillFailed(player, SkillValidationResult.InsufficientItemCount);
            return;
        }

        if (useInfo.Effect >= 0)
        {
            character.Map.AddVisiblePlayersAsPacketRecipients(character);
            CommandBuilder.SendEffectOnCharacterMulti(character, useInfo.Effect);
            CommandBuilder.ClearRecipients();
        }

        if (!targetedItem)
            item.Interaction.OnUse(character.Player, character.CombatEntity);

        CommandBuilder.RemoveItemFromInventory(player, item.Id, 1);
    }
}