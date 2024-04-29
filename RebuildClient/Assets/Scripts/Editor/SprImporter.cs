using System.IO;
using Assets.Editor;
using Assets.Scripts;
using UnityEditor;
using UnityEngine;

public class LayerCurveSet
{
    public string Path;
    public EditorCurveBinding SpriteBinding;
    public EditorCurveBinding PositionXBinding;
    public EditorCurveBinding PositionYBinding;
    public EditorCurveBinding PositionZBinding;
}

public static class ImporterExtensions
{
    public static Keyframe SetConstant(this Keyframe frame)
    {
        frame.inTangent = Mathf.Infinity;
        frame.outTangent = Mathf.Infinity;
        return frame;
    }
}


[UnityEditor.AssetImporters.ScriptedImporter(1, "spr")]
public class SprImporter : UnityEditor.AssetImporters.ScriptedImporter
{
    public override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext ctx)
    {
        var path = ctx.assetPath;
        var name = Path.GetFileNameWithoutExtension(ctx.assetPath);

        var spr = new RagnarokSpriteLoader();
        spr.Load(ctx);

        var basePath = Path.GetDirectoryName(path);
        var baseName = Path.GetFileNameWithoutExtension(path);
        var actName = Path.Combine(basePath, baseName + ".act");

        if (File.Exists(actName))
        {
            var actLoader = new RagnarokActLoader();
            var actions = actLoader.Load(ctx, spr, actName);

            var asset = ScriptableObject.CreateInstance(typeof(RoSpriteData)) as RoSpriteData;
            asset.Actions = actions.ToArray();
            asset.Sprites = spr.Sprites.ToArray();
            asset.SpriteSizes = spr.SpriteSizes.ToArray();
            asset.Name = baseName;
            asset.Atlas = spr.Atlas;
            if (actLoader.Sounds != null)
            {
                asset.Sounds = new AudioClip[actLoader.Sounds.Length];

                for (var i = 0; i < asset.Sounds.Length; i++)
                {
                    var s = actLoader.Sounds[i];
                    if (s == "atk")
                        continue;
                    var sPath = $"Assets/Sounds/{s}";
                    if(!File.Exists(sPath))
                        sPath = $"Assets/Sounds/{s.Replace(".wav", ".ogg")}";
                    var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(sPath);
                    if (sound == null)
                        Debug.Log("Could not find sound " + sPath + " for sprite " + name);
                    asset.Sounds[i] = sound;
                }
            }
            //asset.Sounds = asset.Sounds.ToArray();
            
            //Debug.Log(asset.Sprites.Length);

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
                case 40:
                case 41: //zerom for some reason
                    asset.Type = SpriteType.Monster;
                    break;
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
                        if (l.Index == -1)
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

            asset.Size = Mathf.CeilToInt(maxExtent);
            asset.AverageWidth = totalWidth / widthCount;

            //Debug.Log(asset.Actions.Length);

            ctx.AddObjectToAsset(name + " data", asset);
            ctx.SetMainObject(asset);
            
            //CreateObjectWithAnimations(obj, ctx, spr.Sprites, actions);
        }
    }
}
