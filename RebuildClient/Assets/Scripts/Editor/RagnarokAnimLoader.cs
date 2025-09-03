using System;
using System.Collections.Generic;
using System.IO;
using Assets.Scripts;
using UnityEngine;

namespace Assets.Editor
{

	class RagnarokActLoader
	{
		private static Stream fs;
		private static BinaryReader br;

		private static RagnarokSpriteLoader sprite;

		private static int version;

		public string[] Sounds;

		private RoFrame ReadLayers()
		{
			var count = br.ReadUInt32();
			var layers = new RoLayer[count];

			var anim = new RoFrame();

			for (var i = 0; i < count; i++)
			{
				var layer = new RoLayer()
				{
					Position = new Vector2(br.ReadInt32(), br.ReadInt32()),
					Index = br.ReadInt32(),
					IsMirror = br.ReadInt32() != 0,
					Scale = new Vector2(1, 1),
					Color = Color.white,
				};

				if (version >= 20)
				{
					var r = br.ReadByte();
					var g = br.ReadByte();
					var b = br.ReadByte();
					var a = br.ReadByte();

					layer.Color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);

					var scalex = br.ReadSingle();
					var scaley = scalex;
					if (version > 23)
						scaley = br.ReadSingle();

					layer.Scale = new Vector2(scalex, scaley);

					layer.Angle = br.ReadInt32();
					layer.Type = br.ReadInt32();

					if (layer.Type == 1)
						layer.Index += sprite.IndexCount;

					if (version >= 25)
					{
						layer.Width = br.ReadInt32();
						layer.Height = br.ReadInt32();
					}
				}

				layers[i] = layer;
			}

			anim.Layers = layers;

			if (version >= 20)
				anim.Sound = br.ReadInt32();
			else
				anim.Sound = -1;

			if (version >= 23)
			{
				var pcount = br.ReadInt32();
				var posList = new RoPos[pcount];

				for (var i = 0; i < pcount; i++)
				{
					var pos = new RoPos();

					pos.Unknown1 = br.ReadInt32();
					pos.Position = new Vector2(br.ReadInt32(), br.ReadInt32());
					pos.Unknown2 = br.ReadInt32();

					posList[i] = pos;
				}

				anim.Pos = posList;
			}

			return anim;
		}

		private RoFrame[] ReadAnimations()
		{
			var count = br.ReadUInt32();
			var anims = new RoFrame[count];

			for (var i = 0; i < count; i++)
			{
				fs.Seek(32, SeekOrigin.Current);
				anims[i] = ReadLayers();
			}

			return anims;
		}

		private RoAction[] ReadActions()
		{
			var count = br.ReadUInt16();
			fs.Seek(10, SeekOrigin.Current);

			var actions = new RoAction[count];

			for (var i = 0; i < count; i++)
			{
				var action = new RoAction();
				action.Delay = 150;
				action.Frames = ReadAnimations();

				actions[i] = action;
			}

			return actions;
		}
		
		
		private void LoadImf(string imfPath, RoAction[] actions)
		{
			var b = File.ReadAllBytes(imfPath);
			using var ms = new MemoryStream(b);
			using var br = new BinaryReader(ms);

			var ver = br.ReadSingle();
			var unknown1 = br.ReadInt32();
			var unknown2 = br.ReadInt32();
			var actionCount = br.ReadInt32();
			
			 // Debug.Log($"Loading related imf file {imfPath}");

			for (var i = 0; i < actionCount; i++)
			{
				var action = actions[i];
				var frameCount = br.ReadInt32();
                for (var j = 0; j < frameCount; j++)
				{
					var dat1 = br.ReadInt32();
					var dat2 = br.ReadInt32();
					var dat3 = br.ReadInt32();
					if (j < action.Frames.Length) // Skip frames that are out outside the Array bounds (resulting from inconsistencies between imf and act files)
						action.Frames[j].IsForeground = dat1 == 0;
					// Debug.Log($"Action {i} Frame {j} isForeground = {dat1 == 0}");
				}
			}
			
			//technically the foreground/background option for heads is also in the imf but it's always opposite of body so we don't care
		}

		public List<RoAction> Load(RagnarokSpriteLoader spriteLoader, string actfile, string imfFile)
		{
			sprite = spriteLoader;

			var basename = Path.GetFileNameWithoutExtension(actfile);

			fs = new FileStream(actfile, FileMode.Open);
			br = new BinaryReader(fs);

			var header = new string(br.ReadChars(2));
			if (header != "AC")
				throw new Exception("Not action");

			var minorVersion = br.ReadByte();
			var majorVersion = br.ReadByte();
			version = majorVersion * 10 + minorVersion;

			var actions = ReadActions();

			//string[] sounds;


			if (version >= 21)
			{
				var count = br.ReadInt32();
				Sounds = new string[count];

				for (var i = 0; i < count; i++)
				{
					Sounds[i] = new string(br.ReadChars(40)).TrimEnd('\0');
				}
				
				for (var i = 0; i < actions.Length; i++)
				{
					var frameCount = actions[i].Frames.Length;

					for (var j = 0; j < frameCount; j++)
					{
						var frame = actions[i].Frames[j];
						if (frame.Sound >= 0)
						{
							var sName = Sounds[frame.Sound];
							if (sName == "atk")
								frame.IsAttackFrame = true;
						}
					}
				}
			}

			if (version >= 22)
			{
				for (var i = 0; i < actions.Length; i++)
					actions[i].Delay = (int)(br.ReadSingle() * 24);
			}
			else
			{
				Console.WriteLine($"Processed ACT {actfile} with version {version}");
			}
			
			fs.Close();
			
			if(File.Exists(imfFile))
				LoadImf(imfFile, actions);

			return new List<RoAction>(actions);
		}
	}
}
