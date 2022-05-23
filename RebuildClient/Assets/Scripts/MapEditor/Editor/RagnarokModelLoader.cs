using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using FileMode = System.IO.FileMode;

namespace Assets.Scripts.MapEditor.Editor
{
	class RagnarokModelLoader : IDisposable
	{
		private FileStream fs;
		private BinaryReader br;
		private RsmModel model;

		private Texture2D atlas;
		private Rect[] atlasRects;

		//private int textureIndex = 0;

		private string savePath;
		private string baseName;

		private int nameId = 0;

		private RsmNode LoadNode()
		{
			var node = new RsmNode();

			node.Name = br.ReadKoreanString(40);
			node.ParentName = br.ReadKoreanString(40);

			if (string.IsNullOrWhiteSpace(node.Name))
			{
				node.Name = $"Object {nameId}";
				nameId++;
			}

			var textureCount = br.ReadInt32();
			node.TextureIds = new List<int>();

			for (var i = 0; i < textureCount; i++)
			{
				node.TextureIds.Add(br.ReadInt32());
			}

			var forward = br.ReadVector3();
			var up = br.ReadVector3();
			var right = br.ReadVector3();

			node.OffsetMatrix = new Matrix4x4(forward, up, right, new Vector4(0, 0, 0, 1));
			node.Offset = br.ReadVector3();
			node.Position = br.ReadVector3();
			node.RotationAngle = br.ReadSingle();
			node.RotationAxis = br.ReadVector3();
			node.Scale = br.ReadVector3();

			var vertCount = br.ReadInt32();

			for (var i = 0; i < vertCount; i++)
			{
				node.Vertices.Add(node.OffsetMatrix * br.ReadVector3());
			}

			var uvCount = br.ReadInt32();

			for (var i = 0; i < uvCount; i++)
			{
				node.Colors.Add(model.Version >= 12 ? br.ReadByteColor() : Color.white);

				var uv = br.ReadVector2();
				uv.x = Mathf.Clamp(uv.x, 0, 1);
				uv.y = Mathf.Clamp(1 - uv.y, 0, 1); //1 - (uv.y * 0.98f + 0.01f); //wut

				node.UVs.Add(uv);
			}

			var faceCount = br.ReadInt32();

			for (var i = 0; i < faceCount; i++)
			{
				var face = new RsmFace();

				face.VertexIds[0] = br.ReadUInt16();
				face.VertexIds[1] = br.ReadUInt16();
				face.VertexIds[2] = br.ReadUInt16();

				face.UVIds[0] = br.ReadUInt16();
				face.UVIds[1] = br.ReadUInt16();
				face.UVIds[2] = br.ReadUInt16();

				face.TextureId = br.ReadUInt16();
				face.Padding = br.ReadUInt16();
				face.TwoSided = br.ReadInt32() == 1;
				if (model.Version >= 12)
					face.SmoothGroup = br.ReadInt32();

				node.Faces.Add(face);
			}

			if (model.Version > 15)
			{
				var posKeyFrames = br.ReadInt32();
				for (var i = 0; i < posKeyFrames; i++)
					node.PosKeyFrames.Add(new RsmPosKeyframe() { Frame = br.ReadInt32(), Position = br.ReadRoPosition() });
			}

			var rotKeyFrames = br.ReadInt32();
			for (var i = 0; i < rotKeyFrames; i++)
			{
				var keyframe = new RsmRotKeyFrame() { Frame = br.ReadInt32(), Rotation = br.ReadQuaternion().FlipY() };
				node.RotationKeyFrames.Add(keyframe);
				//Debug.Log(keyframe.Rotation.eulerAngles);
			}

			node.Matrix = Matrix4x4.identity;
			node.Bounds = new Bounds();

			return node;
		}

		public void LoadTextures(string savePath)
		{
			var path = Path.Combine("Assets/models/atlas", savePath); //Path.Combine(savePath);
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);

			var atlasName = $"{model.Name.Replace("\\", "_")}_atlas";
			var atlasPath = Path.Combine(path, atlasName + ".png");

			model.Textures = new List<string>();

			var textures = new List<Texture2D>();
			var texCount = br.ReadInt32();

			for (var i = 0; i < texCount; i++)
			{
				var tName = br.ReadKoreanString(40);
				model.Textures.Add(tName);

				var texout = TextureImportHelper.GetOrImportTextureToProject(tName, RagnarokDirectory.GetRagnarokDataDirectory, "Assets/models");
				textures.Add(texout);
			}

			TextureImportHelper.SetTexturesReadable(textures);

			var extratexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			textures.Add(extratexture);

			var supertexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			supertexture.name = atlasName;

			atlasRects = supertexture.PackTextures(textures.ToArray(), 2, 4096, false);

			if (File.Exists(atlasPath))
			{
				//we still needed to make the atlas to get the rects. We assume they're the same, but they might not be?
				GameObject.DestroyImmediate(supertexture);
				atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
				return;
			}

			TextureImportHelper.PatchAtlasEdges(supertexture, atlasRects);

			supertexture = TextureImportHelper.SaveAndUpdateTexture(supertexture, atlasPath);

			//var bytes = supertexture.EncodeToPNG();
			//File.WriteAllBytes(atlasPath, bytes);

			//TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(atlasPath);
			//importer.textureType = TextureImporterType.Default;
			//importer.npotScale = TextureImporterNPOTScale.None;
			//importer.textureFormat = TextureImporterFormat.Automatic;
			//importer.textureCompression = TextureImporterCompression.CompressedHQ;
			//importer.wrapMode = TextureWrapMode.Clamp;
			//importer.isReadable = false;
			//importer.mipmapEnabled = false;
			//importer.alphaIsTransparency = true;
			//importer.maxTextureSize = 4096;

			//importer.SaveAndReimport();

			//supertexture = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);

			//Get rid of mipmaps by copying the texture data. We have no need of mipmaps in our world.
			//var copyTexture = new Texture2D(supertexture.width, supertexture.height, TextureFormat.RGBA32, false);
			////var pixels = supertexture.GetPixels(0, 0, supertexture.width, supertexture.height, 0);
			////copyTexture.SetPixels(pixels);
			//copyTexture.name = supertexture.name;
			//supertexture = copyTexture;
			//supertexture.wrapMode = TextureWrapMode.Clamp;

			//AssetDatabase.CreateAsset(supertexture, atlasPath);

			atlas = supertexture;
		}

		public void SmoothNodeTriangles(RsmNode node, List<RsmTriangle> triangles)
		{
			//var maxSmoothGroup = node.Faces.Max(f => f.SmoothGroup);

			//we can assume all faces in a given triangle has the same normal at this point
			var faceNormals = new List<Vector3>();
			//var faceNormals = triangles.Select(t => new[] {t.Normals[0], t.Normals[1], t.Normals[2]}).ToList();
			foreach (var t in triangles)
				faceNormals.Add(VectorHelper.CalcNormal(t.Vertices[0], t.Vertices[1], t.Vertices[2]));

			for (var i = 0; i < node.Faces.Count; i++) //loop through the first set of faces
			{
				var face1 = node.Faces[i];
				var f1Normals = new[] { faceNormals[i], faceNormals[i], faceNormals[i] };
				var f1Counts = new[] { 1, 1, 1 };

				for (var j = 0; j < node.Faces.Count; j++) //loop through the second set of faces
				{
					var face2 = node.Faces[j];

					if (i == j || face1.SmoothGroup != face2.SmoothGroup) //they must be in the same smooth group and not the same face
						continue;

					for (var k = 0; k < 3; k++) //loop through each vertex in face1
					{
						for (var l = 0; l < 3; l++) //loop through each vertex in face2
						{
							if (face1.VertexIds[k] == face2.VertexIds[l]) //if the ids match, add their normal to the total
							{
								f1Normals[k] += faceNormals[j];
								f1Counts[k]++;
							}

						}
					}
				}

				for (var k = 0; k < 3; k++)
				{
					var normal = (f1Normals[k] / f1Counts[k]).normalized;
					if (normal.magnitude > 0.1) //discard normals that don't normalize, as that means it's not pointing any direction
						triangles[i].Normals[k] = normal;
				}
			}

		}

		public GameObject CompileNode(GameObject parentGameObject, RsmNode node, Material mat)
		{
			var go = new GameObject(node.Name);
			var mf = go.AddComponent<MeshFilter>();
			var mr = go.AddComponent<MeshRenderer>();
			var mc = go.AddComponent<MeshCollider>();

			go.layer = LayerMask.NameToLayer("Object");
			go.isStatic = true;
			mr.material = mat;
			mr.receiveGI = ReceiveGI.Lightmaps;
			mr.shadowCastingMode = ShadowCastingMode.TwoSided;
			mc.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation |
								MeshColliderCookingOptions.EnableMeshCleaning |
								MeshColliderCookingOptions.WeldColocatedVertices |
								MeshColliderCookingOptions.UseFastMidphase;

			go.transform.parent = parentGameObject.transform;

			var position = node.Position;
			var rotation = Quaternion.AngleAxis(node.RotationAngle * Mathf.Rad2Deg, node.RotationAxis).FlipY();
			var scale = node.Scale;
			var offset = node.Offset;

			if (node.PosKeyFrames.Count > 0)
				position = node.PosKeyFrames[0].Position;
			if (node.RotationKeyFrames.Count > 0)
				rotation = node.RotationKeyFrames[0].Rotation;

			go.transform.localPosition = position.FlipY();
			go.transform.localRotation = rotation;
			go.transform.localScale = scale;

			var vTrans = new List<Vector3>();
			var tris = new List<RsmTriangle>();

			foreach (var f in node.Faces)
			{
				var tri = new RsmTriangle() { TwoSided = f.TwoSided };

				var realTexId = node.TextureIds[f.TextureId];

				for (var i = 0; i < 3; i++)
				{
					var v = node.Vertices[f.VertexIds[i]].FlipY() + offset.FlipY();
					vTrans.Add(go.transform.TransformPoint(v));
					tri.Vertices[i] = v;
					tri.UVs[i] = VectorHelper.RemapUV(node.UVs[f.UVIds[i]], atlasRects[realTexId]);
					tri.Colors[i] = node.Colors[f.UVIds[i]];
				}

				tri.CalcNormals();
				tris.Add(tri);
			}

			if (model.ShadingType == RsmShadingType.Smooth)
				SmoothNodeTriangles(node, tris);

			if (vTrans.Count > 0)
				node.Bounds = GeometryUtility.CalculateBounds(vTrans.ToArray(), Matrix4x4.identity);
			else
				node.Bounds = new Bounds(Vector3.zero, Vector3.zero);

			var mb = new MeshBuilder();

			foreach (var t in tris)
			{
				mb.AddFullTriangle(t.Vertices, t.FlippedNormals, t.UVs, t.Colors, new[] { 2, 1, 0 });

				if (t.TwoSided)
					mb.AddFullTriangle(t.Vertices, t.Normals, t.UVs, t.Colors, new[] { 0, 1, 2 });
			}

			var mesh = mb.Build(node.Name, true);
			mf.sharedMesh = mesh;
			mc.sharedMesh = mesh;
			if (tris.Any(t => t.TwoSided))
				mr.shadowCastingMode = ShadowCastingMode.On; //disable two sided shadows if the model has any two sided faces

			if (node.Children.Count > 0)
			{
				foreach (var child in node.Children)
				{
					CompileNode(go, child, mat);
				}
			}

			if (node.RotationKeyFrames.Count != 0 || node.PosKeyFrames.Count != 0)
				go.ChangeStaticRecursive(false);

			if (string.IsNullOrWhiteSpace(node.ParentName))
			{
				var bounds = node.Bounds;
				foreach (var n in model.RsmNodes)
					bounds.Encapsulate(n.Bounds);

				var centering = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
				//Debug.Log(centering);
				go.transform.localPosition -= centering;
				go.transform.localPosition *= 0.2f;
				go.transform.localScale *= 0.2f;
			}

			if (node.RotationKeyFrames.Count > 0)
			{
				var r = go.AddComponent<RoKeyframeRotator>();
				var keyframes = new List<float>();
				var rotations = new List<Quaternion>();
				foreach (var k in node.RotationKeyFrames)
				{
					keyframes.Add(k.Frame / 1000f);
					rotations.Add(k.Rotation);
				}

				r.Keyframes = keyframes.ToArray();
				r.Rotations = rotations.ToArray();
			}

			//save Mesh
			var meshPath = AssetHelper.GetAssetPath(Path.Combine("Assets/models/mesh", savePath, baseName), Path.GetFileNameWithoutExtension(node.Name) + ".asset");

			AssetDatabase.CreateAsset(mesh, meshPath);

			return go;
		}

		private Material CreateMaterial()
		{
			var mat = new Material(Shader.Find("Custom/ObjectShader"));
			mat.mainTexture = atlas;
			mat.SetFloat("_Glossiness", 0);
			mat.SetColor("_SpecColor", new Color(0, 0, 0, 1));
			mat.SetFloat("_Mode", 1);

			mat.SetOverrideTag("RenderType", "TransparentCutout");
			mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
			mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
			mat.SetInt("_ZWrite", 1);
			mat.EnableKeyword("_ALPHATEST_ON");
			mat.DisableKeyword("_ALPHABLEND_ON");
			mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
			mat.renderQueue = (int)RenderQueue.AlphaTest;

			mat.doubleSidedGI = true;
			mat.enableInstancing = true;

			var matPath = AssetHelper.GetAssetPath(Path.Combine("Assets/models/materials", savePath), Path.GetFileNameWithoutExtension(baseName) + ".mat");

			AssetDatabase.CreateAsset(mat, matPath);

			return mat;
		}

		public GameObject Compile()
		{
			var mat = CreateMaterial();

			var go = new GameObject(model.Name);
			go.isStatic = true;
			//go.transform.localScale = new Vector3(1f, -1f, 1f);
			//go.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);

			CompileNode(go, model.RootNode, mat);

			return go;
		}

		public void LoadModel(string path, string subPath)
		{
			var filename = path;
			var basedir = Path.GetDirectoryName(path);

			//Debug.Log(filename);

			savePath = subPath;
			baseName = Path.GetFileNameWithoutExtension(filename);

			fs = new FileStream(filename, FileMode.Open);
			br = new BinaryReader(fs);

            try
            {

                var header = new string(br.ReadChars(4));
                if (header != "GRSM")
                    throw new Exception("Not model file");

                model = new RsmModel();

                model.Name = baseName;

                var majorVersion = br.ReadByte();
                var minorVersion = br.ReadByte();
                model.Version = majorVersion * 10 + minorVersion;

                var animLen = br.ReadInt32();
                model.ShadingType = (RsmShadingType)br.ReadInt32();

                model.Alpha = 1f;
                if (model.Version >= 14)
                    model.Alpha = br.ReadByte() / 255f;

                fs.Seek(16, SeekOrigin.Current);

                LoadTextures(savePath);

                model.Name = br.ReadKoreanString(40);

                var nodeCount = br.ReadInt32();

                model.RsmNodes = new List<RsmNode>();

                for (var i = 0; i < nodeCount; i++)
                {
                    var node = LoadNode();
                    model.RsmNodes.Add(node);
                    if (node.Name == model.Name)
                        model.RootNode = node;
                }

                if (model.RootNode == null)
                    model.RootNode = model.RsmNodes[0];

                if (model.Version <= 15)
                {
                    var posKeyFrames = br.ReadInt32();

                    for (var i = 0; i < posKeyFrames; i++)
                    {
                        model.PosKeyFrames.Add(new RsmPosKeyframe()
                            { Frame = br.ReadInt32(), Position = br.ReadRoPosition() });
                    }
                }

                var volumeBoxes = br.ReadInt32();
                for (var i = 0; i < volumeBoxes; i++)
                {
                    model.VolumeBoxes.Add(new RsmVolumeBox
                    {
                        Scale = br.ReadVector3(), Position = br.ReadRoPosition(), Rotation = br.ReadVector3(),
                        Flag = (model.Version >= 13 ? br.ReadInt32() : 0)
                    });
                }

                for (var i = 0; i < model.RsmNodes.Count; i++)
                {
                    var node = model.RsmNodes[i];
                    if (string.IsNullOrWhiteSpace(node.ParentName) || node.ParentName == node.Name)
                        continue;

                    var parent = model.RsmNodes.FirstOrDefault(n => n.Name == node.ParentName);
                    if (parent != null)
                    {
                        parent.Children.Add(node);
                        node.Parent = parent;
                    }
                }

                if (model.PosKeyFrames.Count > 0)
                {
                    EditorApplication.Beep();
                    Debug.LogWarning("POSITIONAL KEYFRAMES");
                }


                //var jsonData = SerializationUtility.SerializeValue(model, DataFormat.JSON);
                //File.WriteAllBytes(@"G:\Projects2\test.txt", jsonData);

                //Debug.Log("Done!");
            }
            catch (Exception e)
            {
				Debug.LogError($"Could not load model {Path.GetFileName(filename)} due to exception: {e}");
                throw;
            }
            finally
            {
                fs.Close();
			}
		}

		public void Dispose()
		{
			fs?.Dispose();
			br?.Dispose();
		}

		public static void LoadModelTest()
		{
			var loader = new RagnarokModelLoader();
			try
			{
				var go = GameObject.Find("ModelTest");
				if (go != null)
					GameObject.DestroyImmediate(go);
				go = new GameObject("ModelTest");

				//var modelPath = @"G:\Projects2\Ragnarok\Resources\data\model\게페니아\대장간.rsm"; //Retarded hammer building
				//var modelPath = @"G:\Projects2\Ragnarok\Resources\data\model\유노\유노_나무3.rsm"; //Juno tree
				//var modelPath = @"G:\Projects2\Ragnarok\Resources\data\model\게페니아\도구점.rsm"; //geffen windmill
				//var modelPath = @"G:\Projects2\Ragnarok\Resources\data\model\유노\유노_과학자건물.rsm"; //alchemist thing?
				//var modelPath = @"G:\Projects2\Ragnarok\Resources\data\model\모로코\개미나무.rsm"; //big ass ant
				//var modelPath = @"G:\Projects2\Ragnarok\Resources\data\model\모로코\개미지옥나무뿌리.rsm"; //ant hell tree thing

				//var modelPath = @"G:\Projects2\Ragnarok\Resources\data\model\니플헤임\니플헤임-무기점.rsm"; //weird skull hatchet house
				//var modelPath = @"G:\Projects2\Ragnarok\Resources\data\model\니플헤임\니플헤임-풍차.rsm"; //niflheim windmill
				//var modelPath = @"G:\Projects2\Ragnarok\Resources\data\model\프론테라\상점01.rsm"; //fruit cart
				//var modelPath = @"G:\Projects2\Ragnarok\Resources\data\model\페이욘\싸리담1.rsm"; //payon fence
				//var modelPath = @"G:\Projects2\Ragnarok\Resources\data\model\알베르타\도구점.rsm"; //alberta pickaxe shop
				var modelPath = @"G:\Projects2\Ragnarok\Resources\data\model\프론테라\무기점.rsm"; //prontera armory




				var savePath = DirectoryHelper.GetRelativeDirectory(RagnarokDirectory.GetRagnarokDataDirectory, Path.GetDirectoryName(modelPath));

				//loader.LoadModel(@"G:\Projects2\Ragnarok\Resources\data\model\글래스트\글래스트_부서진의자1.rsm"); //Glast\Glast_BrokenChair1
				//loader.LoadModel(@"G:\Projects2\Ragnarok\Resources\data\model\내부소품\탁상1.rsm"); //props\desk1
				loader.LoadModel(modelPath, savePath); //Glast\Glast_KnightStatue13


				var model = loader.Compile();

				model.transform.SetParent(go.transform, false);
				model.transform.localScale = new Vector3(1f, 1f, 1f);
			}
			finally
			{
				loader.Dispose(); ;
			}


		}

	}
}
