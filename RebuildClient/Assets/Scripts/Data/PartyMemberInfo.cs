using System;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using RebuildSharedData.Data;

namespace Assets.Scripts.Data
{
    public class PartyMemberInfo : IComparable<PartyMemberInfo>
    {
        public int PartyMemberId;
        public int EntityId;
        public int Level;
        //online members only
        public int JobLevel;
        public string Map;
        public string PlayerName;
        public bool IsLeader;
        public ServerControllable Controllable;
        public int Hp;
        public int MaxHp;
        public int Sp;
        public int MaxSp;
        //offline members show their last known look
        public PlayerAppearance Appearance;

        public bool IsOnline => EntityId > 0;

        //the linked Controllable is PartyState's to change
        public void Apply(in PartyMemberData data)
        {
            PartyMemberId = data.MemberId;
            EntityId = data.EntityId;
            Level = data.Level;
            JobLevel = data.JobLevel;
            Map = data.Map;
            PlayerName = data.Name;
            IsLeader = data.IsLeader;
            Hp = data.Hp;
            MaxHp = data.MaxHp;
            Sp = data.Sp;
            MaxSp = data.MaxSp;
            Appearance = data.Appearance;
        }

        public int CompareTo(PartyMemberInfo other)
        {
            if (IsLeader != other.IsLeader)
                return IsLeader ? -1 : 1;

            if (IsOnline != other.IsOnline)
                return IsOnline ? -1 : 1;

            return String.Compare(PlayerName, other.PlayerName, StringComparison.OrdinalIgnoreCase);
        }
    }
}