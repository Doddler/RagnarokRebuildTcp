using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Assets.Scripts.Utility
{
	public static class AddressableUtility
	{
		public static void LoadRoSpriteData(GameObject owner, string spritePath, Action<RoSpriteData> onComplete)
		{
			Addressables.LoadAssetAsync<RoSpriteData>(spritePath).Completed += handle =>
			{
				if (owner != null)
					onComplete(handle.Result);
			};
		}

		public static void LoadSprite(GameObject owner, string spritePath, Action<Sprite> onComplete)
		{
			Addressables.LoadAssetAsync<Sprite>(spritePath).Completed += handle =>
			{
				if (owner != null)
					onComplete(handle.Result);
			};
		}

		public static void Load<T>(GameObject owner, string fileName, Action<T> onComplete)
		{
			Addressables.LoadAssetAsync<T>(fileName).Completed += handle =>
			{
				if (owner != null)
					onComplete(handle.Result);
			};
		}
	}
}
