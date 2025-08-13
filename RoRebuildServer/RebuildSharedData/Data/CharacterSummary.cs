namespace RebuildSharedData.Data
{
    public class CharacterSummary
    {
        public short JobId { get; set; }
        public short JobAppearance { get; set; } //eventually clothing dye
        public byte HeadId { get; set; }
        public short HeadAppearance { get; set; } //hair color
        public byte Level { get; set; }
        public int Headgear1;
        public int Headgear2;
        public int Headgear3;
        public string Name = null!;
    }
}
