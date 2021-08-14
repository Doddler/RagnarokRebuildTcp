using System.Collections.Generic;
using Leopotam.Ecs;
using Lidgren.Network;
using RebuildData.Server.Data.Character;
using RebuildData.Shared.Enum;
using RebuildData.Shared.Networking;
using RebuildZoneServer.EntityComponents;
using RebuildZoneServer.Util;

namespace RebuildZoneServer.Networking
{
	public static class CommandBuilder
	{
		private static List<NetworkConnection> recipients = new List<NetworkConnection>(10);

		public static void AddRecipient(EcsEntity e)
		{
			if (!e.IsAlive())
				return;

			var player = e.Get<Player>();
			recipients.Add(player.Connection);
		}

		public static void ClearRecipients()
		{
			recipients.Clear();
		}

		public static bool HasRecipients()
		{
			return recipients.Count > 0;
		}

		private static void WriteMoveData(Character c, OutboundMessage packet)
		{
			packet.Write(c.MoveSpeed);
			packet.Write(c.MoveCooldown);
			packet.Write((byte)c.TotalMoveSteps);
			packet.Write((byte)c.MoveStep);
			if (c.TotalMoveSteps > 0)
			{
				packet.Write(c.WalkPath[0]);

				var i = 1;

				//pack directions into 2 steps per byte
				while (i < c.TotalMoveSteps)
				{
					var b = (byte)((byte)(c.WalkPath[i] - c.WalkPath[i - 1]).GetDirectionForOffset() << 4);
					i++;
					if (i < c.TotalMoveSteps)
						b |= (byte)(c.WalkPath[i] - c.WalkPath[i - 1]).GetDirectionForOffset();
					i++;
					packet.Write(b);
				}
			}
		}

        private static void AddFullEntityData(OutboundMessage packet, Character c, bool isSelf = false)
        {
            packet.Write(c.Id);
            packet.Write((byte)c.Type);
            packet.Write((short)c.ClassId);
            packet.Write(c.Position);
            packet.Write((byte)c.FacingDirection);
            packet.Write((byte)c.State);
            if (c.Type == CharacterType.Monster || c.Type == CharacterType.Player)
            {
                var ce = c.Entity.Get<CombatEntity>();
                packet.Write((byte)ce.BaseStats.Level);
                packet.Write((ushort)ce.Stats.MaxHp);
                packet.Write((ushort)ce.Stats.Hp);
            }
            if (c.Type == CharacterType.Player)
            {
                var player = c.Entity.Get<Player>();
                packet.Write((byte)player.HeadFacing);
                packet.Write(player.HeadId);
                packet.Write(player.IsMale);
                packet.Write(player.Name);
            }
            if (c.State == CharacterState.Moving)
            {
                WriteMoveData(c, packet);
            }
		}
		
		private static OutboundMessage BuildCreateEntity(Character c, bool isSelf = false)
		{
			var type = isSelf ? PacketType.EnterServer : PacketType.CreateEntity;
			var packet = NetworkManager.StartPacket(type, 256);

			AddFullEntityData(packet, c, isSelf);

			return packet;
		}

		public static void AttackMulti(Character attacker, Character target, DamageInfo di)
		{
			if (recipients.Count <= 0)
				return;

			var packet = NetworkManager.StartPacket(PacketType.Attack, 48);

			packet.Write(attacker.Id);
			packet.Write(target.Id);
			packet.Write((byte)attacker.FacingDirection);
			packet.Write(attacker.Position);
			packet.Write(di.Damage);

			NetworkManager.SendMessageMulti(packet, recipients);
		}

		public static void ChangeSittingMulti(Character c)
		{
			if (recipients.Count <= 0)
				return;

			var packet = NetworkManager.StartPacket(PacketType.SitStand, 48);

			packet.Write(c.Id);
			packet.Write(c.State == CharacterState.Sitting);

			NetworkManager.SendMessageMulti(packet, recipients);
		}

		public static void ChangeFacingMulti(Character c)
		{
			if (recipients.Count <= 0)
				return;

			var packet = NetworkManager.StartPacket(PacketType.LookTowards, 48);

			packet.Write(c.Id);
			packet.Write((byte)c.FacingDirection);
			if (c.Type == CharacterType.Player)
			{
				var player = c.Entity.Get<Player>();
				packet.Write((byte)player.HeadFacing);
			}

			NetworkManager.SendMessageMulti(packet, recipients);
		}
		
		public static void CharacterStopImmediateMulti(Character c)
		{
			if (recipients.Count <= 0)
				return;

			var packet = NetworkManager.StartPacket(PacketType.StopImmediate, 32);

			packet.Write(c.Id);
			packet.Write(c.Position);

			NetworkManager.SendMessageMulti(packet, recipients);
		}

		public static void CharacterStopMulti(Character c)
		{
			if (recipients.Count <= 0)
				return;

			var packet = NetworkManager.StartPacket(PacketType.StopAction, 32);

			packet.Write(c.Id);

			NetworkManager.SendMessageMulti(packet, recipients);
		}

		public static void SendMoveEntityMulti(Character c)
		{
			var packet = NetworkManager.StartPacket(PacketType.Move, 48);

			packet.Write(c.Id);
			packet.Write(c.Position);

			NetworkManager.SendMessageMulti(packet, recipients);
		}

		public static void SendStartMoveEntityMulti(Character c)
		{
			var packet = NetworkManager.StartPacket(PacketType.StartMove, 256);

			packet.Write(c.Id);
			WriteMoveData(c, packet);

			NetworkManager.SendMessageMulti(packet, recipients);
		}

		public static void InformEnterServer(Character c, Player p)
		{
			var packet = BuildCreateEntity(c, true);
			packet = NetworkManager.StartPacket(PacketType.EnterServer, 32);
			packet.Write(c.Id);
			packet.Write(c.Map.Name);
			NetworkManager.SendMessage(packet, p.Connection);
		}

		public static void SendCreateEntityMulti(Character c)
		{
			if (recipients.Count <= 0)
				return;

			var packet = BuildCreateEntity(c);
			NetworkManager.SendMessageMulti(packet, recipients);
		}

		public static void SendCreateEntity(Character c, Player player)
		{
			var packet = BuildCreateEntity(c);
			if (packet == null)
				return;

			NetworkManager.SendMessage(packet, player.Connection);
		}

		public static void SendRemoveEntityMulti(Character c, CharacterRemovalReason reason)
		{
			if (recipients.Count <= 0)
				return;

			var packet = NetworkManager.StartPacket(PacketType.RemoveEntity, 32);
			packet.Write(c.Id);
			packet.Write((byte)reason);

			NetworkManager.SendMessageMulti(packet, recipients);
		}

		public static void SendRemoveEntity(Character c, Player player, CharacterRemovalReason reason)
		{
			var packet = NetworkManager.StartPacket(PacketType.RemoveEntity, 32);
			packet.Write(c.Id);
			packet.Write((byte)reason);

			NetworkManager.SendMessage(packet, player.Connection);
		}

		public static void SendRemoveAllEntities(Player player)
		{
			var packet = NetworkManager.StartPacket(PacketType.RemoveAllEntities, 8);

			NetworkManager.SendMessage(packet, player.Connection);
		}

		public static void SendChangeMap(Character c, Player player)
		{
			var packet = NetworkManager.StartPacket(PacketType.ChangeMaps, 128);

			packet.Write(c.Map.Name);
			//packet.Write(c.Position);

			NetworkManager.SendMessage(packet, player.Connection);
		}

        public static void SendChangeTarget(Player p, Character target)
        {
            var packet = NetworkManager.StartPacket(PacketType.ChangeTarget, 32);

            packet.Write(target?.Id ?? 0);

            NetworkManager.SendMessage(packet, p.Connection);
        }

        public static void SendPlayerDeath(Character c)
        {
            if (recipients.Count <= 0)
                return;

            var packet = NetworkManager.StartPacket(PacketType.Death, 16);
            packet.Write(c.Id);
			packet.Write(c.Position);

            NetworkManager.SendMessageMulti(packet, recipients);
		}

		public static void SendHitMulti(Character c, float delayTime, int damage)
		{
			if (recipients.Count <= 0)
				return;

			var packet = NetworkManager.StartPacket(PacketType.HitTarget, 32);
			packet.Write(c.Id);
			packet.Write(delayTime);
			packet.Write(damage);

			NetworkManager.SendMessageMulti(packet, recipients);
		}

        public static void SendHealMulti(Player p, int healAmount, HealType type)
        {
            if (recipients.Count <= 0)
                return;

            var packet = NetworkManager.StartPacket(PacketType.HpRecovery, 32);
			packet.Write(p.Character.Id);
			packet.Write(healAmount);
            packet.Write(p.CombatEntity.Stats.Hp);
            packet.Write(p.CombatEntity.Stats.MaxHp);
            packet.Write((byte)type);

			NetworkManager.SendMessageMulti(packet, recipients);
		}

        public static void SendHealSingle(Player p, int healAmount, HealType type)
        {
            var packet = NetworkManager.StartPacket(PacketType.HpRecovery, 32);
            packet.Write(p.Character.Id);
            packet.Write(healAmount);
            packet.Write(p.CombatEntity.Stats.Hp);
            packet.Write(p.CombatEntity.Stats.MaxHp);
			packet.Write((byte)type);

			NetworkManager.SendMessage(packet, p.Connection);
		}

        public static void SendExpGain(Player p, int exp)
        {
            var packet = NetworkManager.StartPacket(PacketType.GainExp, 8);
			packet.Write(exp);

			NetworkManager.SendMessage(packet, p.Connection);
        }

        public static void SendRequestFailed(Player p, ClientErrorType error)
        {
            var packet = NetworkManager.StartPacket(PacketType.RequestFailed, 8);
			packet.Write((byte)error);

			NetworkManager.SendMessage(packet, p.Connection);
        }

        public static void LevelUp(Character c, int level)
        {
            if (recipients.Count <= 0)
                return;

            var packet = NetworkManager.StartPacket(PacketType.LevelUp, 8);
            packet.Write(c.Id);
            packet.Write((byte)level);

            NetworkManager.SendMessageMulti(packet, recipients);
		}
	}
}
