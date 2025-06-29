using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Character;

namespace RoRebuildServer.Networking.PacketHandlers.NPCPackets;

[ClientPacketHandler(PacketType.NpcRefineSubmit)]
public class PacketNpcRefineSubmit : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        var player = connection.Player;
        var map = player?.Character.Map;

        if (player == null || !player.IsInNpcInteraction || map == null)
            return;

        var recipients = player.Character.GetVisiblePlayerList();

        if (player.NpcInteractionState.InteractionResult != NpcInteractionResult.WaitForRefine || recipients == null)
            return;

        var bagId = msg.ReadInt32();
        var oreId = msg.ReadInt32();
        var catalystId = msg.ReadInt32();

        if (player.Inventory == null || !player.Inventory.GetItem(bagId, out var targetItem) || targetItem.Type != ItemType.UniqueItem)
            goto OnError;

        var result = EquipmentRefineSystem.AttemptRefineItem(player, ref targetItem, oreId, catalystId);
        if (result == RefineSuccessResult.FailedIncorrectRequirements)
            goto OnError;
        if (result == RefineSuccessResult.FailedCatalystMismatch)
        {
            CommandBuilder.ErrorMessage(player, $"Failed to refine, the selected catalyst is incompatible.");
            return;
        }
        
        var isEquipped = player.Equipment.IsItemEquipped(bagId);
        if(isEquipped)
            player.Equipment.UnEquipItem(bagId);
        
        CommandBuilder.AddRecipients(recipients);

        if (result == RefineSuccessResult.FailedNoChange || result == RefineSuccessResult.FailedWithLevelDown)
            CommandBuilder.SendEffectOnCharacterMulti(player.Character, DataManager.EffectIdForName["RefineFailure"]);
        else
            CommandBuilder.SendEffectOnCharacterMulti(player.Character, DataManager.EffectIdForName["RefineSuccess"]);

        CommandBuilder.ClearRecipients();

        CommandBuilder.SendUpdateZeny(player);

        if (result == RefineSuccessResult.Success || result == RefineSuccessResult.FailedWithLevelDown)
        {
            player.Inventory.UpdateUniqueItemReference(bagId, targetItem.UniqueItem);
            CommandBuilder.PlayerUpdateInventoryItemState(player, bagId, targetItem.UniqueItem);

            if (isEquipped)
            {
                player.Equipment.EquipItem(bagId);
                player.UpdateStats(false);
            }
        }

        return;
    OnError:
        CommandBuilder.ErrorMessage(player, $"Failed to refine, missing one or more requirement(s).");
    }
}