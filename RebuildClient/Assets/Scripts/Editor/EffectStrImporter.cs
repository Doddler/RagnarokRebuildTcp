using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RebuildSharedData.ClientTypes;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor
{

    public static class EffectStrImporter
    {
        [MenuItem("Ragnarok/Load Effects")]
        public static void Import()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/effects.json");
            var effects = JsonUtility.FromJson<EffectTypeList>(asset.text);

            foreach (var e in effects.Effects)
            {
                if (!e.ImportEffect)
                    continue;

                var loader = new RagnarokEffectLoader();
                loader.Load(@$"G:\Projects2\Ragnarok\Resources\data\texture\effect\{e.StrFile}.str", e.Name);
                loader.MakeAtlas(@"Assets/Effects/Atlas/");
            }

            
        }
    }
}
