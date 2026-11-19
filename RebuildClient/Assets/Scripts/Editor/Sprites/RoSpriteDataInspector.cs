using System.Collections.Generic;
using Assets.Scripts;
using Assets.Scripts.Sprites;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor.Sprites
{
    [CustomEditor(typeof(RoSpriteData))]
    public sealed class RoSpriteDataInspector : UnityEditor.Editor
    {
        private string cachedSpriteName;
        private List<RoSpriteAsset> cachedAssets;

        private void OnEnable()
        {
            EditorApplication.projectChanged += ClearCache;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= ClearCache;
        }

        public override void OnInspectorGUI()
        {
            var spriteData = (RoSpriteData)target;
            DrawSourceNavigation(GetMatchingAssets(spriteData.Name));
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        private void ClearCache()
        {
            cachedSpriteName = null;
            cachedAssets = null;
        }

        private List<RoSpriteAsset> GetMatchingAssets(string spriteName)
        {
            if (cachedAssets != null && cachedSpriteName == spriteName)
                return cachedAssets;

            cachedSpriteName = spriteName;
            cachedAssets = FindMatchingSprAssets(spriteName);
            if (cachedAssets.Count == 0 && TryGetBaseSpriteName(spriteName, out var baseName))
                cachedAssets = FindMatchingSprAssets(baseName);
            return cachedAssets;
        }

        private static void DrawSourceNavigation(List<RoSpriteAsset> candidates)
        {
            if (candidates.Count == 0)
            {
                EditorGUILayout.HelpBox("No matching RoSpriteAsset was found in the project.", MessageType.Info);
                using (new EditorGUI.DisabledScope(true))
                    GUILayout.Button("Open SPR Asset");
                return;
            }

            var label = candidates.Count == 1 ? "Open SPR Asset" : "Choose SPR Asset…";
            if (!GUILayout.Button(label)) return;

            if (candidates.Count == 1)
            {
                SelectAsset(candidates[0]);
                return;
            }

            var menu = new GenericMenu();
            foreach (var candidate in candidates)
            {
                var asset = candidate;
                menu.AddItem(new GUIContent(AssetDatabase.GetAssetPath(asset)), false, () => SelectAsset(asset));
            }
            menu.ShowAsContext();
        }

        private static List<RoSpriteAsset> FindMatchingSprAssets(string spriteName)
        {
            var results = new List<RoSpriteAsset>();
            if (string.IsNullOrEmpty(spriteName)) return results;

            foreach (var guid in AssetDatabase.FindAssets("t:RoSpriteAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<RoSpriteAsset>(path);
                if (asset != null && asset.sprFileName == spriteName)
                    results.Add(asset);
            }
            return results;
        }

        private static bool TryGetBaseSpriteName(string name, out string baseName)
        {
            baseName = null;
            if (string.IsNullOrEmpty(name)) return false;

            var suffixSeparator = name.LastIndexOf('_');
            if (suffixSeparator <= 0 || suffixSeparator == name.Length - 1) return false;
            var suffix = name.Substring(suffixSeparator + 1);
            if (!int.TryParse(suffix, out var paletteIndex) || paletteIndex < 0 || paletteIndex > 9)
                return false;

            baseName = name.Substring(0, suffixSeparator);
            return true;
        }

        private static void SelectAsset(RoSpriteAsset asset)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
