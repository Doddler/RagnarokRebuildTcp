using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.MapEditor.Editor
{
    class RagnarokDirectory : EditorWindow
    {
        private static string NormalizeDataPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return null;
            }
        }

        public static string GetRagnarokDataDirectory
        {
            get
            {
                var path = NormalizeDataPath(EditorPrefs.GetString("RagnarokDataPath", null));
                if (path == null)
                    throw new Exception("You must set a ragnarok data directory first!");
                return path;
            }
        }

        //alternative that does not throw an exception
        public static string GetRagnarokDataDirectorySafe
        {
            get
            {
                return NormalizeDataPath(EditorPrefs.GetString("RagnarokDataPath", null));
            }
        }


        [MenuItem("Ragnarok/Set Ragnarok Data Directory", priority = 0)]
        public static void SetDataDirectory()
        {
            var oldPath = NormalizeDataPath(EditorPrefs.GetString("RagnarokDataPath", null));
            var startPath = oldPath;

            if (string.IsNullOrWhiteSpace(startPath) || !Directory.Exists(startPath))
                startPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

            var path = NormalizeDataPath(EditorUtility.OpenFolderPanel("Locate Ragnarok Data Folder", startPath, ""));

            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                EditorPrefs.SetString("RagnarokDataPath", path);
                Debug.Log("Ragnarok data directory set to: " + path);
            }
            else
                Debug.LogWarning("Failed to set data directory. Using old directory: " + EditorPrefs.GetString("RagnarokDataPath", null));
        }

        [MenuItem("Ragnarok/Open Ragnarok Data Directory", priority = 1)]
        public static void OpenDataDirectory()
        {
            var oldPath = EditorPrefs.GetString("RagnarokDataPath", null);
            EditorUtility.RevealInFinder(oldPath);
        }
    }
}
