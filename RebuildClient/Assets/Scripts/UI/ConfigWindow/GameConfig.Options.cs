using System;
using System.Collections.Generic;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum.EntityStats;
using UnityEngine;
using UnityEngine.Audio;

namespace Assets.Scripts.UI.ConfigWindow
{
    public enum BoolOption
    {
        ShowAllSkillsInSkillWindow, AutoLockSkillWindow,
        ShowExpGainOnKill, ScalePlayerDisplayWithZoom, ShowMonsterHpBars, ShowLevelsInOverlay, AutoHideFullHPBars,
        UseUnfilteredSprites, UseSpriteBasedDamageNumbers, AllowTabToShowWalkTable, HideShoutChat, EnableXRay, EnableWASDControls,
        ShowBaseExpValue, ShowBaseExpPercent, ShowJobExpValue, ShowJobExpPercent, ShowExpGainInChat,
        EnableMaster, EnableBgm, EnableEffects, EnableEnvironment
    }

    public enum RangeOption
    {
        DamageNumberSize, DamageSpacingSize, MasterUIScale,
        VolumeMaster, VolumeBgm, VolumeEffects, VolumeEnvironment
    }

    // A get/set pair over a GameConfigData field. GameConfig.Set applies the change after writing.
    public readonly struct ConfigBinding<T>
    {
        public readonly Func<T> Get;
        public readonly Action<T> Set;
        public ConfigBinding(Func<T> get, Action<T> set) { Get = get; Set = set; }
    }

    public static partial class GameConfig
    {
        private static GameConfigData D => Data;

        public static AudioMixer AudioMixer;

        // Reflected by name (ReflectMap), except the UI-scale conversion and array-backed audio channels (bound by hand).
        private static readonly Dictionary<BoolOption, ConfigBinding<bool>> Bools = BuildBools();
        private static readonly Dictionary<RangeOption, ConfigBinding<float>> Ranges = BuildRanges();

        private static Dictionary<BoolOption, ConfigBinding<bool>> BuildBools()
        {
            var map = ReflectMap<BoolOption, bool>();
            map[BoolOption.EnableMaster]      = EnableBinding(ConfigAudioChannel.Master);
            map[BoolOption.EnableBgm]         = EnableBinding(ConfigAudioChannel.Music);
            map[BoolOption.EnableEffects]     = EnableBinding(ConfigAudioChannel.Effects);
            map[BoolOption.EnableEnvironment] = EnableBinding(ConfigAudioChannel.Environment);
            Validate(map);
            return map;
        }

        private static Dictionary<RangeOption, ConfigBinding<float>> BuildRanges()
        {
            var map = ReflectMap<RangeOption, float>();
            // Slider works in tenths; config stores the scale factor (slider / 10).
            map[RangeOption.MasterUIScale] = new(() => D.MasterUIScale * 10f, v => D.MasterUIScale = v / 10f);
            map[RangeOption.VolumeMaster]      = VolumeBinding(ConfigAudioChannel.Master);
            map[RangeOption.VolumeBgm]         = VolumeBinding(ConfigAudioChannel.Music);
            map[RangeOption.VolumeEffects]     = VolumeBinding(ConfigAudioChannel.Effects);
            map[RangeOption.VolumeEnvironment] = VolumeBinding(ConfigAudioChannel.Environment);
            Validate(map);
            return map;
        }

        // Bind each enum member to the same-named, same-typed GameConfigData field; skip members with no match.
        private static Dictionary<TEnum, ConfigBinding<TValue>> ReflectMap<TEnum, TValue>() where TEnum : Enum
        {
            var map = new Dictionary<TEnum, ConfigBinding<TValue>>();
            foreach (TEnum o in Enum.GetValues(typeof(TEnum)))
            {
                var f = typeof(GameConfigData).GetField(o.ToString());
                if (f != null && f.FieldType == typeof(TValue))
                    map[o] = new(() => (TValue)f.GetValue(D), v => f.SetValue(D, v));
            }
            return map;
        }

        private static void Validate<TEnum, TValue>(Dictionary<TEnum, ConfigBinding<TValue>> map) where TEnum : Enum
        {
            foreach (TEnum o in Enum.GetValues(typeof(TEnum)))
                if (!map.ContainsKey(o))
                    Debug.LogError($"GameConfig: {typeof(TEnum).Name}.{o} has no binding (no '{o}' field on GameConfigData and no explicit override) — it will not sync.");
        }

        public static bool Get(BoolOption o) { InitializeIfNecessary(); return Bools[o].Get(); }
        public static void Set(BoolOption o, bool v) { Bools[o].Set(v); ApplyBool(o); }

        public static float Get(RangeOption o) { InitializeIfNecessary(); return Ranges[o].Get(); }
        public static void Set(RangeOption o, float v, bool apply = true) { Ranges[o].Set(v); if (apply) ApplyRange(o); }
        public static void Apply(RangeOption o) => ApplyRange(o);

        public static void ApplyAll()
        {
            foreach (BoolOption o in Enum.GetValues(typeof(BoolOption))) ApplyBool(o);
            foreach (RangeOption o in Enum.GetValues(typeof(RangeOption))) ApplyRange(o);
        }

        // Resets only option-backing fields, leaving other state (window positions, hotbars, saved login) intact.
        public static void ResetToDefaults()
        {
            InitializeIfNecessary();

            var defaults = new GameConfigData();
            defaults.InitDefaultValues();

            foreach (BoolOption o in Enum.GetValues(typeof(BoolOption))) ResetOptionField(o.ToString(), defaults);
            foreach (RangeOption o in Enum.GetValues(typeof(RangeOption))) ResetOptionField(o.ToString(), defaults);

            // Array-backed audio options have no same-named field, so reset their arrays explicitly.
            Data.AudioVolumeLevels = defaults.AudioVolumeLevels;
            Data.AudioMuteValues = defaults.AudioMuteValues;

            ApplyAll();
        }

        private static void ResetOptionField(string name, GameConfigData defaults)
        {
            var f = typeof(GameConfigData).GetField(name);
            if (f != null) f.SetValue(Data, f.GetValue(defaults));
        }

        public static void ApplyAudio()
        {
            foreach (var ch in MixerParam.Keys)
                ApplyAudioChannel(ch);
        }

        // Only options that feed cached/derived runtime state need a case; read-on-use options fall through.
        private static void ApplyBool(BoolOption o)
        {
            switch (o)
            {
                case BoolOption.EnableMaster:      ApplyAudioChannel(ConfigAudioChannel.Master); break;
                case BoolOption.EnableBgm:         ApplyAudioChannel(ConfigAudioChannel.Music); break;
                case BoolOption.EnableEffects:     ApplyAudioChannel(ConfigAudioChannel.Effects); break;
                case BoolOption.EnableEnvironment: ApplyAudioChannel(ConfigAudioChannel.Environment); break;

                case BoolOption.UseUnfilteredSprites:
                    CameraFollower.Instance.SetSmoothPixel(!D.UseUnfilteredSprites);
                    break;
                case BoolOption.UseSpriteBasedDamageNumbers:
                    CameraFollower.Instance.UseTTFDamage = !D.UseSpriteBasedDamageNumbers;
                    break;

                case BoolOption.ShowBaseExpValue:
                case BoolOption.ShowBaseExpPercent:
                case BoolOption.ShowJobExpValue:
                case BoolOption.ShowJobExpPercent:
                case BoolOption.ShowExpGainInChat:
                    RefreshExpDisplay();
                    break;
            }
        }

        private static void ApplyRange(RangeOption o)
        {
            switch (o)
            {
                case RangeOption.VolumeMaster:      ApplyAudioChannel(ConfigAudioChannel.Master); break;
                case RangeOption.VolumeBgm:         ApplyAudioChannel(ConfigAudioChannel.Music); break;
                case RangeOption.VolumeEffects:     ApplyAudioChannel(ConfigAudioChannel.Effects); break;
                case RangeOption.VolumeEnvironment: ApplyAudioChannel(ConfigAudioChannel.Environment); break;

                case RangeOption.MasterUIScale:
                    CameraFollower.Instance.UpdateCameraSize();
                    UiManager.Instance.FitFloatingWindowsIntoPlayArea();
                    break;
            }
        }

        private static void RefreshExpDisplay()
        {
            var state = PlayerState.Instance;
            if (state == null)
                return;

            var cam = CameraFollower.Instance;
            cam.UpdatePlayerExp(state.Exp, cam.ExpForLevel(state.Level));
            cam.UpdatePlayerJobExp(state.GetData(PlayerStat.JobExp),
                ClientDataLoader.Instance.GetJobExpRequired(state.JobId, state.GetData(PlayerStat.JobLevel)));
        }

        private static ConfigBinding<bool> EnableBinding(ConfigAudioChannel ch)
            => new(() => !D.AudioMuteValues[(int)ch], v => D.AudioMuteValues[(int)ch] = !v);

        private static ConfigBinding<float> VolumeBinding(ConfigAudioChannel ch)
            => new(() => D.AudioVolumeLevels[(int)ch], v => D.AudioVolumeLevels[(int)ch] = Mathf.RoundToInt(v));

        private static readonly Dictionary<ConfigAudioChannel, string> MixerParam = new()
        {
            [ConfigAudioChannel.Master] = "Master",
            [ConfigAudioChannel.Music] = "Music",
            [ConfigAudioChannel.Environment] = "Environment",
            [ConfigAudioChannel.Effects] = "Sounds",
        };

        private static void ApplyAudioChannel(ConfigAudioChannel ch)
        {
            if (AudioMixer == null)
                return;
            var linear = D.AudioMuteValues[(int)ch] ? 0f : D.AudioVolumeLevels[(int)ch] / 100f;
            AudioMixer.SetFloat(MixerParam[ch], LinearToDecibel(linear));
        }

        private static float LinearToDecibel(float linear) => linear > 0f ? 20f * Mathf.Log10(linear) : -144f;
    }
}
