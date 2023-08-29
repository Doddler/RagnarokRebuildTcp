using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEngine;

namespace UnityEditor.Recorder.Examples
{
    /// <summary>
    /// The settings of the FFmpeg Encoder.
    /// </summary>
    /// <remarks>
    /// This class is sealed because users shouldn't inherit from it. Instead, create a new encoder along with its settings class.
    /// </remarks>
    [DisplayName("FFmpeg Encoder")]
    [Serializable]
    [EncoderSettings(typeof(FFmpegEncoder))]
    public sealed class FFmpegEncoderSettings : IEncoderSettings, IEquatable<FFmpegEncoderSettings>
    {
        [SerializeField] string ffmpegPath;
        public string FFMpegPath
        {
            get => ffmpegPath;
            set => ffmpegPath = value;
        }

        /// <summary>
        /// The output format of the FFmpeg encoder.
        /// </summary>
        public enum OutputFormat
        {
            [InspectorName("H.264 Default")] H264Default,
            [InspectorName("H.264 NVIDIA")] H264Nvidia,
            [InspectorName("H.264 Lossless 420")] H264Lossless420,
            [InspectorName("H.264 Lossless 444")] H264Lossless444,
            [InspectorName("H.265 HEVC Default")] HevcDefault,
            [InspectorName("H.265 HEVC NVIDIA")] HevcNvidia,
            [InspectorName("H.265 HEVC NVIDIA Fast")] HevcNvidiaFast,
            [InspectorName("Apple ProRes 4444 XQ (ap4x)")] ProRes4444XQ,
            [InspectorName("Apple ProRes 4444 (ap4h)")] ProRes4444,
            [InspectorName("Apple ProRes 422 HQ (apch)")] ProRes422HQ,
            [InspectorName("Apple ProRes 422 (apcn)")] ProRes422,
            [InspectorName("Apple ProRes 422 LT (apcs)")] ProRes422LT,
            [InspectorName("Apple ProRes 422 Proxy (apco)")] ProRes422Proxy,
            [InspectorName("VP8 (WebM)")] VP8Default,
            [InspectorName("VP9 (WebM)")] VP9Default,
        }

        /// <summary>
        /// The format of the encoder.
        /// </summary>
        public OutputFormat Format
        {
            get => outputFormat;
            set => outputFormat = value;
        }
        [SerializeField] OutputFormat outputFormat;

        /// <inheritdoc/>
        string IEncoderSettings.Extension
        {
            get
            {
                switch (Format)
                {
                    case OutputFormat.H264Default:
                    case OutputFormat.H264Nvidia:
                    case OutputFormat.H264Lossless420:
                    case OutputFormat.H264Lossless444:
                    case OutputFormat.HevcDefault:
                    case OutputFormat.HevcNvidia:       return "mp4";
                    case OutputFormat.ProRes4444XQ:
                    case OutputFormat.ProRes4444:
                    case OutputFormat.ProRes422HQ:
                    case OutputFormat.ProRes422:
                    case OutputFormat.ProRes422LT:
                    case OutputFormat.ProRes422Proxy:   return "mov";
                    case OutputFormat.VP8Default:
                    case OutputFormat.VP9Default:      return "webm";
                }

                return "mp4";
            }
        }

        public string GetOptions()
        {
            return Format switch
            {
                OutputFormat.H264Default => "-c:v libx264 -preset veryslow -crf 17 -tune film -pix_fmt yuv420p",
                OutputFormat.H264Nvidia =>
                    "-c:v h264_nvenc -pix_fmt yuv420p -rc constqp -qmin 17 -qmax 51 -qp 24 -preset p1 -b:v 10M -tune hq -rc-lookahead 4 -profile:v high",
                OutputFormat.H264Lossless420 => "-c:v libx264 -pix_fmt yuv420p -crf 0",
                OutputFormat.H264Lossless444 => "-c:v libx264 -pix_fmt yuv444p -crf 0",
                OutputFormat.HevcDefault => "-c:v libx265 -pix_fmt yuv420p",
                OutputFormat.HevcNvidia => "-c:v hevc_nvenc -pix_fmt yuv420p -preset p7 -tune hq",
                OutputFormat.HevcNvidiaFast => "-c:v hevc_nvenc -pix_fmt yuv420p -preset p1 -b:v 10M",
                OutputFormat.ProRes4444XQ => "-c:v prores_ks -profile:v 5 -vendor apl0 -pix_fmt yuva444p10le",
                OutputFormat.ProRes4444 => "-c:v prores_ks -profile:v 4 -vendor apl0 -pix_fmt yuva444p10le",
                OutputFormat.ProRes422HQ => "-c:v prores_ks -profile:v 3 -vendor apl0 -pix_fmt yuv422p10le",
                OutputFormat.ProRes422 => "-c:v prores_ks -profile:v 2 -vendor apl0 -pix_fmt yuv422p10le",
                OutputFormat.ProRes422LT => "-c:v prores_ks -profile:v 1 -vendor apl0 -pix_fmt yuv422p10le",
                OutputFormat.ProRes422Proxy => "-c:v prores_ks -profile:v 0 -vendor apl0 -pix_fmt yuv422p10le",
                OutputFormat.VP8Default => "-c:v libvpx -pix_fmt yuv420p -crf 6",
                OutputFormat.VP9Default => "-c:v libvpx-vp9 -pix_fmt yuv420p -crf 25 -b:v 0",
                _ => null
            };
        }

        public string GetPixelFormat(bool inputContainsAlpha)
        {
            var codecFormatSupportsTransparency = CodecFormatSupportsAlphaChannel(Format);
            var willIncludeAlpha = inputContainsAlpha && codecFormatSupportsTransparency;
            return willIncludeAlpha ? "argb" : "rgb24";
        }

        /// <inheritdoc/>
        bool IEncoderSettings.CanCaptureAlpha => CodecFormatSupportsAlphaChannel(Format);

        /// <inheritdoc/>
        bool IEncoderSettings.CanCaptureAudio => true;

        /// <summary>
        /// Indicates whether the requested ProRes codec format can encode an alpha channel or not.
        /// </summary>
        /// <param name="format">The ProRes codec format to check.</param>
        /// <returns>True if the specified codec can encode an alpha channel, False otherwise.</returns>
        internal bool CodecFormatSupportsAlphaChannel(OutputFormat format)
        {
            return format == OutputFormat.ProRes4444XQ || format == OutputFormat.ProRes4444;
        }

        /// <inheritdoc/>
        TextureFormat IEncoderSettings.GetTextureFormat(bool inputContainsAlpha)
        {
            var codecFormatSupportsTransparency = CodecFormatSupportsAlphaChannel(Format);
            var willIncludeAlpha = inputContainsAlpha && codecFormatSupportsTransparency;
            return willIncludeAlpha ? TextureFormat.ARGB32 : TextureFormat.RGB24;
        }

        /// <inheritdoc/>
        void IEncoderSettings.ValidateRecording(RecordingContext ctx, List<string> errors, List<string> warnings)
        {
            if (!File.Exists(FFMpegPath))
                errors.Add($"Cannot find the FFMPEG encoder at path: {FFMpegPath}");
            // Is the codec format supported?
            if (!IsOutputFormatSupported(Format))
                errors.Add($"Format '{Format}' is not supported on this platform.");

            if (ctx.doCaptureAlpha && !CodecFormatSupportsAlphaChannel(Format))
                errors.Add($"Format '{Format}' does not support transparency.");

            if (ctx.frameRateMode == FrameRatePlayback.Variable)
                errors.Add($"This encoder does not support Variable frame rate playback. Please consider using Constant frame rate instead.");
        }

        /// <inheritdoc/>
        public bool SupportsCurrentPlatform()
        {
            return true;
        }

        /// <summary>
        /// Indicates whether the specified ProRes codec format is supported on the current operating system or not.
        /// </summary>
        /// <param name="toCheck">The ProRes codec format to check.</param>
        /// <returns>True if the specified output format is supported on the current operating system, False otherwise</returns>
        /// <remarks>
        /// On Windows, all formats are available.
        /// </remarks>
        internal static bool IsOutputFormatSupported(OutputFormat toCheck)
        {
            return true;
        }

        /// <inheritdoc/>
        bool IEquatable<FFmpegEncoderSettings>.Equals(FFmpegEncoderSettings other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return outputFormat == other.outputFormat;
        }

        /// <summary>
        /// Compares the current object with another one.
        /// </summary>
        /// <param name="obj">The object to compare with the current one.</param>
        /// <returns>True if the two objects are equal, false otherwise.</returns>
        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is FFmpegEncoderSettings other && ((IEquatable<FFmpegEncoderSettings>) this).Equals(other);
        }

        /// <summary>
        /// Returns a hash code of all serialized fields.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine((int)outputFormat);
        }
    }
}
