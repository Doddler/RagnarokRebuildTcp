using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RebuildData.Shared.Data;
using RebuildData.Shared.Enum;
using RebuildData.Shared.Networking;
using RebuildZoneServer.EntityComponents;

namespace RebuildZoneServer.Networking.PacketHandlers
{
    public class PacketRespawn : ClientPacketHandler
    {
        public override PacketType PacketType => PacketType.Respawn;

        public override void Process(InboundMessage msg)
        {
            if (connection.Character.State != CharacterState.Dead)
                return;

            var player = connection.Player;
            var ce = player.CombatEntity;
            var ch = connection.Character;

            if (player.InActionCooldown())
                return;
            
            player.AddActionDelay(1.1f); //add 1s to the player's cooldown times. Should lock out immediate re-use.
            ch.ResetState();
            ch.SpawnImmunity = 5f;

            ce.ClearDamageQueue();
            ce.Stats.Hp = ce.Stats.MaxHp;

            CommandBuilder.SendHealSingle(player, 0, HealType.None); //heal amount is 0, but we set hp to max so it will update without the effect

            if (ch.Map.Name == "prt_fild08")
                ch.Map.TeleportEntity(ref connection.Entity, ch, new Position(170, 367), false, CharacterRemovalReason.OutOfSight);
            else
                ch.Map.World.MovePlayerMap(ref connection.Entity, ch, "prt_fild08", new Position(170, 367));
        }
    }
}
