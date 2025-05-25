using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RebuildZoneServer.Networking;
using RoRebuildServer.Database;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.Database.Requests;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Server;
using RoRebuildServer.Simulation.Util;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace RoRebuildServer.Simulation.Parties;

public record PartyMemberInfo(Guid PlayerId, string Name, int Level, int EntityId, Entity Entity, bool IsOnline);

public class Party
{
    public int PartyId;
    public string PartyName;
    public Entity PartyOwner;
    public int PartyOwnerId;
    public readonly Dictionary<int, PartyMemberInfo> PartyMemberInfo = new();
    public readonly Dictionary<Guid, int> PlayerIdToMemberId = new();
    public EntityList OnlineMembers;
    private EntityValueList<float>? InviteRequests;
    private int idCount;

    private readonly Lock writeLock = new();

    public bool IsPartyLeader(Player p) => p.PartyMemberId == PartyOwnerId;

    public Party(string partyName)
    {
        PartyName = partyName;
        OnlineMembers ??= EntityListPool.Get();
    }

    private PartyMemberInfo AddMemberInfo(Player p)
    {
        idCount++;
        p.PartyMemberId = idCount;
        var info = new PartyMemberInfo(p.Id, p.Name, p.GetData(PlayerStat.Level), p.Character.Id, p.Entity, true);
        PartyMemberInfo.Add(idCount, info);
        OnlineMembers.Add(p.Entity);
        PlayerIdToMemberId.Add(p.Id, idCount);

        OnlineMembers.ClearInactive();
        if (OnlineMembers.Count <= 1)
        {
            PartyOwner = p.Entity;
            PartyOwnerId = idCount;
        }

        return info;
    }

    //Called when creating a party, we know the player is online and in game
    public Party(DbParty existingParty, NetworkConnection owner)
    {
        Debug.Assert(owner.Player != null);
        OnlineMembers ??= EntityListPool.Get();

        PartyId = existingParty.Id;
        PartyName = existingParty.PartyName;

        AddMemberInfo(owner.Player);
    }

    //called when loading a character, we know the player is not in game and all party members offline
    public Party(PartyLoadResult existingParty)
    {
        OnlineMembers ??= EntityListPool.Get();

        PartyId = existingParty.PartyId;
        PartyName = existingParty.PartyName;
        
        foreach (var m in existingParty.Characters)
        {
            idCount++;
            PartyMemberInfo.Add(idCount, new PartyMemberInfo(m.Id, m.Name, -1, 0, Entity.Null, false));
            PlayerIdToMemberId.Add(m.Id, idCount);
        }
    }

    public bool SendInvite(Player sender, Player invitee)
    {
        if (invitee.Party != null)
        {
            CommandBuilder.SendActionResult(sender, ServerResult.InviteFailedAlreadyInParty);
            return false;
        }

        if (sender.MaxLearnedLevelOfSkill(CharacterSkill.BasicMastery) < 6)
        {
            CommandBuilder.SendActionResult(sender, ServerResult.InviteFailedSenderNoBasicSkill);
            return false;
        }

        if (invitee.MaxLearnedLevelOfSkill(CharacterSkill.BasicMastery) < 4)
        {
            CommandBuilder.SendActionResult(sender, ServerResult.InviteFailedRecipientNoBasicSkill);
            return false;
        }

        using (writeLock.EnterScope())
        {
            InviteRequests ??= EntityValueListPool<float>.Get();
            InviteRequests.ClearIfBelowValue(Time.ElapsedTimeFloat - 600f);

            InviteRequests.AddOrSetValue(ref invitee.Entity, Time.ElapsedTimeFloat);
            CommandBuilder.InviteJoinParty(invitee, sender, this);
        }

        CommandBuilder.SendActionResult(sender, ServerResult.PartyInviteSent);

        return true;
    }

    public bool HasInvite(Player p)
    {
        if (InviteRequests == null)
            return false;

        return InviteRequests.GetValueOrDefault(p.Entity, -9999) > Time.ElapsedTimeFloat - 600f;
    }

    public void AddMember(Player player)
    {
        using (writeLock.EnterScope())
            AddMemberInfo(player);

        RoDatabase.EnqueueDbRequest(new UpdatePartyStatusRequest(player.Id, PartyId));

        CommandBuilder.NotifyPartyOfChange(this, player.PartyMemberId, PartyUpdateType.AddPlayer);
        CommandBuilder.AcceptPartyInvite(player);
        CommandBuilder.NotifyNearbyPlayersOfPartyChangeAutoVis(player);
    }

    public void RemoveMember(int memberId)
    {
        if (PartyMemberInfo.TryGetValue(memberId, out var member))
        {
            if (member.Entity.TryGet<Player>(out var p)) //online player
            {
                RemoveMember(p);
                return;
            }

            //offline player
            PartyMemberInfo.Remove(memberId);
            CommandBuilder.NotifyPartyOfChange(this, memberId, PartyUpdateType.RemovePlayer);

            if (PartyMemberInfo.Count == 0)
                EndParty();
            else
                RoDatabase.EnqueueDbRequest(new UpdatePartyStatusRequest(member.PlayerId, 0));
        }
    }

    public void RemoveMember(Player player)
    {
        CommandBuilder.NotifyPartyOfChange(this, player.PartyMemberId, PartyUpdateType.RemovePlayer);

        var memberId = player.PartyMemberId;

        using (writeLock.EnterScope())
        {
            OnlineMembers.Remove(ref player.Entity);
            PartyMemberInfo.Remove(player.PartyMemberId);
            PlayerIdToMemberId.Remove(player.Id);

            player.Party = null;
            player.PartyMemberId = 0;
        }

        if (PartyOwnerId == memberId || PartyOwnerId < 0)
            PromoteRandomToLeader();

        CommandBuilder.NotifyNearbyPlayersOfPartyChangeAutoVis(player);

        if (PartyMemberInfo.Count == 0)
            EndParty();
        else
            RoDatabase.EnqueueDbRequest(new UpdatePartyStatusRequest(player.Id, 0));
    }
    
    public void LogMemberIn(Player p)
    {
        if (!PlayerIdToMemberId.TryGetValue(p.Id, out var memberId))
        {
            ServerLogger.LogWarning($"Performing call to LogMemberIn, but the member isn't actually a part of this party!");
            return;
        }

        using (writeLock.EnterScope())
        {
            PartyMemberInfo[memberId] = new PartyMemberInfo(p.Id, p.Name, p.GetData(PlayerStat.Level), p.Character.Id, p.Entity, true);
            OnlineMembers.Add(p.Entity);
            p.PartyMemberId = memberId;

            if (OnlineMembers.Count == 1)
            {
                PartyOwnerId = memberId;
                PartyOwner = p.Entity;
            }
        }

        CommandBuilder.NotifyPartyOfChange(this, memberId, PartyUpdateType.LogIn);
    }

    public void LogMemberOut(Player p)
    {
        if (!PlayerIdToMemberId.TryGetValue(p.Id, out var memberId))
        {
            ServerLogger.LogWarning($"Performing call to LogMemberOut, but the member isn't actually a part of this party!");
            return;
        }

        using (writeLock.EnterScope())
        {
            PartyMemberInfo[memberId] = new PartyMemberInfo(p.Id, p.Name, p.GetData(PlayerStat.Level), -1, Entity.Null, false);
            OnlineMembers.Remove(ref p.Entity);
            if (PartyOwnerId == memberId || PartyOwnerId < 0)
                PromoteRandomToLeader();
        }

        CommandBuilder.NotifyPartyOfChange(this, memberId, PartyUpdateType.LogOut);
    }

    public void PromoteMemberToLeader(int memberId)
    {
        if (PartyOwnerId == memberId)
            return;
        if (PartyMemberInfo.TryGetValue(memberId, out var member) && member.Entity.IsAlive())
        {
            using (writeLock.EnterScope())
            {
                PartyOwnerId = memberId;
                PartyOwner = member.Entity;
            }

            CommandBuilder.NotifyPartyOfChange(this, memberId, PartyUpdateType.ChangeLeader);
        }
    }

    public void Disband()
    {
        CommandBuilder.NotifyPartyOfChange(this, -1, PartyUpdateType.DisbandParty);
        foreach (var e in OnlineMembers)
        {
            if (e.TryGet<Player>(out var player))
            {
                player.Party = null;
                CommandBuilder.NotifyNearbyPlayersOfPartyChangeAutoVis(player);
            }
        }

        EndParty(); //we don't need to send individual db requests as EndParty will remove everyone's party ids
    }

    private void EndParty()
    {
        RoDatabase.EnqueueDbRequest(new DeletePartyRequest(PartyId));

        World.Instance.RemoveParty(this);
        EntityListPool.Return(OnlineMembers);
        OnlineMembers = null!;
        PartyId = -1;

        //if we were cool we'd put this in a pool
    }
    
    public void UpdateOfflineMembers()
    {
        foreach (var (id, m) in PartyMemberInfo)
        {
            if ((m.EntityId > 0 || m.IsOnline) && !m.Entity.IsAlive())
            {
                using (writeLock.EnterScope())
                {
                    CommandBuilder.NotifyPartyOfChange(this, id, PartyUpdateType.LogOut);
                    PartyMemberInfo[id] = m with { EntityId = -1, Entity = Entity.Null, IsOnline = false };
                }
            }
        }

        using (writeLock.EnterScope())
        {
            OnlineMembers.ClearInactive();

            if (!PartyOwner.IsAlive())
                PromoteRandomToLeader();
        }
    }

    public void PromoteRandomToLeader(bool clearInactive = true)
    {
        if (clearInactive)
            OnlineMembers.ClearInactive();

        PartyOwner = Entity.Null;
        PartyOwnerId = -1;

        if (OnlineMembers.Count == 0)
            return;

        var leaderEntity = OnlineMembers[GameRandom.Next(0, OnlineMembers.Count)];
        if (leaderEntity.TryGet<Player>(out var leader))
        {
            PartyOwner = leader.Entity;
            PartyOwnerId = leader.PartyMemberId;
            CommandBuilder.NotifyPartyOfChange(this, PartyOwnerId, PartyUpdateType.ChangeLeader);
        }
    }

    public void SerializePartyMemberInfo(OutboundMessage packet, PartyMemberInfo info, int id = -1)
    {
        if (id == -1)
            PlayerIdToMemberId.TryGetValue(info.PlayerId, out id);

        packet.Write(id);
        packet.Write(info.EntityId);
        packet.Write((short)info.Level);
        packet.Write(info.Name);
        packet.Write((byte)(info.EntityId > 0 && id == PartyOwnerId ? 1 : 0));
        if (info.EntityId > 0 && info.Entity.TryGet<Player>(out var p))
        {
            packet.Write(p.Character.Map != null ? p.Character.Map.Name : "");
            packet.Write(p.GetStat(CharacterStat.Hp));
            packet.Write(p.GetStat(CharacterStat.MaxHp));
            packet.Write(p.GetStat(CharacterStat.Sp));
            packet.Write(p.GetStat(CharacterStat.MaxSp));
        }
    }

    public void SerializePartyInfo(OutboundMessage packet)
    {
        packet.Write(PartyMemberInfo.Count);
        foreach (var (id, info) in PartyMemberInfo)
        {
            SerializePartyMemberInfo(packet, info, id);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ValidateWriteAccess()
    {
#if DEBUG
        if (!ZoneWorker.IsMainThread)
        {
            ServerLogger.LogWarning(
                $"Warning! Writing to the party list while outside of the main thread is dangerous! " + 
                "You should try to make sure all party changes are scheduled correctly on the main thread.");
            return false;
        }

        if (PartyId == -1)
        {
            ServerLogger.LogWarning($"Warning! Attempting to write to a party that has already disbanded.");
            return false;
        }
#endif
        return true;
    }
}