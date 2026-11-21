using RebuildSharedData.Util;

namespace RebuildSharedData.Data
{
    //One party list row; Write and Read define its packet format.
    public readonly struct PartyMemberData
    {
        public readonly int MemberId;
        public readonly int EntityId;
        public readonly int Level;
        public readonly string Name;
        public readonly bool IsLeader;
        public readonly PlayerAppearance Appearance;
        //online members only
        public readonly string? Map;
        public readonly int Hp;
        public readonly int MaxHp;
        public readonly int Sp;
        public readonly int MaxSp;
        public readonly int JobLevel;

        public bool IsOnline => EntityId > 0;

        public PartyMemberData(int memberId, int entityId, int level, string name, bool isLeader, PlayerAppearance appearance,
            string? map, int hp, int maxHp, int sp, int maxSp, int jobLevel)
        {
            MemberId = memberId;
            EntityId = entityId;
            Level = level;
            Name = name;
            IsLeader = isLeader;
            Appearance = appearance;
            Map = map;
            Hp = hp;
            MaxHp = maxHp;
            Sp = sp;
            MaxSp = maxSp;
            JobLevel = jobLevel;
        }

        public static PartyMemberData Offline(int memberId, int level, string name, PlayerAppearance appearance) =>
            new PartyMemberData(memberId, 0, level, name, false, appearance, null, 0, 0, 0, 0, 0);

        public void Write(IBinaryMessageWriter packet)
        {
            packet.Write(MemberId);
            packet.Write(EntityId);
            packet.Write((short)Level);
            packet.Write(Name);
            packet.Write(IsLeader);
            Appearance.Write(packet);
            if (!IsOnline)
                return;

            packet.Write(Map ?? "");
            packet.Write(Hp);
            packet.Write(MaxHp);
            packet.Write(Sp);
            packet.Write(MaxSp);
            packet.Write((short)JobLevel);
        }

        public static PartyMemberData Read(IBinaryMessageReader packet)
        {
            var memberId = packet.ReadInt32();
            var entityId = packet.ReadInt32();
            var level = (int)packet.ReadInt16();
            var name = packet.ReadString();
            var isLeader = packet.ReadBoolean();
            var appearance = PlayerAppearance.Read(packet);
            if (entityId <= 0)
                return Offline(memberId, level, name, appearance);

            var map = packet.ReadString();
            var hp = packet.ReadInt32();
            var maxHp = packet.ReadInt32();
            var sp = packet.ReadInt32();
            var maxSp = packet.ReadInt32();
            var jobLevel = (int)packet.ReadInt16();
            return new PartyMemberData(memberId, entityId, level, name, isLeader, appearance, map, hp, maxHp, sp, maxSp, jobLevel);
        }
    }
}
