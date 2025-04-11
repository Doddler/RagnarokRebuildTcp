using System;
using Assets.Scripts.Network;

namespace Assets.Scripts.Data
{
    public class PartyMemberInfo : IComparable
    {
        public int PartyMemberId;
        public int EntityId;
        public int Level;
        public string Map;
        public string PlayerName;
        public bool IsLeader;
        public ServerControllable Controllable;
        public int Hp;
        public int MaxHp;
        public int Sp;
        public int MaxSp;
        
        //comparer sorts by online status, then by name
        public int CompareTo(object obj)
        {
            if (obj is not PartyMemberInfo other)
                return 0;

            if (EntityId > 0 && other.EntityId > 0)
                return String.Compare(PlayerName, other.PlayerName, StringComparison.Ordinal);

            if (EntityId > 0)
                return -1;

            if (other.EntityId > 0)
                return 1;

            return String.Compare(PlayerName, other.PlayerName, StringComparison.Ordinal);
        }
    }
}