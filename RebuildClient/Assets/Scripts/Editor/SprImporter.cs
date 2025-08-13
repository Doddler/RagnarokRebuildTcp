
//
// public class LayerCurveSet
// {
//     public string Path;
//     public EditorCurveBinding SpriteBinding;
//     public EditorCurveBinding PositionXBinding;
//     public EditorCurveBinding PositionYBinding;
//     public EditorCurveBinding PositionZBinding;
// }
//
// public static class ImporterExtensions
// {
//     public static Keyframe SetConstant(this Keyframe frame)
//     {
//         frame.inTangent = Mathf.Infinity;
//         frame.outTangent = Mathf.Infinity;
//         return frame;
//     }
// }
//
//
//
// [UnityEditor.AssetImporters.ScriptedImporter(1, "spr")]
// public class SprImporter : ScriptedImporter
// {
//     public override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext ctx)
//     {
//         var path = ctx.assetPath;
//         var name = Path.GetFileNameWithoutExtension(ctx.assetPath);
//
//         var spr = new RagnarokSpriteLoader();
//         spr.Load(ctx);
//         
//         // if(ctx.selectedBuildTarget == BuildTarget.WebGL)
//         //     spr.Atlas.Compress(true);
//
//         var basePath = Path.GetDirectoryName(path);
//         var baseName = Path.GetFileNameWithoutExtension(path);
//         var actName = Path.Combine(basePath, baseName + ".act");
//         
//         if (File.Exists(actName))
//         {
//             var actLoader = new RagnarokActLoader();
//             var actions = actLoader.Load(spr, actName);
//
//             var asset = ScriptableObject.CreateInstance(typeof(RoSpriteData)) as RoSpriteData;
//             asset.Actions = actions.ToArray();
//             asset.Sprites = spr.Sprites.ToArray();
//             asset.SpriteSizes = spr.SpriteSizes.ToArray();
//             asset.Name = baseName;
//             asset.Atlas = spr.Atlas;
//             asset.SpritesPerPalette = spr.SpriteFrameCount;
//             if (actLoader.Sounds != null)
//             {
//                 asset.Sounds = new AudioClip[actLoader.Sounds.Length];
//
//                 for (var i = 0; i < asset.Sounds.Length; i++)
//                 {
//                     var s = actLoader.Sounds[i];
//                     if (s == "atk")
//                         continue;
//                     var sPath = $"Assets/Sounds/{s}";
//                     if(!File.Exists(sPath))
//                         sPath = $"Assets/Sounds/{s.Replace(".wav", "")}.ogg";
//
//                     var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(sPath);
//                     if (sound == null)
//                         Debug.Log("Could not find sound " + sPath + " for sprite " + name);
//                     asset.Sounds[i] = sound;
//                 }
//             }
//             //asset.Sounds = asset.Sounds.ToArray();
//             
//             //Debug.Log(asset.Sprites.Length);
//
//             switch (asset.Actions.Length)
//             {
//
//                 case 8:
//                     asset.Type = SpriteType.Npc;
//                     break;
//                 case 13:
//                     asset.Type = SpriteType.Npc;
//                     break;
//                 case 32:
//                     asset.Type = SpriteType.ActionNpc;
//                     break;
//                 case 39: //mi gao/increase soil for some reason
//                 case 40:
//                 case 41: //zerom for some reason
//                     asset.Type = SpriteType.Monster;
//                     break;
//                 case 47: //dullahan for some reason
//                 case 48:
//                     asset.Type = SpriteType.Monster2;
//                     break;
//                 case 56:
// 	                asset.Type = SpriteType.Monster; //???
// 	                break;
//                 case 64:
//                     asset.Type = SpriteType.Monster;
//                     break;
//                 case 72:
//                     asset.Type = SpriteType.Pet;
//                     break;
//             }
//
//             var maxExtent = 0f;
//             var totalWidth = 0f;
//             var widthCount = 0;
//
//             foreach (var a in asset.Actions)
//             {
// 	            var frameId = 0;
//                 foreach (var f in a.Frames)
//                 {
// 	                if (f.IsAttackFrame)
// 		                asset.AttackFrameTime = a.Delay * frameId;
//
// 	                frameId++;
//
//                     foreach (var l in f.Layers)
//                     {
//                         if (l.Index == -1)
//                             continue;
//                         var sprite = asset.SpriteSizes[l.Index];
//                         var y = l.Position.y + sprite.y / 2f;
//                         if (l.Position.x < 0)
//                             y = Mathf.Abs(l.Position.y - sprite.y / 2f);
//                         if (y > maxExtent)
//                             maxExtent = y;
//                         totalWidth += Mathf.Abs(l.Position.x) + sprite.x / 2f;
//                         widthCount++;
//                     }
//                 }
//             }
//             
//             //far better way to get sprite attack timing
//             if (asset.Type == SpriteType.Monster || asset.Type == SpriteType.Monster2 || asset.Type == SpriteType.Pet)
//             {
//                 var actionId = RoAnimationHelper.GetMotionIdForSprite(asset.Type, SpriteMotion.Attack1);
//                 if (actionId == -1)
//                     actionId = RoAnimationHelper.GetMotionIdForSprite(asset.Type, SpriteMotion.Attack2);
//                 if (actionId >= 0)
//                 {
//
//                     var frames = asset.Actions[actionId].Frames;
//                     var found = false;
//
//                     for (var j = 0; j < frames.Length; j++)
//                     {
//                         if (frames[j].IsAttackFrame)
//                         {
//                             asset.AttackFrameTime = j * asset.Actions[actionId].Delay;
//                             found = true;
//                             break;
//                         }
//                     }
//
//                     if (!found)
//                     {
//                         var pos = frames.Length - 2;
//                         if (pos < 0)
//                             pos = 0;
//                         asset.AttackFrameTime = pos * asset.Actions[actionId].Delay;
//                     }
//                 }
//             }
//             
//             asset.Size = Mathf.CeilToInt(maxExtent);
//             asset.AverageWidth = totalWidth / widthCount;
//
//             //Debug.Log(asset.Actions.Length);
//
//             ctx.AddObjectToAsset(name + " data", asset);
//             ctx.SetMainObject(asset);
//             //
//             // EditorApplication.delayCall += () =>
//             // {
//             //     AssetDatabase.Refresh();
//             //
//             //     var atlasPath = Path.Combine(Path.Combine(Path.GetDirectoryName(assetPath), "atlas/", $"{name}_atlas.png"));
//             //     TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(atlasPath);
//             //     importer.textureType = TextureImporterType.Default;
//             //     importer.npotScale = TextureImporterNPOTScale.None;
//             //     importer.textureCompression = TextureImporterCompression.Compressed;
//             //     importer.crunchedCompression = true;
//             //     importer.compressionQuality = 50;
//             //     importer.wrapMode = TextureWrapMode.Clamp;
//             //     importer.isReadable = false;
//             //     importer.mipmapEnabled = false;
//             //     importer.alphaIsTransparency = true;
//             //     importer.maxTextureSize = 4096;
//             //
//             //     EditorUtility.SetDirty(importer);
//             //     importer.SaveAndReimport();
//             //     AssetDatabase.Refresh();
//             //
//             //     var newTex = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
//             //     
//             //     //var atlasPath = Path.Combine(Path.GetDirectoryName(path), "atlas/", asset.Atlas[0].name + ".png");
//             //     var spr = AssetDatabase.LoadAssetAtPath<RoSpriteData>(assetPath);
//             //     spr.Atlas = newTex;
//             //     EditorUtility.SetDirty(spr);
//             //     EditorUtility.SetDirty(spr.Atlas);
//             //     EditorUtility.SetDirty(newTex);
//             //     AssetDatabase.SaveAssets();
//             //     AssetDatabase.Refresh();
//             // };
//
//             //CreateObjectWithAnimations(obj, ctx, spr.Sprites, actions);
//         }
//     }
// }
