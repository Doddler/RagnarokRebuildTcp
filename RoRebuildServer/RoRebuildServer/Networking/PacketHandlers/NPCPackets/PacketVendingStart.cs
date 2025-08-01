using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation;

namespace RoRebuildServer.Networking.PacketHandlers.NPCPackets;

[ClientPacketHandler(PacketType.VendingStart)]
public class PacketVendingStart : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsConnectedAndInGame)
            return;

        var character = connection.Character;

        if (character == null || connection.Player == null
                              //|| character.State == CharacterState.Sitting
                              || character.State == CharacterState.Dead
                              || character.Player.IsInNpcInteraction)
            return;

        if (!character.Player.CanPerformCharacterActions() && !character.CombatEntity.HasBodyState(BodyStateFlags.Hidden))
            return;

        var player = character.Player;
        var map = character.Map!;

        if (!player.HasCart || player.CartInventory == null)
        {
            CommandBuilder.SkillFailed(player, SkillValidationResult.CartRequired);
            return;
        }
        
        var vendName = msg.ReadString();
        if (string.IsNullOrWhiteSpace(vendName) || vendName.Length > 32)
        {
            CommandBuilder.SkillFailed(player, SkillValidationResult.VendFailedNameNotValid);
            return;
        }

        if (map.CheckIfNpcNearby(character, 4))
        {
            CommandBuilder.SkillFailed(player, SkillValidationResult.VendFailedTooCloseToNpc);
            return;
        }
        
        player.VendingState ??= new VendingState();
        var vend = player.VendingState;
        vend.SellingItems.Clear();
        vend.SellingItemValues.Clear();

        var itemCount = msg.ReadInt32();
        if (itemCount <= 0)
        {
            CommandBuilder.SkillFailed(player, SkillValidationResult.VendFailedGenericError);
            return;
        }

        if (itemCount > player.MaxLearnedLevelOfSkill(CharacterSkill.Vending) + 2)
        {
            CommandBuilder.SkillFailed(player, SkillValidationResult.VendFailedTooManyItems);
            return;
        }

        for (var i = 0; i < itemCount; i++)
        {
            var bagId = msg.ReadInt32();
            var count = msg.ReadInt32();
            var price = msg.ReadInt32();

            if (price < 0 || price > 9_999_999)
            {
                CommandBuilder.SkillFailed(player, SkillValidationResult.VendFailedInvalidPrice);
                return;
            }

            if (!player.CartInventory.GetItem(bagId, out var item) || item.Count < count)
            {
                CommandBuilder.SkillFailed(player, SkillValidationResult.VendFailedItemsNotPreset);
                return;
            }

            item.Count = count;

            vend.SellingItems.Add(bagId, item);
            vend.SellingItemValues.Add(bagId, price);
        }

        //To handle vending state, we create an NPC on the player tile that has the VendingProxy type and name equal to the shop title.
        //The player is held in an interaction state with this proxy npc, ensuring they can't move, perform actions, move or use items.
        //Other players will automatically be notified of vending shops when the proxy entity comes into view with the vending player.
        //VendingProxy type events are unique in that the client is also sent the owner ID, which lets clients attach the shop to the right player.
        //Another point of using a proxy like this is that we want interactables (vends, chats, etc.) to also be assignable to npcs in the future.

        var e = World.Instance.CreateEvent(character.Entity, map, "VendNpcProxy", character.Position, 0, 0, 0, 0, null);
        if (!e.TryGet<Npc>(out var proxy))
        {
            ServerLogger.LogWarning($"Failed to create vend proxy.");
            CommandBuilder.ErrorMessage(player, "Failed to open vending shop.");
            return;
        }

        proxy.ChangeNpcClass("EFFECT");
        proxy.Name = vendName;
        proxy.HasInteract = true;
        proxy.ExpireEventWithoutOwner = true;
        proxy.DisplayType = NpcDisplayType.VendingProxy;
        proxy.RevealToPlayers();

        proxy.OnInteract(player);

        vend.VendProxy = proxy.Entity;

        CommandBuilder.VendingStart(player, vendName);
    }
}