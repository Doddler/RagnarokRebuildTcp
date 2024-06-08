using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.Networking.PacketHandlers.Admin
{
    [AdminClientPacketHandler(PacketType.AdminFindTarget)]
    public class PacketAdminFindTarget : IClientPacketHandler
    {
        public void Process(NetworkConnection connection, InboundMessage msg)
        {
            if (!connection.IsOnlineAdmin)
                return;

            var targetName = msg.ReadString();
            var targetNameUpper = targetName.ToUpperInvariant();
            var ch = connection.Character!;
            var map = ch.Map;
            
            if(map == null) return;

            WorldObject closest = null;
            var distance = 999999;

            for (var i = 0; i < map.Chunks.Length; i++)
            {
                foreach (var entity in map.Chunks[i].AllEntities)
                {
                    var valid = false;
                    if(entity.Type == EntityType.Monster)
                        if (entity.Get<Monster>().MonsterBase.Code == targetNameUpper)
                            valid = true;

                    var chara = entity.Get<WorldObject>();
                    if (chara.Name == targetName)
                        valid = true;

                    if (!valid)
                        continue;

                    var newDistance = connection.Character.Position.BlockDistance(chara.Position);

                    if (newDistance < distance)
                    {
                        distance = newDistance;
                        closest = chara;
                    }
                }
            }

            if(closest == null)
                CommandBuilder.SkillFailed(connection.Player, SkillValidationResult.Failure);
            else
            {
                ch.ResetState();
                ch.SetSpawnImmunity();
                map.TeleportEntity(ref connection.Entity, ch, closest.Position);
            }
        }
    }
}
