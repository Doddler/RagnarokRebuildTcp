using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.MapEditor.Editor
{
	class RagnarokResourceLoader
	{
		private FileStream fs;
		private BinaryReader br;

		private RagnarokWorld world;
        private RoMapData mapData;

        private GameObject parentBox;

		private void LoadModel(int index)
		{
			var model = new RoWorldModel();
			model.Index = index;
			if (world.Version > 13)
			{
				model.Name = br.ReadKoreanString(40);
				model.AnimType = br.ReadInt32();
				model.AnimSpeed = br.ReadSingle();
				model.BlockType = br.ReadInt32();
			}

			model.FileName = br.ReadKoreanString(80);
			model.NodeName = br.ReadKoreanString(80);

			model.Position = br.ReadVector3();
			model.Rotation = br.ReadVector3();
			model.Scale = br.ReadVector3();

			//var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
			//cube.name = "Model " + model.Name;
			//cube.transform.parent = parentBox.transform;
			//cube.transform.localPosition = new Vector3(model.Position.x / 5, -model.Position.y / 5, model.Position.z / 5);
			//cube.transform.localScale = model.Scale;
			//cube.transform.rotation = Quaternion.Euler(model.Rotation);

			world.Models.Add(model);

			//Debug.Log($"Model: {model.Name} type {model.AnimType} speed {model.AnimSpeed} blockType {model.BlockType} filename {model.FileName} node {model.NodeName} pos {model.Position} rot {model.Rotation} scale {model.Scale}");
		}

		private void LoadLight(int index)
		{
			var light = new RoWorldLight();

			light.Index = index;
			light.Name = "Light " + br.ReadKoreanString(80);
			light.Position = br.ReadVector3();
			light.Color = new Color(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
			light.Range = br.ReadSingle();

			//var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			//sphere.name = light.Name;
			//sphere.transform.parent = parentBox.transform;
			//sphere.transform.localPosition = new Vector3(light.Position.x/5, -light.Position.y/5, light.Position.z/5);
			//sphere.transform.localScale = new Vector3(light.Range/5, light.Range/5, light.Range/5);

			//var lobj = new GameObject(light.Name);
			//lobj.transform.parent = parentBox.transform;
			//lobj.transform.localPosition = new Vector3(light.Position.x / 5, -light.Position.y / 5, light.Position.z / 5);

			//var l = lobj.AddComponent<Light>();
			//l.type = LightType.Point;
			//l.range = light.Range / 5f;
			//l.color = light.Color;
			//l.intensity = 10f;
			//l.lightmapBakeType = LightmapBakeType.Mixed;
			//l.shadows = LightShadows.Soft;

			//Debug.Log($"Light: {light.Name} pos: {light.Position} color: {light.Color} range: {light.Range}");
			world.Lights.Add(light);
		}

		private void LoadSound(int index)
		{
			var sound = new RoWorldSound();

			sound.Index = index;
			sound.Name = br.ReadKoreanString(80);
			sound.File = br.ReadKoreanString(80);
			sound.Position = br.ReadVector3();
			sound.Volume = br.ReadSingle();
			sound.Width = br.ReadInt32();
			sound.Height = br.ReadInt32();
			sound.Range = br.ReadSingle();
			if (world.Version >= 20)
				sound.Cycle = br.ReadSingle();

			//Debug.Log($"Sound {sound.Name} file {sound.File} pos {sound.Position} vol {sound.Volume} width {sound.Width} height {sound.Height} range {sound.Range} cycle {sound.Cycle}");


			//var obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
			//obj.name = "Sound " + sound.Name;
			//obj.transform.parent = parentBox.transform;
			//obj.transform.localPosition = new Vector3(sound.Position.x / 5, -sound.Position.y / 5, sound.Position.z / 5);

			world.Sounds.Add(sound);
		}

		private void LoadEffect(int index)
		{
			var effect = new RoWorldEffect();

			effect.Index = index;
			effect.Name = br.ReadKoreanString(80);
			effect.Position = br.ReadVector3();
			effect.Id = br.ReadInt32();
			effect.Delay = br.ReadSingle() * 10f;
			effect.Param = br.ReadVector4();

			//Debug.Log($"Effect: {effect.Name} pos {effect.Position} Id {effect.Id} Delay {effect.Delay} Param {effect.Param}");

			//var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			//obj.name = "Effect " + effect.Name;
			//obj.transform.parent = parentBox.transform;
			//obj.transform.localPosition = new Vector3(effect.Position.x / 5, -effect.Position.y / 5, effect.Position.z / 5);

			world.Effects.Add(effect);

		}

		private RagnarokWorld Load(string path, RoMapData mapData)
		{
			var filename = path;
			var basename = Path.GetFileNameWithoutExtension(filename);
			var basedir = Path.GetDirectoryName(path);

			fs = new FileStream(filename, FileMode.Open);
			br = new BinaryReader(fs);

            this.mapData = mapData;

			var header = new string(br.ReadChars(4));
			if (header != "GRSW")
				throw new Exception("Not world resource");

			Debug.Log("Loading ragnarok world resource file " + basename);

			world = ScriptableObject.CreateInstance<RagnarokWorld>();

			world.MapName = basename;

			var majorVersion = br.ReadByte();
			var minorVersion = br.ReadByte();
			world.Version = majorVersion * 10 + minorVersion;

			var oldBox = GameObject.Find($"{basename} resources");
			if (oldBox != null)
				GameObject.DestroyImmediate(oldBox);


			Debug.Log("Version: " + world.Version);

			world.IniFileName = br.ReadKoreanString(40);
			world.GndFileName = br.ReadKoreanString(40);
			world.GatFileName = br.ReadKoreanString(40);
			if (world.Version >= 14)
				world.SrcFileName = br.ReadKoreanString(40);

			//Debug.Log("Ini: " + world.IniFileName);
			//Debug.Log("Gnd: " + world.GndFileName);
			//Debug.Log("Gat: " + world.GatFileName);
			//Debug.Log("Src: " + world.SrcFileName);

			//water stuff
			if (world.Version >= 13)
			{
				var water = new RoWater();

				water.Level = br.ReadSingle() / 5f;

				if (world.Version >= 18)
				{
					water.Type = br.ReadInt32();
					water.WaveHeight = br.ReadSingle();
					water.WaveSpeed = br.ReadSingle();
					water.WavePitch = br.ReadSingle();

					if (world.Version >= 19)
						water.AnimSpeed = br.ReadInt32();
				}

				world.Water = water;

                mapData.Water = new MapWater()
                {
                    Type = water.Type,
                    AnimSpeed = water.AnimSpeed,
                    Level = water.Level,
                    WaveHeight = water.WaveHeight,
                    WavePitch = water.WavePitch,
                    WaveSpeed = water.WaveSpeed
                };

				EditorUtility.SetDirty(mapData);

				Debug.Log($"Water: Level {water.Level} Type: {water.Type} Height: {water.WaveHeight} Speed: {water.WaveSpeed} Pitch: {water.WavePitch} AnimSpeed: {water.AnimSpeed}");
			}

			//lightmap stuff
			if (world.Version >= 15)
			{
				var light = new RoLightSetup();
				light.Latitude = br.ReadInt32();
				light.Longitude = br.ReadInt32();
				light.Diffuse = br.ReadColorNoAlpha();
				light.Ambient = br.ReadColorNoAlpha();

				if (world.Version >= 17)
					light.Opacity = br.ReadSingle();

				Debug.Log($"Lightmap: lat:{light.Latitude} lng:{light.Longitude} diff:{light.Diffuse} Ambient:{light.Ambient} Opacity:{light.Opacity}");

				world.LightSetup = light;
			}

			//ground stuff?
			if (world.Version >= 15)
			{
				var top = br.ReadInt32(); //top
				var bottom = br.ReadInt32(); //bottom
				var left = br.ReadInt32(); //left
				var right = br.ReadInt32(); //right

				Debug.Log($"Ground: {top} {bottom} {left} {right}");
			}

			var objCount = br.ReadInt32();
			Debug.Log("Objects: " + objCount);

			for (var i = 0; i < objCount; i++)
			{
				var type = br.ReadInt32();

				switch (type)
				{
					case 1:
						LoadModel(i);
						break;
					case 2:
						LoadLight(i);
						break;
					case 3:
						LoadSound(i);
						break;
					case 4:
						LoadEffect(i);
						break;
					default:
						Debug.LogWarning("Unhandled type " + type);
						return null;
				}
			}

			Debug.Log($"Loaded rsw world data {fs.Position} out of {fs.Length}");

			world.FogSetup = LoadFogData(basedir, basename);

            world.LightSetup.UseMapAmbient = CheckUseMapAmbient(basedir, basename);

			Debug.Log($"UseAmbient: " + world.LightSetup.UseMapAmbient);

			return world;
		}

        public bool CheckUseMapAmbient(string baseDir, string mapName)
        {
            var lightPath = Path.Combine(baseDir, "mapobjlighttable.txt");
            var lines = File.ReadAllLines(lightPath);

            var line = lines.FirstOrDefault(l => l.Contains(mapName + ".rsw"));

            if (line == null)
                return true;

            return line.ToLower().Contains("on");
        }

		public RoFogSetup LoadFogData(string baseDir, string mapName)
		{
			var fogPath = Path.Combine(baseDir, "fogparametertable.txt");
			var lines = File.ReadAllLines(fogPath);

			var lineId = -1;
			var newLines = new List<string>(lines.Length);
			var rswName = mapName + ".rsw";

			for (var i = 0; i < lines.Length; i++)
			{
				var line = lines[i];
				if (line.StartsWith("//") || string.IsNullOrWhiteSpace(line))
					continue;

				var l = line.Replace("#", "");
				newLines.Add(l);
				if (l == rswName)
					lineId = newLines.Count - 1;
			}

			if (lineId < 0)
				return null;

			Debug.Log(newLines[lineId + 1] + " : " + newLines[lineId + 2] + " " + newLines[lineId + 3]);
			
			var near = float.Parse(newLines[lineId + 1]);
			var far = float.Parse(newLines[lineId + 2]);

            var hex = newLines[lineId + 3].Substring(2);
            if (hex.Length < 8)
                hex = new string('0', 8 - hex.Length) + hex;


			var a = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
			var r = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
			var g = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
			var b = int.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
			var color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);

			return new RoFogSetup() { NearPlane = near, FarPlane = far, FogColor = color };
		}

		//[MenuItem("Ragnarok/Import World Resource")]
		public static RagnarokWorld LoadResourceFile(string path, RoMapData mapData)
		{
			var loader = new RagnarokResourceLoader();
            return loader.Load(path, mapData);

			//DestroyImmediate(loader);
		}
	}
}
