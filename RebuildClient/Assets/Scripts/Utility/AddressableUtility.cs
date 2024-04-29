using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Assets.Scripts.Utility
{
	public static class AddressableUtility
	{
		public static void LoadRoSpriteData(GameObject owner, string spritePath, Action<RoSpriteData> onComplete)
        {
            if (string.IsNullOrWhiteSpace(spritePath))
                throw new Exception($"Attempting to load RoSpriteData but the spritePath was empty!");

			var load = Addressables.LoadAssetAsync<RoSpriteData>(spritePath);
			load.Completed += handle =>
			{
				if(handle.Status != AsyncOperationStatus.Succeeded)
					Debug.LogError("Could not load sprite name " + spritePath);
				if (owner != null)
					onComplete(handle.Result);
			};
			
		}

		public static void LoadSprite(GameObject owner, string spritePath, Action<Sprite> onComplete)
		{
            if (string.IsNullOrWhiteSpace(spritePath))
                throw new Exception($"Attempting to load Sprite but the spritePath was empty!");

            Addressables.LoadAssetAsync<Sprite>(spritePath).Completed += handle =>
			{
                if (handle.Status != AsyncOperationStatus.Succeeded)
                    Debug.LogError("Could not load sprite name " + spritePath);
				if (owner != null)
					onComplete(handle.Result);
			};
		}

		public static AsyncOperationHandle<T> Load<T>(GameObject owner, string fileName, Action<T> onComplete)
		{
            if (string.IsNullOrWhiteSpace(fileName))
                throw new Exception($"Attempting to load type {typeof(T)} but the fileName was empty!");

            var asyncOp = Addressables.LoadAssetAsync<T>(fileName);
            asyncOp.Completed += handle =>
			{
				if (owner != null)
					onComplete(handle.Result);
			};

            return asyncOp;
		}
	}
}
