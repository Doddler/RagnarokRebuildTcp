using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.MapEditor.Editor
{
    class RagnarokDirectory : EditorWindow
    {
        public static string GetRagnarokDataDirectory
        {
            get
            {
                var path = EditorPrefs.GetString("RagnarokDataPath", null);
                if(path == null)
                    throw new Exception("You must set a ragnarok data directory first!");
                return path;
            }
        }

        //alternative that does not throw an exception
        public static string GetRagnarokDataDirectorySafe
        {
            get
            {
                var path = EditorPrefs.GetString("RagnarokDataPath", null);
                return path;
            }
        }


        [MenuItem("Ragnarok/Set Ragnarok Data Directory", priority = 0)]
        public static void SetDataDirectory()
        {
            var defaultName = "Data";
            var oldPath = EditorPrefs.GetString("RagnarokDataPath", null);
            if (!string.IsNullOrWhiteSpace(oldPath) && Directory.Exists(oldPath))
            {
                var di = new DirectoryInfo(oldPath);
                oldPath = di.Parent.FullName;
                defaultName = di.Name;
            }
            var path = EditorUtility.SaveFolderPanel("Locate Ragnarok Data Folder", oldPath, defaultName);
            if (Directory.Exists(path))
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
