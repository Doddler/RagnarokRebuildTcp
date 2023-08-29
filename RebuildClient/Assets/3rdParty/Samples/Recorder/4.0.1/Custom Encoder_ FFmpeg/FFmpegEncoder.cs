//#define FFMPEGMEDIAENCODER_TRACE_ENABLED
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor.Media;
using UnityEditor.Recorder.Encoder;
using UnityEngine;

namespace UnityEditor.Recorder.Examples
{
    class FFmpegEncoder : IEncoder
    {
        bool disposed;
        FFmpegPipe _ffmpegVideoPipe;
        FFmpegPipe _ffmpegAudioPipe;
        string _rawVideoFilename;
        string _rawAudioFilename;
        bool _hasAudio;

        public void OpenStream(IEncoderSettings settings, RecordingContext ctx)
        {
            var ffmpegSettings = settings as FFmpegEncoderSettings;
            _hasAudio = ctx.doCaptureAudio;

            try
            {
                var options = ffmpegSettings.GetOptions();
                var pixel = ffmpegSettings.GetPixelFormat(ctx.doCaptureAlpha);

                var arguments = "  -y -f rawvideo -vcodec rawvideo"
                    + " -pixel_format " + pixel
                    + " -colorspace bt709"
                    + " -video_size " + ctx.width + "x" + ctx.height
                    + " -framerate " + (float)DoubleFromRational(ctx.fps)
                    + " -loglevel error -i - " + options
                    + " \"" + ctx.path + "\"";

                _rawVideoFilename = ctx.path;
                _ffmpegVideoPipe = new FFmpegPipe(arguments, ffmpegSettings.FFMpegPath, "VideoPipe");

                Log($"Video: {arguments}");

                if (_hasAudio)
                {
                    var fileNameAudio = "";
                    _rawAudioFilename = Path.ChangeExtension(_rawVideoFilename, ".mkv");
                    fileNameAudio = "\"" + _rawAudioFilename + "\"";

                    // If the file has audio, it will always be stereo
                    var audioSampleRate = new MediaRational(AudioSettings.outputSampleRate);

                    var audioArgs = "  -loglevel error -y -ar " + audioSampleRate.numerator
                        + " -ac 2"
                        + " -f f32le -i - -c:a aac " + fileNameAudio;
                    _ffmpegAudioPipe = new FFmpegPipe(audioArgs, ffmpegSettings.FFMpegPath, "AudioPipe");

                    Log($"Audio: {audioArgs}");
                }
            }
            catch (Exception e)
            {
                if (_ffmpegVideoPipe != null)
                {
                    _ffmpegVideoPipe.Dispose();
                    _ffmpegVideoPipe.Dispose();
                }

                if (_ffmpegAudioPipe != null)
                {
                    _ffmpegAudioPipe.Dispose();
                    _ffmpegAudioPipe = null;
                }

                Debug.LogWarning(e);
                throw;
            }

            disposed = false;
        }

        public void CloseStream()
        {
            if (_ffmpegVideoPipe != null)
            {
                var error = _ffmpegVideoPipe.CloseAndGetOutput();

                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning(
                        "FFmpeg returned with warning/error messages. " +
                        "See the following lines for details:\n" + error
                    );
                }

                _ffmpegVideoPipe.Dispose();
                _ffmpegVideoPipe = null;
            }

            if (_ffmpegAudioPipe != null)
            {
                var error = _ffmpegAudioPipe.CloseAndGetOutput();

                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning(
                        "FFmpeg returned with warning/error messages. " +
                        "See the following lines for details:\n" + error
                    );
                }

                _ffmpegAudioPipe.Dispose();
                _ffmpegAudioPipe = null;
            }

            if (_hasAudio)
            {
                // Begin remux
                PostProcessAudioRemuxing(_rawVideoFilename, _rawAudioFilename);
            }

            disposed = true;
        }

        public void AddVideoFrame(NativeArray<byte> bytes, MediaTime time)
        {
            if (disposed)
            {
                Debug.LogError("The encoder has already been disposed, ignoring this data.");
                return;
            }
            _ffmpegVideoPipe.PushFrameData(bytes);
            _ffmpegVideoPipe.SyncFrameData();
        }

        public void AddAudioFrame(NativeArray<float> interleavedSamples)
        {
            if (disposed)
            {
                Debug.LogError("The encoder has already been disposed, ignoring this data.");
                return;
            }

            _ffmpegAudioPipe.PushFrameData(interleavedSamples);
            _ffmpegAudioPipe.SyncFrameData();
        }

        static void PostProcessAudioRemuxing(string videoPath, string audioFileName)
        {
            if (string.IsNullOrEmpty(videoPath))
            {
                throw new ArgumentException("Path is empty", "videoPath");
            }

            if (string.IsNullOrEmpty(audioFileName))
            {
                throw new ArgumentException("Path is empty", "audioFileName");
            }

            var videoFileName = videoPath;
            var backupFileName = Path.ChangeExtension(videoFileName, ".tmp");

            Log($"Remux: video={videoFileName} audio={audioFileName} temp={backupFileName}");

            if (IsFileLocked(videoFileName))
            {
                Debug.LogError(videoFileName + " is locked can't mux audio");
                return;
            }

            File.Move(videoFileName, backupFileName);

            var process = FFmpegPipe.LaunchFFMPEG(
                $"-loglevel error -i \"{backupFileName}\" -i \"{audioFileName}\"" +
                $" -map 0:v -map 1:a -c:v copy -c:a copy \"{videoFileName}\"");

            var processLog = new List<string>();

            process.ErrorDataReceived += (sender, args) => processLog.Add(args.Data);
            process.Exited += (sender, args) =>
            {
                foreach (var line in processLog.Where(line => !string.IsNullOrEmpty(line)))
                {
                    Log($"Remux: {line}");
                }

                Log("Remux: Finished");
                processLog.Clear();
            };

            if (!process.Start())
            {
                throw new Exception($"Failed: {process.StartInfo.FileName} {process.StartInfo.Arguments}");
            }

            process.BeginErrorReadLine();

            // Close FFmpeg subprocess.
            process.StandardInput.Close();
            process.WaitForExit(10000);

            process.Close();
            process.Dispose();

            Cleanup(backupFileName);
            Cleanup(audioFileName);
        }

        static void Cleanup(string backupFileName)
        {
            try
            {
                File.Delete(backupFileName);
            }
            catch (IOException ex)
            {
                Debug.LogError(ex.Data);
            }
        }

        static void Log(string log)
        {
#if FFMPEGMEDIAENCODER_TRACE_ENABLED
            Debug.Log("[FFmpeg]: " + log);
#endif
        }

        static bool IsFileLocked(string path)
        {
            FileStream stream = null;

            try
            {
                stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                return true;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                }
            }

            return false;
        }

        static double DoubleFromRational(MediaRational rational)
        {
            if (rational.denominator == 0)
            {
                return 0;
            }

            return rational.numerator / (float)rational.denominator;
        }
    }
}
