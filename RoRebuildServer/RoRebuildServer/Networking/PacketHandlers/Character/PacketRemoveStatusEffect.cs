using RebuildSharedData.Networking;
using System.Diagnostics;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.RemoveStatusEffect)]
public class PacketRemoveStatusEffect : IClientPacketHandler
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
        //var map = connection.Character.Map;

        var statusId = (CharacterStatusEffect)msg.ReadInt32();
        if (statusId <= CharacterStatusEffect.None || statusId >= CharacterStatusEffect.StatusEffectMax)
            return;

        if (!StatusEffectHandler.CanCancelStatusEffect(statusId))
            return;

        player.CombatEntity.RemoveStatusOfTypeIfExists(statusId);
    }
}