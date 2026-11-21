namespace Assets.Scripts.UI.ConfigWindow
{
    public enum OptionKind { Header, Toggle, Slider, Sound, ToggleNumber, List }

    // A row's range comes from GameConfig.BoundsFor.
    public readonly struct OptionRow
    {
        public readonly OptionKind Kind;
        public readonly OptionCategory Category;
        public readonly string Label;
        public readonly BoolOption Bool;    // Toggle, the mute toggle of a Sound row, or the toggle of a ToggleNumber row
        public readonly RangeOption Range;  // Slider, the volume slider of a Sound row, or the number of a ToggleNumber row
        public readonly ListOption List;
        public readonly bool WholeNumbers;  // Slider / Sound
        public readonly bool ApplyOnRelease; // Slider (expensive applies, e.g. UI scale)

        private OptionRow(OptionKind kind, OptionCategory cat, string label, BoolOption b, RangeOption r,
            bool whole, bool applyOnRelease, ListOption list = default)
        {
            Kind = kind; Category = cat; Label = label; Bool = b; Range = r;
            WholeNumbers = whole; ApplyOnRelease = applyOnRelease; List = list;
        }

        public static OptionRow Header(OptionCategory cat, string label)
            => new(OptionKind.Header, cat, label, default, default, false, false);

        public static OptionRow Toggle(OptionCategory cat, string label, BoolOption option)
            => new(OptionKind.Toggle, cat, label, option, default, false, false);

        public static OptionRow Slider(OptionCategory cat, string label, RangeOption option,
            bool wholeNumbers = false, bool applyOnRelease = false)
            => new(OptionKind.Slider, cat, label, default, option, wholeNumbers, applyOnRelease);

        public static OptionRow Sound(OptionCategory cat, string label, RangeOption volume, BoolOption mute)
            => new(OptionKind.Sound, cat, label, mute, volume, true, false);

        public static OptionRow ToggleNumber(OptionCategory cat, string label, BoolOption enabled, RangeOption value)
            => new(OptionKind.ToggleNumber, cat, label, enabled, value, true, false);

        public static OptionRow Dropdown(OptionCategory cat, string label, ListOption option)
            => new(OptionKind.List, cat, label, default, default, false, false, option);
    }

    public static partial class GameConfig
    {
        public static readonly OptionRow[] Layout =
        {
            // ---- Game ----
            OptionRow.Header(OptionCategory.Game, "Graphics"),
            OptionRow.Toggle(OptionCategory.Game, "Enable sprite filtering (pixelated)", BoolOption.EnableSpriteFiltering),
            OptionRow.Toggle(OptionCategory.Game, "Enable X-ray view", BoolOption.EnableXRay),
            OptionRow.Toggle(OptionCategory.Game, "Enable fog", BoolOption.EnableFog),
            OptionRow.ToggleNumber(OptionCategory.Game, "Foreground FPS cap", BoolOption.LimitForegroundFps, RangeOption.ForegroundFpsLimit),
            OptionRow.ToggleNumber(OptionCategory.Game, "Background FPS cap", BoolOption.LimitBackgroundFps, RangeOption.BackgroundFpsLimit),

            OptionRow.Header(OptionCategory.Game, "Display"),
            OptionRow.Slider(OptionCategory.Game, "Damage indicator size", RangeOption.DamageNumberSize),
            OptionRow.Slider(OptionCategory.Game, "Damage indicator spacing", RangeOption.DamageSpacingSize),
            OptionRow.Toggle(OptionCategory.Game, "Use old-school damage numbers", BoolOption.UseSpriteBasedDamageNumbers),
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
            OptionRow.Slider(OptionCategory.UI, "UI scale", RangeOption.MasterUIScale, wholeNumbers: true, applyOnRelease: true),
            OptionRow.Toggle(OptionCategory.UI, "Scale UI with window size", BoolOption.ScaleUiWithResolution),
            OptionRow.Toggle(OptionCategory.UI, "Keep windows fully on screen", BoolOption.KeepWindowsOnScreen),

            OptionRow.Header(OptionCategory.UI, "Minimap"),
            OptionRow.Toggle(OptionCategory.UI, "Enable minimap", BoolOption.EnableMinimap),
            OptionRow.Slider(OptionCategory.UI, "Map opacity", RangeOption.MinimapOpacity, wholeNumbers: true, applyOnRelease: true),
            OptionRow.Slider(OptionCategory.UI, "Icon opacity", RangeOption.MinimapIconOpacity, wholeNumbers: true, applyOnRelease: true),

            OptionRow.Header(OptionCategory.UI, "Chat"),
            OptionRow.Slider(OptionCategory.UI, "Chat width", RangeOption.ChatWidth, wholeNumbers: true),
            OptionRow.Slider(OptionCategory.UI, "Chat lines", RangeOption.ChatLines, wholeNumbers: true),

            OptionRow.Header(OptionCategory.UI, "Floating Display"),
            OptionRow.Toggle(OptionCategory.UI, "Scale floating display with zoom", BoolOption.ScalePlayerDisplayWithZoom),
            OptionRow.Toggle(OptionCategory.UI, "Show monster HP bars", BoolOption.ShowMonsterHpBars),
            OptionRow.Toggle(OptionCategory.UI, "Show other players' HP bars", BoolOption.ShowOtherPlayerHpBars),
            OptionRow.Toggle(OptionCategory.UI, "Show party names", BoolOption.ShowPartyNameInOverlay),
            OptionRow.Toggle(OptionCategory.UI, "Lower HP bar if necessary (sitting)", BoolOption.AdjustOverlayWhenSitting),

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

        // The title screen's mini panel, with its own labels and order.
        public static readonly OptionRow[] TitleScreenLayout =
        {
            OptionRow.Sound(OptionCategory.Audio, "Volume", RangeOption.VolumeMaster, BoolOption.EnableMaster),
            OptionRow.Slider(OptionCategory.UI, "UI scale", RangeOption.MasterUIScale, wholeNumbers: true, applyOnRelease: true),
        };
    }
}
