using RebuildSharedData.Networking;
using System.Diagnostics;
using RebuildSharedData.Enum;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.EquipUnequipGear)]
public class PacketEquipUnequipGear : IClientPacketHandler
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

        var bagId = msg.ReadInt32();
        var isEquip = msg.ReadBoolean();

        if (!isEquip)
        {
            connection.Player.Equipment.UnEquipItem(bagId);
            CommandBuilder.SendHealMultiAutoVis(connection.Character, 0, HealType.None);
            if (connection.Player.Party != null)
                CommandBuilder.UpdatePartyMembersOnMapOfHpSpChange(connection.Player);
            //connection.Player.UpdateStats();
            return;
        }
        
        switch (connection.Player.Equipment.EquipItem(bagId))
        {
            case EquipChangeResult.InvalidItem:
                CommandBuilder.ErrorMessage(connection.Player, "Item could not be equipped.");
                return;
            case EquipChangeResult.LevelTooLow:
                CommandBuilder.ErrorMessage(connection.Player, "Level too low to equip.");
                return;
            case EquipChangeResult.NotApplicableJob:
                CommandBuilder.ErrorMessage(connection.Player, "Your job is unable to equip this item.");
                return;
            case EquipChangeResult.InvalidPosition:
                CommandBuilder.ErrorMessage(connection.Player, "Cannot equip this item to this position.");
                return;
            case EquipChangeResult.AlreadyEquipped:
                return; //do nothing
            default:
                connection.Player.UpdateStats();
                CommandBuilder.SendHealMultiAutoVis(connection.Character, 0, HealType.None);
                if (connection.Player.Party != null)
                    CommandBuilder.UpdatePartyMembersOnMapOfHpSpChange(connection.Player);
                return; //if it succeeds we'll have already sent a response
        }

        
    }
}