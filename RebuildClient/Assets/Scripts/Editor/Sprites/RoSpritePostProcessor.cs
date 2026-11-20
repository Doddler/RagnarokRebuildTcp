using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor.Sprites
{
    internal sealed class RoSpritePostProcessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            // Avoid rebuilding every pair during the import batch after a domain reload.
            if (didDomainReload) return;

            var pairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in importedAssets)
            {
                var extension = Path.GetExtension(path);
                if (!extension.Equals(".act", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".spr", StringComparison.OrdinalIgnoreCase))
                    continue;

                var actPath = RoSpriteImporter.FindSibling(path, ".act");
                var sprPath = RoSpriteImporter.FindSibling(path, ".spr");
                if (actPath != null && sprPath != null)
                    pairs[actPath] = sprPath;
            }

            foreach (var pair in pairs)
            {
                try
                {
                    RoSpriteImporter.ImportPair(pair.Value, pair.Key);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
