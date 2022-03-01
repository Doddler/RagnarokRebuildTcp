using System;
using System.Collections.Generic;
using System.Text;
using Lidgren.Network;
using RebuildData.Server.Config;
using RebuildData.Server.Logging;
using RebuildData.Shared.Data;
using RebuildData.Shared.Enum;
using RebuildData.Shared.Networking;
using RebuildZoneServer.Data.Management;
using RebuildZoneServer.EntityComponents;
using RebuildZoneServer.Util;

namespace RebuildZoneServer.Networking.PacketHandlers
{
	class PacketRandomTeleport : ClientPacketHandler
	{
		public override PacketType PacketType => PacketType.RandomTeleport;

        public override void Process(InboundMessage msg)
		{
			if (connection.Character == null)
				return;

			var player = connection.Entity.Get<Player>();
			if (player.InActionCooldown())
			{
				ServerLogger.Debug("Player stop action ignored due to cooldown.");
				return;
			}

            if (player.Character.State == CharacterState.Dead)
                return;

			var ch = connection.Character;
			var map = ch.Map;

			var p = new Position();

			do
			{
				p = new Position(GameRandom.Next(0, map.Width - 1), GameRandom.Next(0, map.Height - 1));
			} while (!map.WalkData.IsCellWalkable(p));
			
			player.AddActionDelay(1.1f); //add 1s to the player's cooldown times. Should lock out immediate re-use.
			ch.ResetState();
			ch.SpawnImmunity = 5f;
			map.TeleportEntity(ref connection.Entity, ch, p);

			var ce = connection.Entity.Get<CombatEntity>();
			ce.ClearDamageQueue();
		}
	}
}
