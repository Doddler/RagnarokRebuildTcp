using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
	public static class SpriteMeshBuilder
	{
		private static List<Vector3> outVertices = new List<Vector3>(512);
		private static List<Vector3> outNormals = new List<Vector3>(512);
		private static List<int> outTris = new List<int>(1024);
		private static List<Vector2> outUvs = new List<Vector2>(512);
		
		private static List<Color> outColors = new List<Color>(512);

		private static int meshBuildCount = 0;

		public static Mesh BuildColliderMesh(RoSpriteData spriteData, int currentActionIndex, int currentAngleIndex,
			int currentFrame)
		{
			var frame = spriteData.Actions[currentActionIndex + currentAngleIndex].Frames[currentFrame];

			meshBuildCount++;
			//Debug.Log("Building new mesh, current mesh count: " + meshBuildCount);

			outNormals.Clear();
			outVertices.Clear();
			outTris.Clear();
			outUvs.Clear();
			outColors.Clear();

			var mesh = new Mesh();

			var tIndex = 0;

			var min = new Vector2(-0.2f, -0.2f);
			var max = new Vector2(0.2f, 0.2f);

			for (var i = 0; i < frame.Layers.Length; i++)
			{
				var layer = frame.Layers[i];

				if (layer.Index < 0)
					continue;
				var sprite = spriteData.Sprites[layer.Index];
				var verts = sprite.vertices;
				var uvs = sprite.uv;

				var rotation = Quaternion.Euler(0, 0, -layer.Angle);
				var scale = new Vector3(layer.Scale.x * (layer.IsMirror ? -1 : 1), layer.Scale.y, 1);

				var offsetX = (Mathf.RoundToInt(sprite.rect.width) % 2 == 1) ? 0.5f : 0f;
				var offsetY = (Mathf.RoundToInt(sprite.rect.height) % 2 == 1) ? 0.5f : 0f;

				for (var j = 0; j < verts.Length; j++)
				{
					var v = rotation * (verts[j] * scale);
					var vert = v + new Vector3(layer.Position.x - offsetX, -(layer.Position.y) + offsetY) / 50f;

					if (min.x > vert.x)
						min.x = vert.x;
					if (min.y > vert.y)
						min.y = vert.y;

					if (max.x < vert.x)
						max.x = vert.x;
					if (max.y < vert.y)
						max.y = vert.y;
				}
			}

			var xSize = max.x - min.x;
			var ySize = max.y - min.y;
			var xBoost = 0.1f;
			var yBoost = 0.1f;

			//Debug.Log(xSize + " " + ySize);

			if (xSize < 0.5f)
				xBoost += 0.2f;
			if (xSize < 1f)
				xBoost += 0.1f;


			if (ySize < 0.5f)
				yBoost += 0.2f;
			if (ySize < 1f)
				yBoost += 0.1f;


			min -= new Vector2(xBoost, yBoost);
			max += new Vector2(xBoost, yBoost);

			outVertices.Add(new Vector3(min.x, max.y));
			outVertices.Add(new Vector3(max.x, max.y));
			outVertices.Add(new Vector3(min.x, min.y));
			outVertices.Add(new Vector3(max.x, min.y));

			outTris.Add(tIndex);
			outTris.Add(tIndex + 1);
			outTris.Add(tIndex + 2);
			outTris.Add(tIndex + 1);
			outTris.Add(tIndex + 3);
			outTris.Add(tIndex + 2);


			//Debug.Log($"{outVertices.Count} {outColors.Count}");

			mesh.vertices = outVertices.ToArray();
			//mesh.uv = outUvs.ToArray();
			mesh.triangles = outTris.ToArray();
			//mesh.colors = outColors.ToArray();
			//mesh.normals = outNormals.ToArray();

			mesh.Optimize();

			return mesh;
		}

		public static Mesh BuildSpriteMesh(RoSpriteData spriteData, int currentActionIndex, int currentAngleIndex, int currentFrame)
		{
			var frame = spriteData.Actions[currentActionIndex + currentAngleIndex].Frames[currentFrame];

			meshBuildCount++;
			//Debug.Log("Building new mesh, current mesh count: " + meshBuildCount);

			outNormals.Clear();
			outVertices.Clear();
			outTris.Clear();
			outUvs.Clear();
			outColors.Clear();

			var mesh = new Mesh();

			var tIndex = 0;

			for (var i = 0; i < frame.Layers.Length; i++)
			{
				var layer = frame.Layers[i];

				if (layer.Index < 0)
					continue;
				var sprite = spriteData.Sprites[layer.Index];
				var verts = sprite.vertices;
				var uvs = sprite.uv;

				var rotation = Quaternion.Euler(0, 0, -layer.Angle);
				var scale = new Vector3(layer.Scale.x * (layer.IsMirror ? -1 : 1), layer.Scale.y, 1);

				var offsetX = (Mathf.RoundToInt(sprite.rect.width) % 2 == 1) ? 0.5f : 0f;
				var offsetY = (Mathf.RoundToInt(sprite.rect.height) % 2 == 1) ? 0.5f : 0f;

				for (var j = 0; j < verts.Length; j++)
				{
					var v = rotation * (verts[j] * scale);
					outVertices.Add(v + new Vector3(layer.Position.x - offsetX, -(layer.Position.y) + offsetY) / 50f);
					outUvs.Add(uvs[j]);
					outColors.Add(layer.Color);
					outNormals.Add(new Vector3(0, 0, -1));
				}

				if (layer.IsMirror)
				{
					outTris.Add(tIndex + 2);
					outTris.Add(tIndex + 1);
					outTris.Add(tIndex);
					outTris.Add(tIndex + 2);
					outTris.Add(tIndex + 3);
					outTris.Add(tIndex + 1);
				}
				else
				{
					outTris.Add(tIndex);
					outTris.Add(tIndex + 1);
					outTris.Add(tIndex + 2);
					outTris.Add(tIndex + 1);
					outTris.Add(tIndex + 3);
					outTris.Add(tIndex + 2);
				}


				tIndex += 4;
			}

			//Debug.Log($"{outVertices.Count} {outColors.Count}");

			mesh.vertices = outVertices.ToArray();
			mesh.uv = outUvs.ToArray();
			mesh.triangles = outTris.ToArray();
			mesh.colors = outColors.ToArray();
			mesh.normals = outNormals.ToArray();

			mesh.Optimize();

			return mesh;
		}
	}
}
