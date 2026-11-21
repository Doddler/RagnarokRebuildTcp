using System;
using System.Collections.Generic;
using Assets.Scripts.Data;
using Assets.Scripts.Network;
using RebuildSharedData.Data;

namespace Assets.Scripts.PlayerControl
{
    //The player's party. Owned by PlayerState, which it asks for the player's own entity id and name.
    public class PartyState
    {
        //the party was joined or left; the member list was replaced
        public event Action Changed;
        public event Action LeaderChanged;
        //entity id of an online player whose party membership may have changed
        public event Action<int> MembershipChanged;
        public event Action<PartyMemberInfo> MemberAdded;
        public event Action<int> MemberChanged;
        //their entity came into view or left it
        public event Action<int> MemberEntityChanged;
        public event Action<int> MemberRemoved;
        public event Action<int> MemberVitalsChanged;

        private readonly PlayerState player;

        public bool IsInParty { get; private set; }
        public int Id { get; private set; }
        public string Name { get; private set; }
        public int LeaderId { get; private set; } = -1;
        //the player's own member id
        public int MemberId { get; private set; }
        public int InvitedPartyId = -1;
        private readonly Dictionary<int, PartyMemberInfo> members = new();
        //entity id to member id, for online members on any map
        private readonly Dictionary<int, int> memberIdLookup = new();
        public IReadOnlyDictionary<int, PartyMemberInfo> Members => members;
        public IReadOnlyDictionary<int, int> MemberIdLookup => memberIdLookup;

        public bool IsLeader => IsInParty && LeaderId == MemberId;

        public PartyState(PlayerState player) => this.player = player;

        //No events: the previous scene's listeners are going away.
        public void ResetForNewSession()
        {
            InvitedPartyId = -1;
            ResetFields();
        }

        private void ResetFields()
        {
            IsInParty = false;
            Id = 0;
            Name = null;
            LeaderId = -1;
            MemberId = 0;
            foreach (var member in members.Values)
                LinkMemberEntity(member, null);
            members.Clear();
            memberIdLookup.Clear();
        }

        public bool IsMemberEntity(int entityId) =>
            IsInParty && (entityId == player.EntityId || memberIdLookup.ContainsKey(entityId));

        public bool TryGetMember(int entityId, out PartyMemberInfo member)
        {
            member = null;
            return IsInParty
                   && memberIdLookup.TryGetValue(entityId, out var memberId)
                   && members.TryGetValue(memberId, out member);
        }

        public void AssignMemberEntity(int entityId, ServerControllable controllable)
        {
            if (!TryGetMember(entityId, out var member) || ReferenceEquals(member.Controllable, controllable))
                return;

            LinkMemberEntity(member, controllable);
            MemberEntityChanged?.Invoke(member.PartyMemberId);
        }

        private void LinkMemberEntity(PartyMemberInfo member, ServerControllable controllable)
        {
            if (!ReferenceEquals(member.Controllable, null))
                member.Controllable.Left -= OnMemberEntityLeft;
            member.Controllable = controllable;
            if (!ReferenceEquals(controllable, null))
                controllable.Left += OnMemberEntityLeft;
        }

        //AddEntity links a replacement before the old entity leaves, so only the current entity is released
        private void OnMemberEntityLeft(ServerControllable entity, ServerControllable replacement)
        {
            if (!TryGetMember(entity.Id, out var member) || !ReferenceEquals(member.Controllable, entity))
                return;

            member.Controllable = null;
            MemberEntityChanged?.Invoke(member.PartyMemberId);
        }

        public void Join(int partyId, string partyName, IReadOnlyList<PartyMemberData> partyMembers)
        {
            ResetFields();
            IsInParty = true;
            Id = partyId;
            Name = partyName;
            MembershipChanged?.Invoke(player.EntityId);

            foreach (var member in partyMembers)
                RegisterOrUpdateMember(member, out _);

            Changed?.Invoke();
            LeaderChanged?.Invoke();
        }

        public void AddMember(in PartyMemberData data)
        {
            var leaderChanged = RegisterOrUpdateMember(data, out var info);
            MemberAdded?.Invoke(info);
            if (leaderChanged)
                LeaderChanged?.Invoke();
        }

        public void UpdateMember(in PartyMemberData data)
        {
            var leaderChanged = RegisterOrUpdateMember(data, out _);
            MemberChanged?.Invoke(data.MemberId);
            if (leaderChanged)
                LeaderChanged?.Invoke();
        }

        public bool ChangeLeader(int newLeaderId)
        {
            if (!members.TryGetValue(newLeaderId, out var leader))
                return false;

            var oldLeaderId = LeaderId;
            if (members.TryGetValue(oldLeaderId, out var oldLeader))
                oldLeader.IsLeader = false;

            leader.IsLeader = true;
            LeaderId = newLeaderId;
            MemberChanged?.Invoke(oldLeaderId);
            MemberChanged?.Invoke(newLeaderId);
            LeaderChanged?.Invoke();
            return true;
        }

        public void SetMemberMap(int memberId, string mapName)
        {
            if (!members.TryGetValue(memberId, out var member))
                return;

            member.Map = mapName;
            MemberChanged?.Invoke(memberId);
        }

        //the server only sends party hp to members who can't see each other
        public void SetMemberHp(int entityId, int hp, int maxHp)
        {
            if (!TryGetMember(entityId, out var member))
                return;

            member.Hp = hp;
            member.MaxHp = maxHp;
            MemberVitalsChanged?.Invoke(member.PartyMemberId);
        }

        public void SetMemberVitals(int memberId, int hp, int maxHp, int sp, int maxSp)
        {
            if (!members.TryGetValue(memberId, out var member))
                return;

            member.Hp = hp;
            member.MaxHp = maxHp;
            member.Sp = sp;
            member.MaxSp = maxSp;
            MemberVitalsChanged?.Invoke(memberId);
        }

        //updates a known member's info in place, so holders of it stay current; true when it moved the leader
        private bool RegisterOrUpdateMember(in PartyMemberData data, out PartyMemberInfo info)
        {
            var memberId = data.MemberId;
            var entityId = data.EntityId;
            if (!members.TryGetValue(memberId, out info))
            {
                info = new PartyMemberInfo();
                members.Add(memberId, info);
            }

            var oldEntityId = info.EntityId;
            var wasOnline = info.IsOnline;
            info.Apply(data);
            if (info.PlayerName == player.PlayerName || entityId == player.EntityId)
                MemberId = memberId;

            //logged out (the update carries -1) or back with a new entity
            if (wasOnline && oldEntityId != entityId)
            {
                LinkMemberEntity(info, null);
                memberIdLookup.Remove(oldEntityId);
                MembershipChanged?.Invoke(oldEntityId);
            }

            if (info.IsOnline)
            {
                //NetworkManager links an entity already in view, and one that comes into view later
                memberIdLookup[entityId] = memberId;
                MembershipChanged?.Invoke(entityId);
            }

            if (!info.IsLeader || LeaderId == memberId)
                return false;
            LeaderId = memberId;
            return true;
        }

        public PartyMemberInfo RemoveMember(int memberId)
        {
            members.Remove(memberId, out var info);
            if (LeaderId == memberId)
                LeaderId = -1;

            if (info is { IsOnline: true })
            {
                LinkMemberEntity(info, null);
                memberIdLookup.Remove(info.EntityId);
                MembershipChanged?.Invoke(info.EntityId);
            }

            if (info != null)
                MemberRemoved?.Invoke(memberId);
            return info;
        }

        public void Leave()
        {
            var formerMembers = new List<int>(memberIdLookup.Keys);
            if (!formerMembers.Contains(player.EntityId))
                formerMembers.Add(player.EntityId);
            ResetFields();

            foreach (var entityId in formerMembers)
                MembershipChanged?.Invoke(entityId);
            Changed?.Invoke();
            LeaderChanged?.Invoke();
        }
    }
}
