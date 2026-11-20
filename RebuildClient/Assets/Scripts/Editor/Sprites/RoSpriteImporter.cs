using System;
using System.Collections.Generic;
using System.IO;
using Assets.Scripts.Sprites;
using UnityEditor;
using UnityEngine;
using Utility.Editor;

namespace Assets.Scripts.Editor.Sprites
{
    internal static class RoSpriteImporter
    {
        private const int PaletteSlotCount = 10;
        private const int DefaultDelayMilliseconds = 150;
        private const int MillisecondsPerDelayUnit = 24;
        private const int TrueColorLayerType = 1;
        private const int EmptyLayerIndex = -1;
        private const string AttackSoundName = "atk";

        internal static string FindSibling(string sourcePath, string extension)
        {
            if (string.IsNullOrEmpty(sourcePath)) return null;
            var expectedPath = Path.ChangeExtension(sourcePath, extension).Replace('\\', '/');
            if (File.Exists(expectedPath)) return expectedPath;

            var directory = Path.GetDirectoryName(expectedPath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return null;

            var fileName = Path.GetFileName(expectedPath);
            foreach (var file in Directory.EnumerateFiles(directory))
                if (string.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase))
                    return file.Replace('\\', '/');
            return null;
        }

        internal static void ImportPair(string sprPath, string actPath)
        {
            var directory = Path.GetDirectoryName(actPath).Replace('\\', '/');
            var baseName = Path.GetFileNameWithoutExtension(actPath);
            var relative = directory.Substring(directory.IndexOfOccurence("/", 2) + 1);
            var targetFolder = Path.Combine("Assets/Sprites/Imported", relative);

            // Complete source validation before touching any generated asset, including palette variants.
            var (spr, indexed) = ReadSource(sprPath, () =>
            {
                var source = new RoSpr(sprPath);
                var images = source.BitmapImages;
                RagnarokSpriteLoader.ValidateImages(images, source.TrueColorImages);
                return (source, images);
            });
            var (act, actions) = ReadSource(actPath, () =>
            {
                var source = RoAct.Load(actPath);
                return (source, CreateRuntimeActions(source, indexed.Length, spr.TrueColorImages.Length));
            });
            var palettes = ReadPalettes(directory, baseName, spr.PaletteColors);

            var sourceSprGuid = AssetDatabase.AssetPathToGUID(sprPath);
            Directory.CreateDirectory(targetFolder);
            SaveVariant(targetFolder, baseName, baseName, indexed, spr.TrueColorImages,
                spr.PaletteColors, actions, act.Sounds, sourceSprGuid);
            for (var i = 0; i < palettes.Count; i++)
            {
                SaveVariant(targetFolder, baseName, $"{baseName}_{i}", indexed, spr.TrueColorImages,
                    palettes[i], actions, act.Sounds, sourceSprGuid);
            }
            AssetDatabase.SaveAssets();
        }

        private static List<Color32[]> ReadPalettes(string directory, string baseName, Color32[] embeddedPalette)
        {
            var palettes = new List<Color32[]>();
            var paletteFolder = Path.Combine(directory, "Palette");
            for (var i = 0; i < PaletteSlotCount; i++)
            {
                AddPalette($"{baseName}_{i}_1.pal");
                AddPalette($"{baseName}_{i}.pal");
            }
            return palettes;

            void AddPalette(string fileName)
            {
                var path = Path.Combine(paletteFolder, fileName);
                if (File.Exists(path))
                    palettes.Add(ReadSource(path, () => RoPalette.ApplyOverride(embeddedPalette, File.ReadAllBytes(path))));
            }
        }

        private static T ReadSource<T>(string path, Func<T> read)
        {
            try
            {
                return read();
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException ||
                exception is UnauthorizedAccessException || exception is FormatException ||
                exception is NotSupportedException || exception is OverflowException || exception is ArgumentException)
            {
                throw new InvalidDataException($"Cannot import sprite source '{path}': {exception.Message}", exception);
            }
        }

        private static void SaveVariant(string targetFolder, string baseName, string outputName,
            RoBitmapImage[] indexed, RoTrueColorImage[] trueColor, Color32[] palette,
            RoAction[] actions, string[] sounds, string sourceSprGuid)
        {
            var asset = GetOrCreateSpriteData(Path.Combine(targetFolder, outputName + ".asset"));
            var atlasPath = Path.Combine(targetFolder, "Atlas", outputName + "_atlas.png").Replace('\\', '/');
            using var loader = new RagnarokSpriteLoader(indexed, trueColor, palette);
            loader.Load(baseName, atlasPath, asset);
            asset.SourceSprGuid = sourceSprGuid;
            SetUpSpriteData(loader, asset, actions, sounds, baseName, outputName);
        }

        private static int ToRuntimeIndex(RoLayer layer, int bitmapImageCount, int trueColorImageCount)
        {
            if (layer.Index == EmptyLayerIndex) return EmptyLayerIndex;
            var trueColor = layer.Type == TrueColorLayerType;
            var imageCount = trueColor ? trueColorImageCount : bitmapImageCount;
            if (layer.Index < 0 || layer.Index >= imageCount)
                throw new InvalidDataException($"ACT layer references missing SPR image {layer.Index} (type {layer.Type}).");
            return trueColor ? layer.Index + bitmapImageCount : layer.Index;
        }

        private static int ConvertDelay(RoAct act, int actionIndex)
        {
            if (act.ActionDelays == null) return DefaultDelayMilliseconds;
            var milliseconds = act.ActionDelays[actionIndex] * MillisecondsPerDelayUnit;
            if (float.IsNaN(milliseconds) || milliseconds < 0 || (double)milliseconds > int.MaxValue)
                throw new InvalidDataException($"ACT action {actionIndex} has an invalid delay.");
            return (int)milliseconds;
        }

        internal static RoAction[] CreateRuntimeActions(RoAct act, int bitmapImageCount, int trueColorImageCount)
        {
            var runtimeActions = new RoAction[act.Actions.Length];
            for (var actionIndex = 0; actionIndex < act.Actions.Length; actionIndex++)
            {
                var sourceAction = act.Actions[actionIndex];
                var frames = new RoFrame[sourceAction.Frames.Length];
                for (var frameIndex = 0; frameIndex < sourceAction.Frames.Length; frameIndex++)
                {
                    var sourceFrame = sourceAction.Frames[frameIndex];
                    var layers = new RoLayer[sourceFrame.Layers.Length];
                    for (var layerIndex = 0; layerIndex < sourceFrame.Layers.Length; layerIndex++)
                    {
                        var sourceLayer = sourceFrame.Layers[layerIndex];
                        layers[layerIndex] = new RoLayer
                        {
                            Position = sourceLayer.Position,
                            Index = ToRuntimeIndex(sourceLayer, bitmapImageCount, trueColorImageCount),
                            IsMirror = sourceLayer.IsMirror,
                            Scale = sourceLayer.Scale,
                            Color = sourceLayer.Color,
                            Angle = sourceLayer.Angle,
                            Type = sourceLayer.Type,
                            Width = sourceLayer.Width,
                            Height = sourceLayer.Height
                        };
                    }

                    RoPos[] positions = null;
                    if (sourceFrame.Pos != null)
                    {
                        positions = new RoPos[sourceFrame.Pos.Length];
                        for (var i = 0; i < positions.Length; i++)
                            positions[i] = new RoPos
                            {
                                Unknown1 = sourceFrame.Pos[i].Unknown1,
                                Position = sourceFrame.Pos[i].Position,
                                Unknown2 = sourceFrame.Pos[i].Unknown2
                            };
                    }

                    frames[frameIndex] = new RoFrame
                    {
                        Layers = layers,
                        Pos = positions,
                        Sound = sourceFrame.Sound,
                        IsAttackFrame = act.Sounds != null && sourceFrame.Sound >= 0 &&
                            act.Sounds[sourceFrame.Sound] == AttackSoundName
                    };
                }
                runtimeActions[actionIndex] = new RoAction { Delay = ConvertDelay(act, actionIndex), Frames = frames };
            }
            return runtimeActions;
        }

        private static RoSpriteData GetOrCreateSpriteData(string path)
        {
            path = path.Replace("\\", "/");
            var asset = AssetDatabase.LoadAssetAtPath<RoSpriteData>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<RoSpriteData>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SetUpSpriteData(RagnarokSpriteLoader spriteLoader, RoSpriteData asset,
            RoAction[] actions, string[] sounds, string baseName, string outputName)
        {
            asset.Sounds = null;
            asset.AttackFrameTime = 0;
            asset.Actions = actions;
            asset.Sprites = spriteLoader.Sprites.ToArray();
            asset.SpriteSizes = spriteLoader.SpriteSizes.ToArray();
            asset.Name = outputName;
            asset.Atlas = spriteLoader.Atlas;
            asset.SpritesPerPalette = spriteLoader.SpriteFrameCount;

            if (sounds != null)
            {
                asset.Sounds = new AudioClip[sounds.Length];

                for (var i = 0; i < asset.Sounds.Length; i++)
                {
                    var s = sounds[i];
                    if (s == AttackSoundName)
                        continue;
                    var sPath = $"Assets/Sounds/{s}";
                    if (!File.Exists(sPath))
                        sPath = $"Assets/Sounds/{s.Replace(".wav", "")}.ogg";

                    var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(sPath);
                    if (sound == null)
                        Debug.Log("Could not find sound " + sPath + " for sprite " + baseName);
                    asset.Sounds[i] = sound;
                }
            }

            switch (asset.Actions.Length)
            {
                case 8:
                    asset.Type = SpriteType.Npc;
                    break;
                case 13:
                    asset.Type = SpriteType.Npc;
                    break;
                case 32:
                    asset.Type = SpriteType.ActionNpc;
                    break;
                case 39: //mi gao/increase soil for some reason
                case 40:
                case 41: //zerom for some reason
                    asset.Type = SpriteType.Monster;
                    break;
                case 47: //dullahan for some reason
                case 48:
                    asset.Type = SpriteType.Monster2;
                    break;
                case 56:
                    asset.Type = SpriteType.Monster; //???
                    break;
                case 64:
                    asset.Type = SpriteType.Monster;
                    break;
                case 72:
                    asset.Type = SpriteType.Pet;
                    break;
            }

            var maxExtent = 0f;
            var totalWidth = 0f;
            var widthCount = 0;

            foreach (var a in asset.Actions)
            {
                var frameId = 0;
                foreach (var f in a.Frames)
                {
                    if (f.IsAttackFrame)
                        asset.AttackFrameTime = a.Delay * frameId;

                    frameId++;

                    foreach (var l in f.Layers)
                    {
                        if (l.Index == EmptyLayerIndex)
                            continue;
                        var sprite = asset.SpriteSizes[l.Index];
                        var y = l.Position.y + sprite.y / 2f;
                        if (l.Position.x < 0)
                            y = Mathf.Abs(l.Position.y - sprite.y / 2f);
                        if (y > maxExtent)
                            maxExtent = y;
                        totalWidth += Mathf.Abs(l.Position.x) + sprite.x / 2f;
                        widthCount++;
                    }
                }
            }

            // Prefer an attack marker in the primary attack motion.
            if (asset.Type == SpriteType.Monster || asset.Type == SpriteType.Monster2 || asset.Type == SpriteType.Pet)
            {
                var actionId = RoAnimationHelper.GetMotionIdForSprite(asset.Type, SpriteMotion.Attack1);
                if (actionId == -1)
                    actionId = RoAnimationHelper.GetMotionIdForSprite(asset.Type, SpriteMotion.Attack2);
                if (actionId >= 0 && actionId < asset.Actions.Length)
                {
                    var frames = asset.Actions[actionId].Frames;
                    var found = false;

                    for (var j = 0; j < frames.Length; j++)
                    {
                        if (frames[j].IsAttackFrame)
                        {
                            asset.AttackFrameTime = j * asset.Actions[actionId].Delay;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        var pos = frames.Length - 2;
                        if (pos < 0)
                            pos = 0;
                        asset.AttackFrameTime = pos * asset.Actions[actionId].Delay;
                    }
                }
            }

            asset.Size = Mathf.CeilToInt(maxExtent);
            asset.AverageWidth = widthCount == 0 ? 0 : totalWidth / widthCount;

            // Use the idle pose so overhead attacks don't raise emotes and cast bars.
            EditorUtility.SetDirty(asset);
            asset.StandingHeight = 20;
            if (asset.Actions.Length > 0 && asset.Actions[0].Frames.Length > 0)
            {
                var maxHeight = 0f;
                var firstFrame = asset.Actions[0].Frames[0];
                for (var i = 0; i < firstFrame.Layers.Length; i++)
                {
                    var l = firstFrame.Layers[i];
                    if (l.Index < 0)
                        continue;
                    var curSpr = asset.Sprites[l.Index];
                    var height = curSpr.rect.height / 2 - l.Position.y; //y is negative
                    if (height > maxHeight)
                        maxHeight = height;
                }

                if (maxHeight > asset.StandingHeight)
                    asset.StandingHeight = maxHeight;
            }
        }
    }
}
