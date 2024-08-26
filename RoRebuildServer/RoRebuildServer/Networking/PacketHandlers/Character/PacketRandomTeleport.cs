using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.RandomTeleport)]
public class PacketRandomTeleport : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
		if (connection.Character == null || connection.Player == null || connection.Character.Map == null)
            return;

        var player = connection.Player;
        if (player.InActionCooldown())
        {
            ServerLogger.Debug("Player random teleport ignored due to cooldown.");
            return;
        }

        if (player.Character.State == CharacterState.Dead || player.Character.IsActive == false)
            return;

        if (player.IsInNpcInteraction)
            return;

        var ch = connection.Character;
        var map = ch.Map;
        
        var p = new Position();
        var count = 0;

        var area = map.MapBounds.Shrink(5, 5);

        do
        {
            p = area.RandomInArea();
            //p = new Position(GameRandom.NextInclusive(5, map.Width - 5), GameRandom.NextInclusive(5, map.Height - 5));
            count++;
        } while (!map.WalkData.IsCellWalkable(p) && count < 50);

        if (count >= 50)
        {
            ServerLogger.Log($"Failed to move player {player.Name}, random teleport could not find a valid cell to use on map {map.Name} within area {area}.");
            return;
        }

        ServerLogger.Log($"Player {player.Name} executes a random teleport from {ch.Position} to {p}");

        player.AddActionDelay(CooldownActionType.Teleport);
        ch.ResetState();
        ch.SetSpawnImmunity();
        map.TeleportEntity(ref connection.Entity, ch, p);
        CommandBuilder.SendExpGain(connection.Player, 0); //update their exp

        var ce = connection.Entity.Get<CombatEntity>();
        ce.ClearDamageQueue();
	}
}