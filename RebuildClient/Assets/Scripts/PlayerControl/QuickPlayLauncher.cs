using UnityEngine;

#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.UI.TitleScreen;
#endif

namespace Assets.Scripts.PlayerControl
{
    public sealed class QuickPlayLauncher : MonoBehaviour
    {
#if UNITY_EDITOR
        [Serializable]
        private sealed class Config
        {
            public string serverUrl, username, password, characterName;
        }

        private static readonly string CONFIG_FILE = "quickplay.local.json";

        private static QuickPlayLauncher active;
        private static NetworkManager readyClient;
        private Config config;
        private NetworkManager network;
        private bool started, entering;
        private float deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { active = null; readyClient = null; }

        private void OnEnable()
        {
            if (started) return;
            if (active != null && active != this) { enabled = false; return; }
            try
            {
                config = ReadConfig();
                if (config == null || string.IsNullOrWhiteSpace(config.username)
                    || string.IsNullOrWhiteSpace(config.password) || string.IsNullOrWhiteSpace(config.characterName)
                    || !Uri.TryCreate(config.serverUrl, UriKind.Absolute, out var uri)
                    || (uri.Scheme != "ws" && uri.Scheme != "wss"))
                    throw new InvalidOperationException();
            }
            catch
            {
                Debug.LogError("QuickPlay: create a valid quickplay.local.json in the RebuildClient project root using the example file.");
                enabled = false;
                return;
            }

            active = this;
            if (readyClient != null) OnClientReady(readyClient);
        }

        private Config ReadConfig()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../" + CONFIG_FILE));
            return JsonUtility.FromJson<Config>(File.ReadAllText(path));
        }

        public static void OnClientReady(NetworkManager manager)
        {
            readyClient = manager;
            if (active == null || !active.isActiveAndEnabled || active.started) return;
            active.started = true;
            active.network = manager;
            var c = active.config;
            manager.TitleScreen.StartLoginTimer();
            manager.StartConnectWithRegularLogin(c.serverUrl, c.username, c.password, false, false);
            c.password = null;
        }

        public static bool OnCharacters(NetworkManager manager, List<ClientCharacterSummary> characters)
        {
            if (active == null || !active.started || active.network != manager) return false;
            if (active.entering) return true;
            var selected = characters.Find(c => c != null && c.Name == active.config.characterName);
            if (selected == null)
            {
                manager.Disconnect();
                manager.TitleScreen.LogInError("QuickPlay: configured character was not found.");
                return true;
            }
            // Stop the login timer using the existing selection transition, then hide its window.
            manager.TitleScreen.OpenCharacterSelect(characters);
            manager.TitleScreen.CharacterSelectWindow.gameObject.SetActive(false);
            manager.TitleScreen.TitleState = TitleScreen.TitleScreenState.Waiting;
            active.entering = true;
            active.deadline = Time.realtimeSinceStartup + 60f;
            manager.SendEnterServerMessage(selected.Name);
            return true;
        }

        private void Update()
        {
            if (!entering || network == null) return;
            if (PlayerState.Instance.IsValid)
            {
                Release();
                return;
            }
            if (Time.realtimeSinceStartup < deadline) return;
            entering = false;
            network.Disconnect();
            network.TitleScreen.LogInError("QuickPlay: character entry timed out.");
            Release();
        }

        public static void OnLoginFailed()
        {
            if (active != null) active.Release();
        }

        private void OnDisable() => Release();

        private void Release()
        {
            if (active == this) active = null;
            config = null;
            entering = false;
        }
#endif
    }
}
