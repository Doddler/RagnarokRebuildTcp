using System.Collections.Generic;
using System.IO;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Examples;
using UnityEditor.Recorder.Input;
#endif

namespace Utility
{
    [RequireComponent(typeof(Camera))]
    public class VideoRecorder : MonoBehaviour
    {
        public string FFMpegPath;
        public string OutputPath;
        public bool CenterPlayerOnMap = true;
        public bool IsActive = false;
        public bool IsRecording = false;
        public Animator VirtualCamera;
        public string StateName;


        private Camera camera;

#if UNITY_EDITOR
        private RecorderController recorderController;
#endif

        private Dictionary<string, int> clipNumbers = new();

        void Awake()
        {
            camera = GetComponent<Camera>();
            if (VirtualCamera)
            {
                var animator = gameObject.GetComponent<Animator>();
                if (animator != null)
                    Destroy(animator); //fuck the unity animator, we only want to play 1 clip
                VirtualCamera.gameObject.SetActive(false);
            }

            LoadClipNumbers();
        }

        void LoadClipNumbers()
        {
            var path = Path.Combine(OutputPath, "ClipCounts.txt");
            if (!File.Exists(path))
                return;
            foreach (var line in File.ReadAllLines(path))
            {
                var s = line.Split("\t");
                if (int.TryParse(s[1], out var num))
                    clipNumbers.TryAdd(s[0], num);
            }
        }

        void SaveClipNumbers()
        {
            var path = Path.Combine(OutputPath, "ClipCounts.txt");
            var linesOut = new List<string>();
            foreach (var pair in clipNumbers)
            {
                linesOut.Add($"{pair.Key}\t{pair.Value}");
            }

            File.WriteAllLines(path, linesOut);
        }

        public void StopRecording()
        {
#if UNITY_EDITOR
            if (!IsActive)
                return;
            if (IsRecording)
                recorderController.StopRecording();

            UiManager.Instance.SetEnabled(true);
            AudioManager.Instance.ToggleMute();
            gameObject.SetActive(false);
            VirtualCamera.gameObject.SetActive(false);
#endif
        }

        public void StartRecording()
        {
#if UNITY_EDITOR
            if (!VirtualCamera)
                return;

            IsActive = true;
            gameObject.SetActive(true);
            VirtualCamera.gameObject.SetActive(true);

            Debug.Log("Playing clip on animator: " + StateName);

            VirtualCamera.Play(StateName);

            if (CenterPlayerOnMap)
                NetworkManager.Instance.SendMoveRequest(NetworkManager.Instance.CurrentMap, 200, 200, true);
            gameObject.SetActive(true);
            Debug.Log($"CINEMATIC: " + gameObject);
            UiManager.Instance.SetEnabled(false);
            AudioManager.Instance.MuteBGM();
            if (Input.GetKeyDown(KeyCode.F6))
            {
                var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
                recorderController = new RecorderController(controllerSettings);

                var videoRecorder = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                videoRecorder.name = "My Video Recorder";
                videoRecorder.Enabled = true;
                videoRecorder.VideoBitRateMode = VideoBitrateMode.Low;

                videoRecorder.ImageInputSettings = new GameViewInputSettings
                {
                    OutputWidth = 2560,
                    OutputHeight = 1440
                };

                videoRecorder.EncoderSettings = new FFmpegEncoderSettings()
                {
                    Format = FFmpegEncoderSettings.OutputFormat.H264Nvidia,
                    FFMpegPath = FFMpegPath
                };

                videoRecorder.AudioInputSettings.PreserveAudio = true;

                var mapName = NetworkManager.Instance.CurrentMap;
                var startNum = 0;
                if (clipNumbers.TryGetValue(mapName, out var curCount))
                    startNum = curCount + 1;

                for (var i = startNum; i < 9999; i++)
                {
                    var fName = $"{mapName}_{i:0000}";
                    var targetPath = Path.Combine(OutputPath, fName);
                    if (!File.Exists(targetPath + ".mp4"))
                    {
                        videoRecorder.OutputFile = targetPath;
                        clipNumbers[mapName] = i;
                        SaveClipNumbers();
                        break;
                    }
                }

                Debug.Log($"Outputting video to: " + videoRecorder.OutputFile);

                controllerSettings.AddRecorderSettings(videoRecorder);
                controllerSettings.SetRecordModeToFrameInterval(0, 60 * 60 * 10); // max 10 mins, should be enough...
                controllerSettings.FrameRate = 60;
                //controllerSettings.SetRecordModeToManual();


                RecorderOptions.VerboseMode = false;
                recorderController.PrepareRecording();
                recorderController.StartRecording();
                IsRecording = true;
            }
#endif
        }
    }
}