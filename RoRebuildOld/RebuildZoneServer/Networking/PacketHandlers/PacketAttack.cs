using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using Lidgren.Network;
using RebuildData.Server.Config;
using RebuildData.Server.Logging;
using RebuildData.Shared.Enum;
using RebuildData.Shared.Networking;
using RebuildZoneServer.EntityComponents;

namespace RebuildZoneServer.Networking.PacketHandlers
{
	class PacketAttack : ClientPacketHandler
	{
		public override PacketType PacketType => PacketType.Attack;

		public override void Process(InboundMessage msg)
        {
			var id = msg.ReadInt32();

			if (connection.Character.State == CharacterState.Sitting ||
			    connection.Character.State == CharacterState.Dead)
				return;
			
			var target = State.World.GetEntityById(id);

			if (target.IsNull() || !target.IsAlive())
				return;

			var targetCharacter = target.Get<Character>();
			if (targetCharacter == null)
				return;

			if (targetCharacter.Map != connection.Character.Map)
				return;

			if (connection.Character.Position.SquareDistance(targetCharacter.Position) > ServerConfig.MaxViewDistance)
				return;

            var ce = target.Get<CombatEntity>();
			if(!ce.IsValidTarget(connection.Player.CombatEntity))
                return;

			connection.Player.TargetForAttack(targetCharacter);
		}
	}
}
