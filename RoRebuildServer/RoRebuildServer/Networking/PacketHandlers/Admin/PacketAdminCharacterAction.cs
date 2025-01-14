using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents.Character;

namespace RoRebuildServer.Networking.PacketHandlers.Admin;

[AdminClientPacketHandler(PacketType.AdminCharacterAction)]
public class PacketAdminCharacterAction : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsConnectedAndInGame || connection.Player == null)
            return;

        var action = (AdminCharacterAction)msg.ReadInt32();
        switch (action)
        {
            case AdminCharacterAction.RefineItem:
            {
                if (!connection.Player.CanPerformCharacterActions())
                    return;
                var bagId = msg.ReadInt32();
                var change = msg.ReadInt32();
                if (change < 0 || change > 20)
                    return;
                EquipmentRefineSystem.AdminItemUpgrade(connection.Player, bagId, change);
                break;
            }
        }
    }
}