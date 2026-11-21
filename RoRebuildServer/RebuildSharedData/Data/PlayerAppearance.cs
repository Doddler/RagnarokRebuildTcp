using System;
using RebuildSharedData.Packets;
using RebuildSharedData.Util;

namespace RebuildSharedData.Data
{
    //How a player looks in portraits; Write and Read define its packet format.
    public readonly struct PlayerAppearance : IEquatable<PlayerAppearance>
    {
        public readonly int JobId;
        public readonly int HairStyleId;
        public readonly int HairColorId;
        public readonly bool IsMale;
        public readonly int Headgear1;
        public readonly int Headgear2;
        public readonly int Headgear3;

        public PlayerAppearance(int jobId, int hairStyleId, int hairColorId, bool isMale,
            int headgear1 = 0, int headgear2 = 0, int headgear3 = 0)
        {
            JobId = jobId;
            HairStyleId = hairStyleId;
            HairColorId = hairColorId;
            IsMale = isMale;
            Headgear1 = headgear1;
            Headgear2 = headgear2;
            Headgear3 = headgear3;
        }

        public static PlayerAppearance FromSpawn(in EntitySpawnParameters spawn, in PlayerSpawnParameters player) =>
            new PlayerAppearance(spawn.ClassId, player.HeadType, player.HairColor, player.IsMale, player.Headgear1, player.Headgear2, player.Headgear3);

        //older saves can have a shorter summary; missing values read as 0
        public static PlayerAppearance FromSummary(ReadOnlySpan<int> summary) =>
            new PlayerAppearance(SummaryValue(summary, PlayerSummaryData.JobId), SummaryValue(summary, PlayerSummaryData.HeadId),
                SummaryValue(summary, PlayerSummaryData.HairColor), SummaryValue(summary, PlayerSummaryData.Gender) == 0,
                SummaryValue(summary, PlayerSummaryData.Headgear1), SummaryValue(summary, PlayerSummaryData.Headgear2),
                SummaryValue(summary, PlayerSummaryData.Headgear3));

        public void WriteSummary(Span<int> summary)
        {
            summary[(int)PlayerSummaryData.JobId] = JobId;
            summary[(int)PlayerSummaryData.HeadId] = HairStyleId;
            summary[(int)PlayerSummaryData.HairColor] = HairColorId;
            summary[(int)PlayerSummaryData.Gender] = IsMale ? 0 : 1;
            summary[(int)PlayerSummaryData.Headgear1] = Headgear1;
            summary[(int)PlayerSummaryData.Headgear2] = Headgear2;
            summary[(int)PlayerSummaryData.Headgear3] = Headgear3;
        }

        public PlayerAppearance WithHeadgear(int headgear1, int headgear2, int headgear3) =>
            new PlayerAppearance(JobId, HairStyleId, HairColorId, IsMale, headgear1, headgear2, headgear3);

        public void Write(IBinaryMessageWriter packet)
        {
            packet.Write((byte)HairStyleId);
            packet.Write((byte)HairColorId);
            packet.Write(IsMale);
            packet.Write((short)JobId);
            packet.Write(Headgear1);
            packet.Write(Headgear2);
            packet.Write(Headgear3);
        }

        public static PlayerAppearance Read(IBinaryMessageReader packet)
        {
            var hairStyle = packet.ReadByte();
            var hairColor = packet.ReadByte();
            var isMale = packet.ReadBoolean();
            var job = packet.ReadInt16();
            var headgear1 = packet.ReadInt32();
            var headgear2 = packet.ReadInt32();
            var headgear3 = packet.ReadInt32();
            return new PlayerAppearance(job, hairStyle, hairColor, isMale, headgear1, headgear2, headgear3);
        }

        private static int SummaryValue(ReadOnlySpan<int> summary, PlayerSummaryData field) =>
            (int)field < summary.Length ? summary[(int)field] : 0;

        public bool Equals(PlayerAppearance other) =>
            JobId == other.JobId && HairStyleId == other.HairStyleId && HairColorId == other.HairColorId && IsMale == other.IsMale
            && Headgear1 == other.Headgear1 && Headgear2 == other.Headgear2 && Headgear3 == other.Headgear3;

        public override bool Equals(object? obj) => obj is PlayerAppearance other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(JobId, HairStyleId, HairColorId, IsMale, Headgear1, Headgear2, Headgear3);

        public static bool operator ==(PlayerAppearance a, PlayerAppearance b) => a.Equals(b);
        public static bool operator !=(PlayerAppearance a, PlayerAppearance b) => !a.Equals(b);
    }
}
