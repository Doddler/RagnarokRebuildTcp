namespace Assets.Scripts.UI.ConfigWindow
{
    public enum OptionKind { Header, Toggle, Slider, Sound }

    public readonly struct OptionRow
    {
        public readonly OptionKind Kind;
        public readonly OptionCategory Category;
        public readonly string Label;
        public readonly BoolOption Bool;    // Toggle, or the mute toggle of a Sound row
        public readonly RangeOption Range;  // Slider, or the volume slider of a Sound row
        public readonly float Min, Max;     // Slider / Sound
        public readonly bool WholeNumbers;  // Slider / Sound
        public readonly bool ApplyOnRelease; // Slider (expensive applies, e.g. UI scale)

        private OptionRow(OptionKind kind, OptionCategory cat, string label, BoolOption b, RangeOption r,
            float min, float max, bool whole, bool applyOnRelease)
        {
            Kind = kind; Category = cat; Label = label; Bool = b; Range = r;
            Min = min; Max = max; WholeNumbers = whole; ApplyOnRelease = applyOnRelease;
        }

        public static OptionRow Header(OptionCategory cat, string label)
            => new(OptionKind.Header, cat, label, default, default, 0, 0, false, false);

        public static OptionRow Toggle(OptionCategory cat, string label, BoolOption option)
            => new(OptionKind.Toggle, cat, label, option, default, 0, 0, false, false);

        public static OptionRow Slider(OptionCategory cat, string label, RangeOption option,
            float min, float max, bool wholeNumbers = false, bool applyOnRelease = false)
            => new(OptionKind.Slider, cat, label, default, option, min, max, wholeNumbers, applyOnRelease);

        public static OptionRow Sound(OptionCategory cat, string label, RangeOption volume, BoolOption mute)
            => new(OptionKind.Sound, cat, label, mute, volume, 0f, 100f, true, false);
    }

    public static partial class GameConfig
    {
        public static readonly OptionRow[] Layout =
        {
            // ---- Game ----
            OptionRow.Header(OptionCategory.Game, "Display"),
            OptionRow.Slider(OptionCategory.Game, "Damage indicator size", RangeOption.DamageNumberSize, 0.2f, 2f),
            OptionRow.Slider(OptionCategory.Game, "Damage indicator spacing", RangeOption.DamageSpacingSize, 0.4f, 1f),
            OptionRow.Toggle(OptionCategory.Game, "Use old-school damage numbers", BoolOption.UseSpriteBasedDamageNumbers),
            OptionRow.Toggle(OptionCategory.Game, "Enable sprite filtering (pixelated)", BoolOption.EnableSpriteFiltering),
            OptionRow.Toggle(OptionCategory.Game, "Enable X-ray view", BoolOption.EnableXRay),
            OptionRow.Toggle(OptionCategory.Game, "Show exp gain over character", BoolOption.ShowExpGainOnKill),
            OptionRow.Toggle(OptionCategory.Game, "Tab shows walkable tiles", BoolOption.AllowTabToShowWalkTable),

            OptionRow.Header(OptionCategory.Game, "Chat"),
            OptionRow.Toggle(OptionCategory.Game, "Hide shout chat", BoolOption.HideShoutChat),
            OptionRow.Toggle(OptionCategory.Game, "Show exp gain in chat", BoolOption.ShowExpGainInChat),

            OptionRow.Header(OptionCategory.Game, "Skills"),
            OptionRow.Toggle(OptionCategory.Game, "Show all skills, even locked", BoolOption.ShowAllSkillsInSkillWindow),
            OptionRow.Toggle(OptionCategory.Game, "Auto-lock skill points", BoolOption.AutoLockSkillWindow),

            // ---- UI ----
            OptionRow.Header(OptionCategory.UI, "UI Size Settings"),
            OptionRow.Slider(OptionCategory.UI, "UI scale", RangeOption.MasterUIScale, 6f, 11f, wholeNumbers: true, applyOnRelease: true),
            OptionRow.Toggle(OptionCategory.UI, "Scale UI with window size", BoolOption.ScaleUiWithResolution),
            OptionRow.Toggle(OptionCategory.UI, "Keep windows fully on screen", BoolOption.KeepWindowsOnScreen),

            OptionRow.Header(OptionCategory.UI, "Floating Display"),
            OptionRow.Toggle(OptionCategory.UI, "Scale floating display with zoom", BoolOption.ScalePlayerDisplayWithZoom),
            OptionRow.Toggle(OptionCategory.UI, "Show monster HP bars", BoolOption.ShowMonsterHpBars),
            OptionRow.Toggle(OptionCategory.UI, "Hide full HP/MP bars", BoolOption.AutoHideFullHPBars),
            OptionRow.Toggle(OptionCategory.UI, "Show levels next to names", BoolOption.ShowLevelsInOverlay),
            OptionRow.Toggle(OptionCategory.UI, "Lower HP bar if necessary (sitting)", BoolOption.AdjustOverlayWhenSitting),

            OptionRow.Header(OptionCategory.UI, "Player Unit Frame"),
            OptionRow.Toggle(OptionCategory.UI, "Show current/required base exp", BoolOption.ShowBaseExpValue),
            OptionRow.Toggle(OptionCategory.UI, "Show base exp percent progress", BoolOption.ShowBaseExpPercent),
            OptionRow.Toggle(OptionCategory.UI, "Show current/required job exp", BoolOption.ShowJobExpValue),
            OptionRow.Toggle(OptionCategory.UI, "Show job exp percent progress", BoolOption.ShowJobExpPercent),

            // ---- Experimental ----
            OptionRow.Header(OptionCategory.Experimental, "Experimental"),
            OptionRow.Toggle(OptionCategory.Experimental, "Enable WASD controls", BoolOption.EnableWASDControls),

            // ---- Audio ----
            OptionRow.Header(OptionCategory.Audio, "Audio Volume"),
            OptionRow.Sound(OptionCategory.Audio, "Master", RangeOption.VolumeMaster, BoolOption.EnableMaster),
            OptionRow.Sound(OptionCategory.Audio, "BGM", RangeOption.VolumeBgm, BoolOption.EnableBgm),
            OptionRow.Sound(OptionCategory.Audio, "Effects", RangeOption.VolumeEffects, BoolOption.EnableEffects),
            OptionRow.Sound(OptionCategory.Audio, "Environment", RangeOption.VolumeEnvironment, BoolOption.EnableEnvironment),
        };
    }
}
