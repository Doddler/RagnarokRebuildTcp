using System.Diagnostics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RebuildZoneServer.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking;

public static class CommandBuilder
{
    [ThreadStatic]
    private static List<NetworkConnection>? recipients;

    public static void AddRecipient(Entity e)
    {
        if (!e.IsAlive())
            return;

        if (recipients == null)
            recipients = new List<NetworkConnection>(10);

        var player = e.Get<Player>();
        recipients.Add(player.Connection);
    }


    public static void AddRecipients(EntityList list)
    {
        foreach (var e in list)
        {
            AddRecipient(e);
        }
    }

    public static void AddAllPlayersAsRecipients()
    {
        NetworkManager.AddAllPlayersAsRecipient();
    }

    public static void ClearRecipients()
    {
        recipients?.Clear();
    }

    public static bool HasRecipients()
    {
        return recipients != null && recipients.Count > 0;
    }

    private static void WriteMoveData(WorldObject c, OutboundMessage packet)
    {
        if (c.WalkPath == null)
        {
            ServerLogger.LogWarning("Attempting to send empty movepath to player");
            return;
        }
        
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

    private static void AddFullEntityData(OutboundMessage packet, WorldObject c, bool isSelf = false)
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
            packet.Write((byte)ce.GetStat(CharacterStat.Level));
            packet.Write((ushort)ce.GetStat(CharacterStat.MaxHp));
            packet.Write((ushort)ce.GetStat(CharacterStat.Hp));
        }
        if (c.Type == CharacterType.Player)
        {
            var player = c.Entity.Get<Player>();
            packet.Write((byte)player.HeadFacing);
            packet.Write((byte)player.HeadId);
            packet.Write((byte)player.WeaponClass);
            packet.Write(player.IsMale);
            packet.Write(player.Name);
        }

        if (c.Type == CharacterType.NPC)
        {
            var npc = c.Entity.Get<Npc>();
            packet.Write(npc.Name);
            packet.Write(npc.HasInteract);
        }

        if (c.Hidden)
            ServerLogger.LogWarning($"We are sending the data of hidden character \"{c.Name}\" to the client!");

        if (c.State == CharacterState.Moving)
        {
            WriteMoveData(c, packet);
        }

    }

    private static OutboundMessage BuildCreateEntity(WorldObject c, bool isSelf = false)
    {
        var type = isSelf ? PacketType.EnterServer : PacketType.CreateEntity;
        var packet = NetworkManager.StartPacket(type, 256);

        AddFullEntityData(packet, c, isSelf);

        return packet;
    }

    public static void StartCastMulti(WorldObject caster, WorldObject target, CharacterSkill skill, int lvl,
        float castTime)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.StartCast, 48);

        packet.Write(caster.Id);
        packet.Write(target.Id);
        packet.Write((byte)skill);
        packet.Write((byte)lvl);
        packet.Write((byte)caster.FacingDirection);
        packet.Write(caster.Position);
        packet.Write(castTime);

        NetworkManager.SendMessageMulti(packet, recipients);
    }
    
    public static void SkillExecuteTargetedSkill(WorldObject caster, WorldObject target, CharacterSkill skill, int lvl,
        DamageInfo di)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.Skill, 48);

        packet.Write((byte)SkillType.SingleTarget);
        packet.Write(caster.Id);
        packet.Write(target.Id);
        packet.Write((byte)skill);
        packet.Write((byte)lvl);
        packet.Write((byte)caster.FacingDirection);
        packet.Write(caster.Position);
        packet.Write(di.Damage);
        packet.Write((byte)di.Result);
        packet.Write((byte)di.HitCount);
        packet.Write(di.AttackMotionTime);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void AttackMulti(WorldObject attacker, WorldObject target, DamageInfo di)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.Attack, 48);

        packet.Write(attacker.Id);
        packet.Write(target.Id);
        packet.Write((byte)attacker.FacingDirection);
        packet.Write(attacker.Position);
        packet.Write(di.Damage);
        packet.Write((byte)di.HitCount);
        packet.Write(di.AttackMotionTime);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void TakeDamageMulti(WorldObject target, DamageInfo di)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.TakeDamage, 48);
        
        packet.Write(target.Id);
        packet.Write(di.Damage);
        packet.Write(di.HitCount);
        packet.Write(di.Time);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void ChangeSittingMulti(WorldObject c)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.SitStand, 48);

        packet.Write(c.Id);
        packet.Write(c.State == CharacterState.Sitting);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void ChangeFacingMulti(WorldObject c)
    {
        if (!HasRecipients())
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

    public static void CharacterStopImmediateMulti(WorldObject c)
    {
        var packet = NetworkManager.StartPacket(PacketType.StopImmediate, 32);

        packet.Write(c.Id);
        packet.Write(c.Position);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void CharacterStopMulti(WorldObject c)
    {
        var packet = NetworkManager.StartPacket(PacketType.StopAction, 32);

        packet.Write(c.Id);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SendMoveEntityMulti(WorldObject c)
    {
        var packet = NetworkManager.StartPacket(PacketType.Move, 48);

        packet.Write(c.Id);
        packet.Write(c.Position);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SendStartMoveEntityMulti(WorldObject c)
    {
        var packet = NetworkManager.StartPacket(PacketType.StartMove, 256);

        packet.Write(c.Id);
        WriteMoveData(c, packet);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SendServerMessage(string text)
    {
        var packet = NetworkManager.StartPacket(PacketType.Say, 364);

        packet.Write(-1);
        packet.Write(text);
        
        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SendSayMulti(WorldObject c, string text)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.Say, 364);

        packet.Write(c.Id);
        packet.Write(text);

        NetworkManager.SendMessageMulti(packet, recipients);
    }


    public static void SendEmoteMulti(WorldObject c, int emote)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.Emote, 32);

        packet.Write(c.Id);
        packet.Write(emote);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SendChangeNameMulti(WorldObject c, string text)
    {
        var packet = NetworkManager.StartPacket(PacketType.ChangeName, 96);

        packet.Write(c.Id);
        packet.Write(text);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void InformEnterServer(WorldObject c, Player p)
    {
        var packet = BuildCreateEntity(c, true);
        packet = NetworkManager.StartPacket(PacketType.EnterServer, 32);
        packet.Write(c.Id);
        Debug.Assert(c.Map != null, $"Player {p} not attached to map to inform of server enter.");
        packet.Write(c.Map.Name);
        packet.Write(c.Player.Id.ToByteArray());
        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void SendCreateEntityMulti(WorldObject c)
    {
        if (!HasRecipients())
            return;

        var packet = BuildCreateEntity(c);
        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SendCreateEntity(WorldObject c, Player player)
    {
        var packet = BuildCreateEntity(c);
        //if (packet == null)
        //	return;

        NetworkManager.SendMessage(packet, player.Connection);
    }

    public static void SendRemoveEntityMulti(WorldObject c, CharacterRemovalReason reason)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.RemoveEntity, 32);
        packet.Write(c.Id);
        packet.Write((byte)reason);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SendRemoveEntity(WorldObject c, Player player, CharacterRemovalReason reason)
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

    public static void SendChangeMap(WorldObject c, Player player)
    {
        if (c.Map == null)
        {
            ServerLogger.LogWarning($"Trying to send change map for player {player.Name} while the player does not currently have a map.");
            return;
        }

        var packet = NetworkManager.StartPacket(PacketType.ChangeMaps, 128);

        packet.Write(c.Map.Name);
        //packet.Write(c.Position);

        NetworkManager.SendMessage(packet, player.Connection);
    }

    public static void SendChangeTarget(Player p, WorldObject? target)
    {
        var packet = NetworkManager.StartPacket(PacketType.ChangeTarget, 32);

        packet.Write(target?.Id ?? 0);

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void SendMonsterTarget(Player p, WorldObject attacker)
    {
        var packet = NetworkManager.StartPacket(PacketType.Targeted, 32);

        packet.Write(attacker.Id);

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void SendPlayerDeath(WorldObject c)
    {
        var packet = NetworkManager.StartPacket(PacketType.Death, 16);
        packet.Write(c.Id);
        packet.Write(c.Position);

        NetworkManager.SendMessageMulti(packet, recipients);
    }
    public static void SendPlayerResurrection(WorldObject c)
    {
        var packet = NetworkManager.StartPacket(PacketType.Resurrection, 16);
        packet.Write(c.Id);
        packet.Write(c.Position);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SendHitMulti(WorldObject c, float delayTime, int damage)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.HitTarget, 32);
        packet.Write(c.Id);
        packet.Write(delayTime);
        packet.Write(damage);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SendEffectOnCharacterMulti(WorldObject p, int effectId)
    {
        if (!HasRecipients())
            return;
        
        var packet = NetworkManager.StartPacket(PacketType.EffectOnCharacter, 16);
        packet.Write(p.Id);
        packet.Write(effectId);
        
        NetworkManager.SendMessageMulti(packet, recipients);
    }
    
    public static void SendEffectAtLocationMulti(int effectId, Position pos, int facing)
    {
        if (!HasRecipients())
            return;
        
        var packet = NetworkManager.StartPacket(PacketType.EffectAtLocation, 16);
        packet.Write(effectId);
        packet.Write(pos);
        packet.Write(facing);

        NetworkManager.SendMessageMulti(packet, recipients);
    }
    
    public static void SendHealMulti(WorldObject p, int healAmount, HealType type)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.HpRecovery, 32);
        packet.Write(p.Id);
        packet.Write(healAmount);
        packet.Write(p.CombatEntity.GetStat(CharacterStat.Hp));
        packet.Write(p.CombatEntity.GetStat(CharacterStat.MaxHp));
        packet.Write((byte)type);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SendHealSingle(Player p, int healAmount, HealType type)
    {
        var packet = NetworkManager.StartPacket(PacketType.HpRecovery, 32);
        packet.Write(p.Character.Id);
        packet.Write(healAmount);
        packet.Write(p.CombatEntity.GetStat(CharacterStat.Hp));
        packet.Write(p.CombatEntity.GetStat(CharacterStat.MaxHp));
        packet.Write((byte)type);

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void SendExpGain(Player p, int exp)
    {
        var packet = NetworkManager.StartPacket(PacketType.GainExp, 8);
        packet.Write(p.GetData(PlayerStat.Experience));
        packet.Write(exp);
        
        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void SendRequestFailed(Player p, ClientErrorType error)
    {
        var packet = NetworkManager.StartPacket(PacketType.RequestFailed, 8);
        packet.Write((byte)error);

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void LevelUp(WorldObject c, int level)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.LevelUp, 8);
        packet.Write(c.Id);
        packet.Write((byte)level);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SendNpcDialog(Player p, string name, string dialog)
    {
        var packet = NetworkManager.StartPacket(PacketType.NpcInteraction, 256);

        packet.Write((byte)NpcInteractionType.NpcDialog);
        packet.Write(name);
        packet.Write(dialog);

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void SendFocusNpc(Player p, Npc target)
    {
        var packet = NetworkManager.StartPacket(PacketType.NpcInteraction, 32);

        var obj = target.Entity.Get<WorldObject>();

        packet.Write((byte)NpcInteractionType.NpcFocusNpc);
        packet.Write(obj.Id);

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void SendNpcOption(Player p, string[] options)
    {
        var packet = NetworkManager.StartPacket(PacketType.NpcInteraction, 256);

        packet.Write((byte)NpcInteractionType.NpcOption);
        packet.Write(options.Length);
        for (var i = 0; i < options.Length; i++)
        {
            packet.Write(options[i]);
        }
        
        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void SendNpcEndInteraction(Player p)
    {
        var packet = NetworkManager.StartPacket(PacketType.NpcInteraction, 8);
        packet.Write((byte)NpcInteractionType.NpcEndInteraction);
        
        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void SendNpcShowSprite(Player p, string spriteName, int pos)
    {
        var packet = NetworkManager.StartPacket(PacketType.NpcInteraction, 8);
        packet.Write((byte)NpcInteractionType.NpcShowSprite);
        packet.Write(spriteName);
        packet.Write((byte)pos);

        NetworkManager.SendMessage(packet, p.Connection);
    }


    public static void SendAdminHideStatus(Player p, bool isHidden)
    {
        var packet = NetworkManager.StartPacket(PacketType.AdminHideCharacter, 8);
        packet.Write(isHidden);

        NetworkManager.SendMessage(packet, p.Connection);
    }


    public static void SkillFailed(Player p, SkillValidationResult res)
    {
        var packet = NetworkManager.StartPacket(PacketType.SkillError, 24);
        packet.Write((byte)res);

        NetworkManager.SendMessage(packet, p.Connection);
    }
}