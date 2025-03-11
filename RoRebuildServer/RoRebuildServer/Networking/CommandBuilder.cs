using System.Diagnostics;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Networking;
using RebuildZoneServer.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation.Items;
using RoRebuildServer.Simulation.Skills;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;

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

    public static void EnsureRecipient(Entity entity)
    {
        if (!entity.TryGet<Player>(out var player))
            return;

        if (recipients != null)
            for (var i = 0; i < recipients.Count; i++)
                if (recipients[i] == player.Connection)
                    return;

        AddRecipient(entity);
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

    //alternate Move packet that sends floating point walk data
    private static void WriteWalkData2(WorldObject c, OutboundMessage packet)
    {
        Debug.Assert(c.WalkPath != null);
        Debug.Assert(c.IsMoving);
        packet.Write(c.WalkPath[c.MoveStep]);
        packet.Write(c.WorldPosition);
        packet.Write(c.MoveSpeed);
        packet.Write(c.TimeToReachNextStep);
        packet.Write((byte)(c.TotalMoveSteps - c.MoveStep));
        //packet.Write((byte)c.MoveStep);

        if (c.TotalMoveSteps > 0)
        {
            var i = c.MoveStep + 1; //we can derive our starting cell from the MoveStartPosition above

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

            //var lockTime = c.MoveLockTime - Time.ElapsedTimeFloat;
            packet.Write(c.InMoveLock);
        }
    }

    //private static void WriteMoveData(WorldObject c, OutboundMessage packet)
    //{
    //    if (c.WalkPath == null)
    //    {
    //        ServerLogger.LogWarning("Attempting to send empty movepath to player");
    //        return;
    //    }

    //    packet.Write(c.MoveSpeed);
    //    packet.Write(c.MoveProgress);
    //    packet.Write((byte)c.TotalMoveSteps);
    //    packet.Write((byte)c.MoveStep);
    //    if (c.TotalMoveSteps > 0)
    //    {
    //        packet.Write(c.WalkPath[0]);

    //        var i = 1;

    //        //pack directions into 2 steps per byte
    //        while (i < c.TotalMoveSteps)
    //        {
    //            var b = (byte)((byte)(c.WalkPath[i] - c.WalkPath[i - 1]).GetDirectionForOffset() << 4);
    //            i++;
    //            if (i < c.TotalMoveSteps)
    //                b |= (byte)(c.WalkPath[i] - c.WalkPath[i - 1]).GetDirectionForOffset();
    //            i++;
    //            packet.Write(b);
    //        }

    //        var lockTime = c.MoveLockTime - Time.ElapsedTimeFloat;
    //        packet.Write(lockTime > 0 ? lockTime : 0f);
    //    }
    //}

    private static void AddFullEntityData(OutboundMessage packet, WorldObject c, bool isSelf = false)
    {
        var type = c.Type;
        //var isCharacterNpc = false; //npc that has taken a player appearance
        if (c.OverrideAppearanceState != null)
        {
            type = CharacterType.PlayerLikeNpc;
            //isCharacterNpc = true;
        }
        
        packet.Write(c.Id);
        packet.Write((byte)type);
        packet.Write((short)c.ClassId);
        packet.Write(c.Position);
        packet.Write((byte)c.FacingDirection);
        packet.Write((byte)c.State);

        if (type == CharacterType.PlayerLikeNpc)
        {
            packet.Write((byte)40); //lvl
            packet.Write(1000); //max hp
            packet.Write(1000); //hp
            if (c.OverrideAppearanceState!.HasCart)
            {
                packet.Write(true);
                packet.Write((byte)CharacterStatusEffect.PushCart);
                packet.Write(float.MaxValue);
            }
            packet.Write(false); //statusEffectData
        }
        else if (type == CharacterType.Monster || type == CharacterType.Player)
        {
            var ce = c.Entity.Get<CombatEntity>();
            packet.Write((byte)ce.GetStat(CharacterStat.Level));
            packet.Write(ce.GetStat(CharacterStat.MaxHp));
            packet.Write(ce.GetStat(CharacterStat.Hp));

            var status = ce.StatusContainer;
            if(status == null)
                packet.Write(false);
            else
                status.PrepareCreateEntityMessage(packet);
        }
        if (type == CharacterType.Player || type == CharacterType.PlayerLikeNpc)
        {
            if (type != CharacterType.PlayerLikeNpc)
            {
                var player = c.Entity.Get<Player>();
                packet.Write((byte)player.HeadFacing);
                packet.Write((byte)player.GetData(PlayerStat.Head));
                packet.Write((byte)player.GetData(PlayerStat.HairId));
                packet.Write((byte)player.WeaponClass);
                packet.Write(player.IsMale);
                packet.Write(player.Name);
                packet.Write(player.Equipment.GetEquipmentIdBySlot(EquipSlot.HeadTop));
                packet.Write(player.Equipment.GetEquipmentIdBySlot(EquipSlot.HeadMid));
                packet.Write(player.Equipment.GetEquipmentIdBySlot(EquipSlot.HeadBottom));
                packet.Write(player.Equipment.GetEquipmentIdBySlot(EquipSlot.Weapon));
                packet.Write(player.Equipment.GetEquipmentIdBySlot(EquipSlot.Shield));
                if (isSelf)
                {
                    packet.Write(player.GetStat(CharacterStat.Sp));
                    packet.Write(player.GetStat(CharacterStat.MaxSp));
                }
                else
                {
                    packet.Write(0); //they don't need the sp value for other players
                    packet.Write(0);
                }

                //packet.Write(isSelf);
            }
            else
            {
                var npc = c.OverrideAppearanceState!;
                packet.Write((byte)npc.HeadFacing);
                packet.Write((byte)npc.HeadType);
                packet.Write((byte)npc.HairColor);
                packet.Write((byte)npc.WeaponClass);
                packet.Write(npc.IsMale);
                packet.Write(c.Name);
                packet.Write(npc.HeadTop);
                packet.Write(npc.HeadMid);
                packet.Write(npc.HeadBottom);
                packet.Write(npc.Weapon);
                packet.Write(npc.Shield);
                packet.Write(0); //sp
                packet.Write(0); //maxsp

                //packet.Write(false);
            }
        }

        if (type == CharacterType.NPC)
        {
            var npc = c.Entity.Get<Npc>();
            packet.Write(npc.Name);
            packet.Write(npc.HasInteract);
            packet.Write((byte)npc.DisplayType);
            packet.Write((byte)npc.EffectType);
        }
        
        if (c.AdminHidden && !isSelf)
            ServerLogger.LogWarning($"We are sending the data of hidden character \"{c.Name}\" to the client!");

        if (c.State == CharacterState.Moving)
        {
            WriteWalkData2(c, packet);
        }
    }
    
    private static OutboundMessage BuildCreateEntity(WorldObject c, bool isSelf = false)
    {
        var type = isSelf ? PacketType.EnterServer : PacketType.CreateEntity;
        var packet = NetworkManager.StartPacket(type, 256);

        if (c.AdminHidden && !isSelf)
            ServerLogger.LogWarning($"We are unexpectedly sending data for the hidden object {c} to a player!");

        AddFullEntityData(packet, c, isSelf);

        return packet;
    }

    public static void SendUpdatePlayerData(Player p, bool sendInventory = false, bool sendSkills = false)
    {
        var packet = NetworkManager.StartPacket(PacketType.UpdatePlayerData, 512);

        p.SendPlayerUpdateData(packet, sendInventory, sendSkills);

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void PlayerEquipItem(Player player, int bagId, EquipSlot slot, bool isEquip)
    {
        var packet = NetworkManager.StartPacket(PacketType.EquipUnequipGear, 12);
        packet.Write(bagId);
        packet.Write((byte)slot);
        packet.Write(isEquip);

        NetworkManager.SendMessage(packet, player.Connection);
    }

    public static void PlayerUpdateInventoryItemState(Player player, int bagId, UniqueItem item)
    {
        var packet = NetworkManager.StartPacket(PacketType.SocketEquipment, 12);

        packet.Write(bagId);
        item.Serialize(packet);

        NetworkManager.SendMessage(packet, player.Connection);
    }

    public static void UpdatePlayerAppearanceAuto(Player player)
    {
        var packet = NetworkManager.StartPacket(PacketType.UpdateCharacterDisplayState, 48);

        packet.Write(player.Character.Id);
        packet.Write(player.Equipment.GetEquipmentIdBySlot(EquipSlot.HeadTop));
        packet.Write(player.Equipment.GetEquipmentIdBySlot(EquipSlot.HeadMid));
        packet.Write(player.Equipment.GetEquipmentIdBySlot(EquipSlot.HeadBottom));
        packet.Write(player.Equipment.GetEquipmentIdBySlot(EquipSlot.Weapon));
        packet.Write(player.Equipment.GetEquipmentIdBySlot(EquipSlot.Shield));
        packet.Write(player.WeaponClass);

        player.Character.Map?.AddVisiblePlayersAsPacketRecipients(player.Character);
        EnsureRecipient(player.Entity);
        NetworkManager.SendMessageMulti(packet, recipients);
        ClearRecipients();
    }

    public static void ChangeCombatTargetableState(WorldObject target, bool canInteract)
    {
        var packet = NetworkManager.StartPacket(PacketType.ChangeTargetableState, 8);
        packet.Write(target.Id);
        packet.Write(canInteract);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void StopCastMultiAutoVis(WorldObject caster)
    {
        caster.Map?.AddVisiblePlayersAsPacketRecipients(caster);

        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.StopCast, 8);

        packet.Write(caster.Id);

        NetworkManager.SendMessageMulti(packet, recipients);
        ClearRecipients();
    }

    public static void UpdateExistingCastMultiAutoVis(WorldObject caster, float adjustedEndTime)
    {
        caster.Map?.AddVisiblePlayersAsPacketRecipients(caster);
        EnsureRecipient(caster.Entity);
        var packet = NetworkManager.StartPacket(PacketType.UpdateExistingCast, 32);
        packet.Write(caster.Id);
        packet.Write(adjustedEndTime);

        NetworkManager.SendMessageMulti(packet, recipients);

        ClearRecipients();
    }

    public static void StopCastMulti(WorldObject caster)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.StopCast, 8);

        packet.Write(caster.Id);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void StartCastMulti(WorldObject caster, WorldObject? target, CharacterSkill skill, int lvl, float castTime, SkillCastFlags flags)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.StartCast, 48);

        packet.Write(caster.Id);
        packet.Write(target?.Id ?? -1);
        packet.Write((byte)skill);
        packet.Write((byte)lvl);
        packet.Write((byte)caster.FacingDirection);
        packet.Write(caster.Position);
        packet.Write(castTime);
        packet.Write((byte)flags);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void StartCastCircleMulti(Position target, int size, float castTime, bool isAlly, bool hasSound)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.CreateCastCircle, 48);

        packet.Write(target);
        packet.Write((byte)size);
        packet.Write(castTime);
        packet.Write(isAlly);
        packet.Write(hasSound);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void StartCastGroundTargetedMulti(WorldObject caster, Position target, CharacterSkill skill, int lvl, int size, float castTime, SkillCastFlags flags)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.StartAreaCast, 48);

        packet.Write(caster.Id);
        packet.Write(target);
        packet.Write((byte)skill);
        packet.Write((byte)lvl);
        packet.Write((byte)size);
        packet.Write((byte)caster.FacingDirection);
        packet.Write(caster.Position);
        packet.Write(castTime);
        packet.Write((byte)flags);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SkillExecuteTargetedSkillAutoVis(WorldObject caster, WorldObject? target, CharacterSkill skill, int lvl, DamageInfo di)
    {
        caster.Map?.AddVisiblePlayersAsPacketRecipients(caster);
        if(target != null) EnsureRecipient(target.Entity);
        SkillExecuteTargetedSkill(caster, target, skill, lvl, di);
        ClearRecipients();
    }

    public static void SkillExecuteTargetedSkill(WorldObject caster, WorldObject? target, CharacterSkill skill, int lvl, DamageInfo di)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.Skill, 48);

        packet.Write((byte)SkillTarget.Enemy);
        packet.Write(caster.Id);
        packet.Write(target?.Id ?? -1);
        packet.Write((byte)skill);
        packet.Write((byte)lvl);
        packet.Write((byte)caster.FacingDirection);
        packet.Write(caster.Position);
        packet.Write(di.DisplayDamage);
        packet.Write((byte)di.Result);
        packet.Write((byte)di.HitCount);
        packet.Write(di.AttackMotionTime);
        packet.Write(di.Time - Time.ElapsedTimeFloat);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SkillExecuteIndirectAutoVisibility(WorldObject caster, WorldObject target, DamageInfo di)
    {
        caster.Map?.AddVisiblePlayersAsPacketRecipients(target);
        CommandBuilder.SkillExecuteIndirect(caster, target, di);
        CommandBuilder.ClearRecipients();
    }

    public static void SkillExecuteIndirect(WorldObject caster, WorldObject target, DamageInfo di)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.SkillIndirect, 48);

        packet.Write(caster.Id);
        packet.Write(target.Id);
        packet.Write(caster.Position);
        packet.Write(di.DisplayDamage);
        packet.Write((byte)di.AttackSkill);
        packet.Write((byte)di.HitCount);
        packet.Write((byte)di.Result);
        
        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SkillExecuteSelfTargetedSkillAutoVis(WorldObject caster, CharacterSkill skill, int lvl)
    {
        caster.Map?.AddVisiblePlayersAsPacketRecipients(caster);
        SkillExecuteSelfTargetedSkill(caster, skill, lvl);
        ClearRecipients();
    }

    public static void SkillExecuteSelfTargetedSkill(WorldObject caster, CharacterSkill skill, int lvl)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.Skill, 48);

        packet.Write((byte)SkillTarget.Self);
        packet.Write(caster.Id);
        packet.Write((byte)skill);
        packet.Write((byte)lvl);
        packet.Write((byte)caster.FacingDirection);
        packet.Write(caster.Position);
        packet.Write(caster.CombatEntity?.GetTiming(TimingStat.AttackMotionTime) ?? 0);

        NetworkManager.SendMessageMulti(packet, recipients);
    }


    public static void SkillExecuteAreaTargetedSkillAutoVis(WorldObject caster, Position target, CharacterSkill skill,
        int lvl)
    {
        caster.Map?.AddVisiblePlayersAsPacketRecipients(caster);
        SkillExecuteAreaTargetedSkill(caster, target, skill, lvl);
        ClearRecipients();
    }

    public static void SkillExecuteAreaTargetedSkill(WorldObject caster, Position target, CharacterSkill skill, int lvl)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.Skill, 48);

        packet.Write((byte)SkillTarget.Ground);
        packet.Write(caster.Id);
        packet.Write(target);
        packet.Write((byte)skill);
        packet.Write((byte)lvl);
        packet.Write((byte)caster.FacingDirection);
        packet.Write(caster.Position);
        packet.Write(caster.CombatEntity?.GetTiming(TimingStat.AttackMotionTime) ?? 0);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void AttackMulti(WorldObject? attacker, WorldObject target, DamageInfo di, bool showAttackMotion = true)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.Attack, 48);

        var dir = target.FacingDirection;
        if (attacker != null)
            dir = attacker.FacingDirection;

        var pos = target.Position;
        if (attacker != null)
            pos = attacker.Position;

        packet.Write(attacker?.Id ?? -1);
        packet.Write(target.Id);
        packet.Write((byte)dir);
        packet.Write((byte)di.AttackSkill);
        packet.Write((byte)di.HitCount);
        packet.Write((byte)di.Result);
        packet.Write(pos);
        packet.Write(di.DisplayDamage);
        packet.Write(di.AttackMotionTime);
        packet.Write(di.Time - Time.ElapsedTimeFloat);
        packet.Write(showAttackMotion);


        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void TakeDamageMulti(WorldObject target, DamageInfo di)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.TakeDamage, 48);

        //var src = 0;
        //if (di.Source.TryGet<WorldObject>(out var damageSrc))
        //    src = damageSrc.Id;

        packet.Write(target.Id);
        packet.Write(di.Damage);
        packet.Write(di.HitCount);
        packet.Write(di.Time);
        //packet.Write(src);
        //packet.Write((byte)di.AttackSkill);

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

    public static void ChangeFacingMulti(WorldObject c, Position lookAtPos)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.LookTowards, 48);

        packet.Write(c.Id);
        packet.Write(lookAtPos);
        packet.Write((byte)c.FacingDirection);

        if (c.Type == CharacterType.Player)
        {
            var player = c.Entity.Get<Player>();
            packet.Write((byte)player.HeadFacing);
        }
        else if (c.OverrideAppearanceState != null)
            packet.Write((byte)c.OverrideAppearanceState.HeadFacing);
        else
            packet.Write((byte)0);

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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="c"></param>
    public static void SendStartMoveEntityMulti(WorldObject c)
    {
        var packet = NetworkManager.StartPacket(PacketType.StartWalk, 256);

        packet.Write(c.Id);
        WriteWalkData2(c, packet);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    /// <summary>
    /// Special action that informs the client that a character will move from their current position to a destination without regards to distance.
    /// </summary>
    //public static void SendMoveToFixedPositionMulti(WorldObject c, Position dest, float time)
    //{
    //    var packet = NetworkManager.StartPacket(PacketType.FixedMove, 32);

    //    packet.Write(c.Id);
    //    packet.Write(dest);
    //    packet.Write(c.MoveSpeed);
    //    packet.Write(time);

    //    NetworkManager.SendMessageMulti(packet, recipients);
    //}

    public static void SendAllMapImportantEntities(Player p, EntityList mapImportantEntities)
    {
        var packet = NetworkManager.StartPacket(PacketType.UpdateMinimapMarker, 64);

        mapImportantEntities.ClearInactive();
        packet.Write((short)mapImportantEntities.Count);
        for (var i = 0; i < mapImportantEntities.Count; i++)
        {
            var chara = mapImportantEntities[i].Get<WorldObject>();
            packet.Write(chara.Id);
            packet.Write(chara.Position);
            packet.Write((byte)chara.DisplayType);
        }

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void SendUpdateMapImportantEntityMulti(WorldObject o)
    {
        var packet = NetworkManager.StartPacket(PacketType.UpdateMinimapMarker, 32);

        packet.Write((short)1);
        packet.Write(o.Id);
        packet.Write(o.Position);
        packet.Write((byte)o.DisplayType);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SendRemoveMapImportantEntityMulti(WorldObject o)
    {
        var packet = NetworkManager.StartPacket(PacketType.UpdateMinimapMarker, 32);

        packet.Write((short)1);
        packet.Write(o.Id);
        packet.Write(Position.Zero);
        packet.Write((byte)CharacterDisplayType.None);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SendServerMessage(string text, string name = "Server")
    {
        var packet = NetworkManager.StartPacket(PacketType.Say, 364);

        packet.Write(-1);
        packet.Write(text);
        packet.Write(name);
        packet.Write(false);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SendServerEvent(Player p, ServerEvent eventType, int id = 0, string text = "")
    {
        var packet = NetworkManager.StartPacket(PacketType.ServerEvent, 128);

        packet.Write((byte)eventType);
        packet.Write(id);
        packet.Write(text);

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void SendSayMulti(WorldObject? c, string name, string text, bool isShout)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.Say, 364);

        if (c == null)
            packet.Write(-1);
        else
            packet.Write(c.Id);
        packet.Write(text);
        packet.Write(name);
        packet.Write(isShout);

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
        var packet = NetworkManager.StartPacket(PacketType.EnterServer, 32);
        packet.Write(c.Id);
        Debug.Assert(c.Map != null, $"Player {p} not attached to map to inform of server enter.");
        packet.Write(c.Map.Name);
        packet.Write(c.Player.Id.ToByteArray());
        
        NetworkManager.SendMessage(packet, p.Connection);
        SendUpdatePlayerData(p, true, true);
    }

    public static void SendCreateEntityMulti(WorldObject c, CreateEntityEventType entryType = CreateEntityEventType.Normal)
    {
        if (!HasRecipients())
            return;

        var packet = BuildCreateEntity(c);
        packet.Write((byte)entryType);
        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SendCreateEntity(WorldObject c, Player player, CreateEntityEventType entryType = CreateEntityEventType.Normal)
    {
        var packet = BuildCreateEntity(c);
        packet.Write((byte)entryType);
        NetworkManager.SendMessage(packet, player.Connection);
    }

    public static void SendCreateEntityWithEventMulti(WorldObject c, CreateEntityEventType eventType, Position pos)
    {
        if (!HasRecipients())
            return;

        var packet = BuildCreateEntity(c);
        packet.Write((byte)eventType);
        packet.Write(pos);
        NetworkManager.SendMessageMulti(packet, recipients);
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

        var hp = 0;
        if (c.Type == CharacterType.Player)
            hp = c.Player.GetStat(CharacterStat.Hp);

        packet.Write(hp);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SendHitMulti(WorldObject c, int damage, bool isHitStopped)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.HitTarget, 32);
        packet.Write(c.Id);
        //packet.Write(delayTime);
        packet.Write(damage);
        packet.Write(c.Position);
        packet.Write(c.InMoveLock && isHitStopped);

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

    public static void SendPlaySoundAtLocationMulti(string fileName, Position pos)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.PlayOneShotSound, 48);
        packet.Write(fileName);
        packet.Write(pos);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SendApplyStatusEffect(WorldObject p, ref StatusEffectState state)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.ApplyStatusEffect, 16);
        packet.Write(p.Id);
        packet.Write((byte)state.Type);
        packet.Write((float)(state.Expiration - Time.ElapsedTime));

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void SendRemoveStatusEffect(WorldObject p, ref StatusEffectState state, bool isRefresh = false)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.RemoveStatusEffect, 16);
        packet.Write(p.Id);
        packet.Write((byte)state.Type);
        packet.Write(isRefresh);

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

    public static void ChangeSpValue(Player p, int sp, int maxSp)
    {
        var packet = NetworkManager.StartPacket(PacketType.ChangeSpValue, 8);
        packet.Write(sp);
        packet.Write(maxSp);

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void SendImprovedRecoveryValue(Player p, int hpGain, int spGain)
    {
        var packet = NetworkManager.StartPacket(PacketType.ImprovedRecoveryTick, 32);
        packet.Write(p.Character.Id);
        packet.Write((short)hpGain);
        packet.Write((short)spGain);

        NetworkManager.SendMessage(packet, p.Connection);
    }


    public static void SendExpGain(Player p, int exp, int job = 0)
    {
        var packet = NetworkManager.StartPacket(PacketType.GainExp, 8);
        packet.Write(p.GetData(PlayerStat.Experience));
        packet.Write(exp);
        packet.Write(p.GetData(PlayerStat.JobExperience));
        packet.Write(job);

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void SendRequestFailed(Player p, ClientErrorType error)
    {
        var packet = NetworkManager.StartPacket(PacketType.RequestFailed, 8);
        packet.Write((byte)error);

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void LevelUp(WorldObject c, int level, int curExp = 0)
    {
        if (!HasRecipients())
            return;

        var packet = NetworkManager.StartPacket(PacketType.LevelUp, 8);
        packet.Write(c.Id);
        packet.Write((byte)level);
        packet.Write(curExp);

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

    public static void SendFocusNpc(Player p, Npc target, bool isFocus)
    {
        var packet = NetworkManager.StartPacket(PacketType.NpcInteraction, 32);

        var obj = target.Entity.Get<WorldObject>();

        packet.Write((byte)NpcInteractionType.NpcFocusNpc);
        packet.Write(obj.Id);
        packet.Write(isFocus);

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

    public static void SendNpcOpenRefineDialog(Player p)
    {
        var packet = NetworkManager.StartPacket(PacketType.NpcInteraction, 8);
        packet.Write((byte)NpcInteractionType.NpcOpenRefineWindow);

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void SendNpcOpenShop(Player p, Npc npc, bool canDiscount)
    {
        var packet = NetworkManager.StartPacket(PacketType.OpenShop, 128);
        var count = 0;
        if (npc.ItemsForSale != null)
            count = npc.ItemsForSale.Count;
        var discount = canDiscount ? p.MaxLearnedLevelOfSkill(CharacterSkill.Discount) : 0;

        packet.Write((byte)1); //buy from NPC
        packet.Write((byte)discount);
        packet.Write(count);

        for (var i = 0; i < count; i++)
        {
            var (item, cost) = npc.ItemsForSale![i];
            packet.Write(item);
            packet.Write(cost);
        }

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void SendNpcStartTrade(Player p)
    {
        var packet = NetworkManager.StartPacket(PacketType.OpenShop, 128);
        packet.Write((byte)0); //sell to NPC
        packet.Write(p.MaxLearnedLevelOfSkill(CharacterSkill.Overcharge));

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void SendNpcStorage(Player p)
    {
        var packet = NetworkManager.StartPacket(PacketType.OpenStorage, 2048);
        p.StorageInventory.TryWrite(packet, true);

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void SendNpcStorageMoveEvent(Player p, ItemReference item, int bagId, int movedCount, bool moveToStorage)
    {
        var packet = NetworkManager.StartPacket(PacketType.StorageInteraction, 48);
        packet.Write((byte)item.Type);
        packet.Write(bagId);
        packet.Write((short)movedCount);
        item.Serialize(packet);
        packet.Write(p.Inventory?.BagWeight ?? 0);
        packet.Write(p.StorageInventory?.UsedSlots ?? 0);
        packet.Write(moveToStorage);

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

    public static void SendUpdateZeny(Player p)
    {
        var packet = NetworkManager.StartPacket(PacketType.UpdateZeny, 8);
        packet.Write(p.GetZeny());

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void DropItemMulti(GroundItem item, bool isNewDrop)
    {
        var packet = NetworkManager.StartPacket(PacketType.DropItem, 48);
        item.Serialize(packet);
        packet.Write(isNewDrop);
        
        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void RevealDropItemForPlayer(GroundItem item, bool isNewDrop, Player p)
    {
        var packet = NetworkManager.StartPacket(PacketType.DropItem, 48);
        item.Serialize(packet);
        packet.Write(isNewDrop);

        NetworkManager.SendMessage(packet, p.Connection);
    }
    
    public static void PickUpOrRemoveItemMulti(WorldObject? pickup, GroundItem item)
    {
        var packet = NetworkManager.StartPacket(PacketType.PickUpItem, 32);
        packet.Write(pickup?.Id ?? -1);
        packet.Write(item.Id);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void RemoveDropItemForSinglePlayer(GroundItem item, Player p)
    {
        var packet = NetworkManager.StartPacket(PacketType.PickUpItem, 32);
        packet.Write(-1);
        packet.Write(item.Id);

        NetworkManager.SendMessage(packet, p.Connection);
    }

    //alternate version to send to clean up an item that should not be visible to the player.
    public static void RemoveDropItemForSinglePlayerByGroundId(int itemId, Player p)
    {
        var packet = NetworkManager.StartPacket(PacketType.PickUpItem, 32);
        packet.Write(-1);
        packet.Write(itemId);

        NetworkManager.SendMessageMulti(packet, recipients);
    }

    public static void AddItemToInventory(Player p, ItemReference item, int bagId, int change)
    {
        var packet = NetworkManager.StartPacket(PacketType.AddOrRemoveInventoryItem, 48);
        packet.Write(true); //isAdd
        packet.Write((byte)item.Type);
        packet.Write(bagId);
        packet.Write((short)change);
        packet.Write(p.Inventory?.BagWeight ?? 0);
        item.Serialize(packet);

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void RemoveItemFromInventory(Player p, int bagId, int change)
    {
        var packet = NetworkManager.StartPacket(PacketType.AddOrRemoveInventoryItem, 24);
        packet.Write(false); //isAdd
        packet.Write(bagId);
        packet.Write((short)change);
        packet.Write(p.Inventory?.BagWeight ?? 0);

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void SendMapMemoLocations(Player p)
    {
        var packet = NetworkManager.StartPacket(PacketType.MemoMapLocation, 96);

        for(var i = 0; i < 4; i++)
            p.MemoLocations[i].Serialize(packet);

        NetworkManager.SendMessage(packet, p.Connection);
    }
    
    public static void SkillFailed(Player p, SkillValidationResult res)
    {
        var packet = NetworkManager.StartPacket(PacketType.SkillError, 24);
        packet.Write((byte)res);

        NetworkManager.SendMessage(packet, p.Connection);
    }


    public static void ErrorMessage(Player p, string text)
    {
        var packet = NetworkManager.StartPacket(PacketType.ErrorMessage, 64);
        packet.Write(text);

        NetworkManager.SendMessage(packet, p.Connection);
    }


    public static void ErrorMessage(NetworkConnection connection, string text)
    {
        var packet = NetworkManager.StartPacket(PacketType.ErrorMessage, 64);
        packet.Write(text);

        NetworkManager.SendMessage(packet, connection);
    }

    public static void ApplySkillPoint(Player p, CharacterSkill skill)
    {
        var packet = NetworkManager.StartPacket(PacketType.ApplySkillPoint, 32);
        packet.Write((byte)skill);
        packet.Write((byte)p.LearnedSkills[skill]);
        packet.Write(p.GetData(PlayerStat.SkillPoints));

        NetworkManager.SendMessage(packet, p.Connection);
    }

    public static void ChangePlayerSpecialActionState(Player p, SpecialPlayerActionState state)
    {
        var packet = NetworkManager.StartPacket(PacketType.ChangePlayerSpecialActionState, 16);
        packet.Write((byte)state);

        NetworkManager.SendMessage(packet, p.Connection);
    }
}