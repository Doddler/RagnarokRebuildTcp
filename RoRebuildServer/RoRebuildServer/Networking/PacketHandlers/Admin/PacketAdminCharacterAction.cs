using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;
using System.Threading;
using RebuildSharedData.Enum.EntityStats;

namespace RoRebuildServer.Networking.PacketHandlers.Admin;

[AdminClientPacketHandler(PacketType.AdminCharacterAction)]
public class PacketAdminCharacterAction : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsConnectedAndInGame || connection.Player == null)
            return;

        var ch = connection.Character!;
        var map = ch.Map;

        if (map == null) return;
        
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
            case AdminCharacterAction.CreateEvent:
            {
                var eventName = msg.ReadString();
                var p1 = msg.ReadInt32();
                var p2 = msg.ReadInt32();
                var p3 = msg.ReadInt32();
                var p4 = msg.ReadInt32();
                var paramString = msg.ReadString();
                var e = World.Instance.CreateEvent(ch.Entity, map, eventName, ch.Position, p1, p2, p3, p4, paramString);
                ch.AttachEvent(e);
                break;
            }
            case AdminCharacterAction.UnlockSkill:
            {
                var skill = (CharacterSkill)msg.ReadInt32();
                var level = msg.ReadInt32();
                if(level > 0)
                    ch.Player.GrantSkillToCharacter(skill, level);
                else
                    ch.Player.RemoveGrantedSkill(skill);

                CommandBuilder.RefreshGrantedSkills(ch.Player);
                break;
            }
            case AdminCharacterAction.Die:
                ch.Player.SetStat(CharacterStat.Hp, 1);
                ch.Player.Die();
                break;
#if DEBUG
            //as a safety god mode is only available in debug builds
            case AdminCharacterAction.GodModeSelf:
                ch.CombatEntity.GodMode = msg.ReadBoolean();
                break;
            case AdminCharacterAction.GodModeOther:
            {
                var name = msg.ReadString();
                var isEnabled = msg.ReadBoolean();
                using var el = EntityListPool.Get();
                map.GatherMonstersInArea(ch.Position, ServerConfig.MaxViewDistance, el);

                foreach (var m in el)
                {
                    if (m.Type != EntityType.Monster || !m.TryGet<WorldObject>(out var mon))
                        continue;

                    if (name != null && mon.Monster.MonsterBase.Name == name)
                        mon.CombatEntity.GodMode = isEnabled;
                }

                break;
            }
#endif
        }
    }
}