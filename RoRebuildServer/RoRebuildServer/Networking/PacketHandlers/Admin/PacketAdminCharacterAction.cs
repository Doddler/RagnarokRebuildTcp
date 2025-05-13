using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;

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
            case AdminCharacterAction.ApplyStatus:
            {
                if (!connection.Player.CanPerformCharacterActions())
                    return;
                var status = (CharacterStatusEffect)msg.ReadByte();
                var duration = msg.ReadFloat();
                var v1 = msg.ReadInt32();
                var v2 = msg.ReadInt32();
                var v3 = msg.ReadInt16();
                var v4 = msg.ReadByte();

                if (duration <= 0)
                    duration = StatusEffectHandler.GetDefaultDuration(status);

                var effect = StatusEffectState.NewStatusEffect(status, duration, v1, v2, v3, v4);
                connection.Player.CombatEntity.AddStatusEffect(effect);
                break;
            }
        }
    }
}