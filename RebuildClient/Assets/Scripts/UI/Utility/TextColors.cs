namespace Assets.Scripts.UI.Utility
{
    public enum TextColor
    {
        Normal,
        Party,
        Skill,
        Job,
        Equipment,
        Item,
        Error,
        System
    }

    public static class TextColors
    {
        public const string Say = "#FFFFFF";
        public const string Party = "#F0C008";
        public const string Shout = "#E68C5A";

        public const string Error = "#ED0000";
        public const string System = "#FFFF00";
        public const string Job = "#99CCFF";
        public const string Item = "#00FBFB";
        public const string Equip = "#00F6C7";
        public const string Unequip = "#C80000";
    }
}
