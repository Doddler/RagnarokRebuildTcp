using System.Collections.Generic;
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

        public static Mesh BuildColliderMesh(RoSpriteData spriteData, int currentActionIndex, int currentAngleIndex, int currentFrame) 
            => BuildColliderMesh(spriteData, currentActionIndex + currentAngleIndex, currentFrame);
		
        public static Mesh BuildColliderMesh(RoSpriteData spriteData, int currentActionIndex, int currentFrame)
        {
	        currentActionIndex = Mathf.Clamp(currentActionIndex, 0, spriteData.Actions.Length - 1);
	        
			//this can sometimes happen if it gets rotated to an angle that doesn't have a matching frame count
            var maxFrames = spriteData.Actions[currentActionIndex].Frames.Length;
			if (currentFrame >= maxFrames)
				currentFrame = maxFrames - 1;

			var frame = spriteData.Actions[currentActionIndex].Frames[currentFrame];

			meshBuildCount++;

			outNormals.Clear();
			outVertices.Clear();
			outTris.Clear();
			outUvs.Clear();
			outColors.Clear();

			var mesh = new Mesh();

			var tIndex = 0;

			var min = new Vector2(-0.4f, -0f);
			var max = new Vector2(0.4f, 0.4f);

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
			
			if (min.y < -0.3f)
				min.y = -0.3f;
			if (max.y < min.y)
				max.y = min.y;
			
			// Debug.Log($"{spriteData.Name} Action:{currentActionIndex} Frame:{currentFrame} Min:{min} Max:{max}");
			
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

        public static Mesh BuildSpriteMesh(RoSpriteData spriteData, int currentActionIndex, int currentAngleIndex, int currentFrame, float alpha = 1, float vOffset = 0f, int paletteId = 0) 
            => BuildSpriteMesh(spriteData, currentActionIndex + currentAngleIndex, currentFrame, alpha, vOffset, paletteId);
        
		
        public static Mesh BuildSpriteMesh(RoSpriteData spriteData, int currentActionIndex, int currentFrame, float alpha = 1, float vOffset = 0f, int paletteId = 0)
        {
	        currentActionIndex = Mathf.Clamp(currentActionIndex, 0, spriteData.Actions.Length - 1);
            var actions = spriteData.Actions[currentActionIndex];

            if (currentFrame >= actions.Frames.Length)
            {
				//this can happen if the camera rotates to an animation with less frames of animation. If that happens, just use the last frame..
                currentFrame = actions.Frames.Length - 1;
            }

            var frame = actions.Frames[currentFrame];

			meshBuildCount++;
			//Debug.Log("Building new mesh, current mesh count: " + meshBuildCount);

			outNormals.Clear();
			outVertices.Clear();
			outTris.Clear();
			outUvs.Clear();
			outColors.Clear();

			var mesh = new Mesh();

			var tIndex = 0;

			var maxX = 0f;
			var maxY = 0f;
			var layersPerPalette = spriteData.Sprites.Length / spriteData.SpritesPerPalette;
			if (paletteId * layersPerPalette > spriteData.Sprites.Length)
				paletteId = 0;

			for (var i = 0; i < frame.Layers.Length; i++)
			{
				var layer = frame.Layers[i];

				if (layer.Index < 0)
					continue;
				var sprite = spriteData.Sprites[layersPerPalette * paletteId + layer.Index];
				var verts = sprite.vertices;
				var uvs = sprite.uv;

				var rotation = Quaternion.Euler(0, 0, -layer.Angle);
				
				var scale = new Vector3(layer.Scale.x, layer.Scale.y, 1);

				var offsetX = (Mathf.RoundToInt(sprite.rect.width) % 2 == 1) ? 0.5f : 0f;
				var offsetY = (Mathf.RoundToInt(sprite.rect.height) % 2 == 1) ? 0.5f : 0f;
				maxX = Mathf.Max(maxX, sprite.rect.width);
				maxY = Mathf.Max(maxY, sprite.rect.height);
				
				float minU = float.PositiveInfinity, maxU = float.NegativeInfinity;
				if (layer.IsMirror)
				{
					for (int k = 0; k < uvs.Length; k++)
					{
						float u = uvs[k].x;
						if (u < minU) minU = u;
						if (u > maxU) maxU = u;
					}
				}

				for (var j = 0; j < verts.Length; j++)
				{
					var v3 = rotation * (verts[j] * scale);
					v3 += new Vector3(0, vOffset, 0);
					outVertices.Add(v3 + new Vector3(layer.Position.x - offsetX, -(layer.Position.y) + offsetY) / 50f);
					
					Vector2 uv = uvs[j];
					if (layer.IsMirror)
					{
						uv.x = (minU + maxU) - uv.x;
					}

					outUvs.Add(uv);

					var c = new Color(layer.Color.r, layer.Color.g, layer.Color.b, layer.Color.a * alpha);
					outColors.Add(c);
					outNormals.Add(new Vector3(0, 0, -1));
				}
				
				outTris.Add(tIndex);
				outTris.Add(tIndex + 1);
				outTris.Add(tIndex + 2);
				outTris.Add(tIndex + 1);
				outTris.Add(tIndex + 3);
				outTris.Add(tIndex + 2);

				tIndex += 4;
			}

			mesh.bounds = new Bounds(Vector3.zero, new Vector3(maxX, maxY, Mathf.Max(maxX, maxY)));

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
