using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
	public static class SpriteMeshCache
	{
		private static Dictionary<string, Dictionary<int, Mesh>> spriteMeshCache;
		private static Dictionary<string, Dictionary<int, Mesh>> spriteColliderCache;

		public static Dictionary<int, Mesh> GetColliderCacheForSprite(string spriteName)
		{
			if (spriteColliderCache == null)
				spriteColliderCache = new Dictionary<string, Dictionary<int, Mesh>>();

			if (spriteColliderCache.TryGetValue(spriteName, out var meshCache))
				return meshCache;

			//Debug.Log("Making new mesh cache");

			var newCache = new Dictionary<int, Mesh>();
			spriteColliderCache.Add(spriteName, newCache);
			return newCache;
		}

		public static Dictionary<int, Mesh> GetMeshCacheForSprite(string spriteName)
		{
			if(spriteMeshCache == null)
				spriteMeshCache = new Dictionary<string, Dictionary<int, Mesh>>();

			if (spriteMeshCache.TryGetValue(spriteName, out var meshCache)) 
				return meshCache;

			//Debug.Log("Making new mesh cache");

			var newCache = new Dictionary<int, Mesh>();
			spriteMeshCache.Add(spriteName, newCache);
			return newCache;
		}
	}
}