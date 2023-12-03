using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts.MapEditor.Editor;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using RebuildSharedData.ClientTypes;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Assets.Scripts.Editor
{
    public static class EffectStrImporter
    {
        [MenuItem("Ragnarok/Load Effects")]
        public static void Import()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/effects.json");
            var effects = JsonUtility.FromJson<EffectTypeList>(asset.text);

            if (!Directory.Exists("Assets/Effects/Prefabs/"))
                Directory.CreateDirectory("Assets/Effects/Prefabs/");
            
            foreach (var e in effects.Effects)
            {
                if (!e.ImportEffect)
                    continue;

                var prefabPath = $"Assets/Effects/Prefabs/{e.Name}.prefab";

                if (File.Exists(prefabPath))
                    continue;

                if (!string.IsNullOrWhiteSpace(e.StrFile))
                {
                    var loader = new RagnarokEffectLoader();
                    var anim = loader.Load(@$"G:\Projects2\Ragnarok\Resources\data\texture\effect\{e.StrFile}.str", e.Name);
                    if (anim == null)
                        continue;

                    loader.MakeAtlas(@"Assets/Effects/Atlas/");

                    var obj = new GameObject(e.Name);
                    var renderer = obj.AddComponent<RoEffectRenderer>();
                    var sorter = obj.AddComponent<SortingGroup>();
                    //var billboard = obj.AddComponent<Billboard>();

                    if (!string.IsNullOrWhiteSpace(e.SoundFile))
                    {
                        var assetPath = $"Assets/Sounds/{e.SoundFile}.ogg";
                        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);

                        if (clip == null)
                        {
                            assetPath = $"Assets/Sounds/Effects/{e.SoundFile}.ogg";
                            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                        }

                        if (clip != null)
                        {
                            var audio = obj.AddComponent<EffectAudioSource>();
                            audio.Volume = 1f;
                            audio.Clip = clip;
                            renderer.AudioSource = audio;
                        }
                        else
                        {
                            Debug.LogWarning("Could not load sound file at : " + assetPath);
                        }
                    }

                    renderer.Anim = anim;

                    PrefabUtility.SaveAsPrefabAssetAndConnect(obj, prefabPath, InteractionMode.AutomatedAction);
                    Object.DestroyImmediate(obj);
                }

                if (!string.IsNullOrWhiteSpace(e.Sprite))
                {
                    var obj = new GameObject(e.Name);
                    var effect = obj.AddComponent<SpriteEffect>();

                    var spritePath = "Assets/Sprites/Effects/";
                    var sprite = AssetDatabase.LoadAssetAtPath<RoSpriteData>(spritePath + e.Sprite + ".spr");

                    effect.SpriteData = sprite;
                    effect.IsLoop = true;
                    
                    PrefabUtility.SaveAsPrefabAssetAndConnect(obj, prefabPath, InteractionMode.AutomatedAction);
                    Object.DestroyImmediate(obj);
                }
            }

            RagnarokMapImporterWindow.UpdateAddressables();
        }
    }
}
