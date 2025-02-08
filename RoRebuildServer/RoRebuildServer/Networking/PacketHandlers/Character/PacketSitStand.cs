using RebuildSharedData.Data;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation;
using System;
using RebuildSharedData.Enum;
using System.Diagnostics;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.SitStand)]
public class PacketSitStand : IClientPacketHandler
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

        connection.Player.AddActionDelay(CooldownActionType.SitStand);

        var isSitting = msg.ReadBoolean();

        if (isSitting && (connection.Player.JobId == 0 && connection.Player.MaxLearnedLevelOfSkill(CharacterSkill.BasicMastery) < 2))
        {
            CommandBuilder.ErrorMessage(connection.Player, $"You need level 2 of basic mastery to sit.");
            return;
        }

        connection.Character.SitStand(isSitting);
	}
}