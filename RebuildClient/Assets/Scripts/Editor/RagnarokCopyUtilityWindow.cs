using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Assets.Editor
{
    [Serializable]
    public sealed class RagnarokCopyProfile
    {
        public string name = "";
        public string description = "";
        public bool all;
        public RagnarokCopyProfileResources resources = new();

        [NonSerialized] public string AssetPath = "";

        public void EnsureDefaults()
        {
            resources ??= new RagnarokCopyProfileResources();
            resources.EnsureDefaults();
        }
    }

    [Serializable]
    public sealed class RagnarokCopyProfileResources
    {
        public List<string> maps = new();
        public List<int> items = new();
        public List<string> jobs = new();
        public List<string> monsters = new();
        public List<int> npcs = new();
        public List<string> effects = new();
        public List<int> skills = new();

        public void EnsureDefaults()
        {
            maps ??= new List<string>();
            items ??= new List<int>();
            jobs ??= new List<string>();
            monsters ??= new List<string>();
            npcs ??= new List<int>();
            effects ??= new List<string>();
            skills ??= new List<int>();
        }
    }

    public sealed class RagnarokCopyUtilityWindow : EditorWindow
    {
        private const string PROFILE_DIRECTORY = "Assets/StreamingAssets/ProjectConfig";

        private readonly List<RagnarokCopyProfile> profiles = new();
        private readonly List<string> validationErrors = new();
        private string[] profileNames = Array.Empty<string>();
        private Vector2 scrollPosition;
        private int selectedProfile;

        [MenuItem("Ragnarok/Copy data from Profile", priority = 2)]
        public static void OpenMinimum()
        {
            Open("minimum.json");
        }

        private static void Open(string preferredFile)
        {
            var window = GetWindow<RagnarokCopyUtilityWindow>("Ragnarok Copy Utility");
            window.LoadProfiles(preferredFile);
            window.Show();
        }

        private void LoadProfiles(string preferredFile)
        {
            profiles.Clear();

            if (Directory.Exists(PROFILE_DIRECTORY))
            {
                foreach (var path in Directory.GetFiles(PROFILE_DIRECTORY, "*.json", SearchOption.TopDirectoryOnly)
                             .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var profile = JsonUtility.FromJson<RagnarokCopyProfile>(File.ReadAllText(path));
                        if (profile == null)
                            continue;
                        profile.EnsureDefaults();
                        profile.AssetPath = path.Replace("\\", "/");
                        profiles.Add(profile);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError($"[Ragnarok Copy Utility] Could not load resource profile '{path}': {exception.Message}");
                    }
                }
            }

            profileNames = profiles.Select(profile => string.IsNullOrWhiteSpace(profile.name)
                ? Path.GetFileNameWithoutExtension(profile.AssetPath)
                : profile.name).ToArray();

            if (!string.IsNullOrWhiteSpace(preferredFile))
            {
                var index = profiles.FindIndex(profile => string.Equals(
                    Path.GetFileName(profile.AssetPath), preferredFile, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                    selectedProfile = index;
            }

            selectedProfile = Mathf.Clamp(selectedProfile, 0, Math.Max(0, profiles.Count - 1));
            ValidateSelectedProfile();
            Repaint();
        }

        private void ValidateSelectedProfile()
        {
            validationErrors.Clear();
            if (profiles.Count == 0)
                return;

            var profile = profiles[selectedProfile];
            validationErrors.AddRange(RagnarokCopyFromRealClient.ValidateProfile(profile));
            if (profiles.Count(candidate => string.Equals(candidate.name, profile.name, StringComparison.OrdinalIgnoreCase)) > 1)
                validationErrors.Add($"Profile name '{profile.name}' is duplicated.");
        }

        private void OpenFilePicker()
        {
            var oldPath = EditorPrefs.GetString("RagnarokDataPath", null);
            var startPath = oldPath;

            if (string.IsNullOrWhiteSpace(startPath) || !Directory.Exists(startPath))
                startPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

            var path = EditorUtility.OpenFolderPanel("Locate Ragnarok Data Folder", startPath, "");

            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                EditorPrefs.SetString("RagnarokDataPath", path);
                Debug.Log("[Ragnarok Copy Utility] Ragnarok data directory set to: " + path);
            }
            else
                Debug.LogWarning("[Ragnarok Copy Utility] Failed to set data directory. Using old directory: " + EditorPrefs.GetString("RagnarokDataPath", null));
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Profiles copy only their selected resources and required dependencies. Existing assets are never removed.",
                MessageType.Info);
            EditorGUILayout.Space();

            if (profiles.Count == 0)
            {
                EditorGUILayout.HelpBox($"No JSON profiles were found in {PROFILE_DIRECTORY}.", MessageType.Error);
                if (GUILayout.Button("Refresh"))
                    LoadProfiles(null);
                return;
            }

            if (GUILayout.Button("Locate Data folder"))
            {
                OpenFilePicker();
            }

            var dataFolderPath = EditorPrefs.GetString("RagnarokDataPath", null);
            EditorGUILayout.LabelField("Data folder path", dataFolderPath);

            var newSelection = EditorGUILayout.Popup("Profile", selectedProfile, profileNames);
            if (newSelection != selectedProfile)
            {
                selectedProfile = newSelection;
                ValidateSelectedProfile();
            }
            var profile = profiles[selectedProfile];

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.LabelField("File", profile.AssetPath);
            EditorGUILayout.LabelField("Description", profile.description, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            if (profile.all)
            {
                EditorGUILayout.HelpBox("This profile performs an exhaustive full client-data copy.", MessageType.Warning);
            }
            else
            {
                var resources = profile.resources;
                EditorGUILayout.LabelField("Maps", resources.maps.Count.ToString());
                EditorGUILayout.LabelField("Items", resources.items.Count.ToString());
                EditorGUILayout.LabelField("Jobs", resources.jobs.Count.ToString());
                EditorGUILayout.LabelField("Monsters", resources.monsters.Count.ToString());
                EditorGUILayout.LabelField("NPCs", resources.npcs.Count.ToString());
                EditorGUILayout.LabelField("Effects", resources.effects.Count.ToString());
                EditorGUILayout.LabelField("Skills", resources.skills.Count.ToString());
            }

            foreach (var error in validationErrors)
                EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Profiles", GUILayout.Height(28)))
                LoadProfiles(Path.GetFileName(profile.AssetPath));

            GUI.enabled = validationErrors.Count == 0;
            if (GUILayout.Button("Import Profile", GUILayout.Height(28)))
                RagnarokCopyFromRealClient.CopyFromProfile(profile, dataFolderPath);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }
    }
}
