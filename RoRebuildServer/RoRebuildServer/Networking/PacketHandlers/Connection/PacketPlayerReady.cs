using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking.PacketHandlers.Connection;

[ClientPacketHandler(PacketType.PlayerReady)]
public class PacketPlayerReady : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (connection.Character == null || connection.Character.Map == null || connection.Player == null)
            return;

        if (connection.Character.IsActive)
            throw new Exception($"Woah! A player {connection.Character.Name} is trying to send a PlayerReady packet while they're already ready!");

        connection.Character.Map.ActivatePlayerAndNotifyNearby(connection.Player, CreateEntityEventType.EnterServer);

        //connection.Character.IsActive = true;
        //connection.Character.Map.SendAllEntitiesToPlayer(ref connection.Entity);
        //connection.Character.Map.SendAddEntityAroundCharacter(ref connection.Entity, connection.Character);

        CommandBuilder.SendExpGain(connection.Player, 0); //update their exp

        connection.Character.SetSpawnImmunity();
        connection.Player.ResetRegenTickTime();
        if (connection.Player.Party != null)
        {
            if (!connection.Player.HasEnteredServer)
                CommandBuilder.AcceptPartyInvite(connection.Player, true);
            CommandBuilder.UpdatePartyMembersOfMapChange(connection.Player, connection.Character.Map.Name);
            CommandBuilder.UpdatePartyMembersOnMapOfHpSpChange(connection.Player, false);
        }

        connection.Player.HasEnteredServer = true;

        ServerLogger.Debug($"Player {connection.Entity} finished loading, spawning him on {connection.Character.Map.Name} at position {connection.Character.Position}.");
    }
}