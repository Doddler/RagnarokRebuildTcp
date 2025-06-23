using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Effects;
using Assets.Scripts.MapEditor;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI;
using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.UI.Hud;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Scripts.Utility
{
	class SceneTransitioner : MonoBehaviour
	{
		public static SceneTransitioner Instance;

		public RawImage BlackoutImage;
		public Image LoadingImage;

		public TextMeshProUGUI MonsterHoverText;

		public TextAsset MapList;
		public TextAsset FogData;
		
		private Scene unloadScene;
		private string newScene;
		private Action finishCallback;

		private static List<ClientMapEntry> mapEntries;
		private static List<FogInfo> mapFogInfo;
		
		private bool needAudioChange = false;

		public List<ClientMapEntry> GetMapEntries()
		{
			if(mapEntries == null)
				LoadMaps();
			return mapEntries;
		}

		public void Start()
		{
			Instance = this;
			BlackoutImage.gameObject.SetActive(true);
			LoadingImage.gameObject.SetActive(true);
			
			var json = JsonUtility.FromJson<Wrapper<FogInfo>>(FogData.text);
			mapFogInfo = json.Items.ToList();
        }

		private void LoadMaps()
		{
			var maps = JsonUtility.FromJson<Wrapper<ClientMapEntry>>(MapList.text);
			mapEntries = maps.Items.ToList();
		}

		private bool CheckMapAudioChange(string curScene, string newScene)
		{
			if(mapEntries == null)
				LoadMaps();

			var curMap = mapEntries.FirstOrDefault(m => m.Code == curScene);
			var newMap = mapEntries.FirstOrDefault(m => m.Code == newScene);

			//Debug.Log(curMap?.Code + " vs " + newMap?.Code);

			if (newMap == null || string.IsNullOrWhiteSpace(newMap.Music))
			{
				AudioManager.Instance.FadeOutCurrentBgm();
				return false;
			}

			if (curMap == null)
				return true;

			return curMap.Music != newMap.Music;
		}

		public void DoTransitionToScene(Scene currentScene, string sceneName, Action onFinish)
		{
			if(mapEntries == null)
				LoadMaps();

			needAudioChange = CheckMapAudioChange(currentScene.name, sceneName);
			if(needAudioChange)
				AudioManager.Instance.FadeOutCurrentBgm();

			unloadScene = currentScene;
			newScene = sceneName;
			finishCallback = onFinish;

			if(currentScene.name != sceneName)
				BlackoutImage.gameObject.SetActive(true);

			UpdateAlpha(0);

			var tween = LeanTween.value(gameObject, UpdateAlpha, 0, 1, 0.3f);
			if (currentScene.name != sceneName)
				tween.setOnComplete(StartTransitionScene);
			else
				tween.setOnComplete(() => { 
					Shader.DisableKeyword("BLINDEFFECT_ON");
					SceneManager.UnloadSceneAsync(unloadScene);
					finishCallback();
				});
		}

		public void LoadScene(string sceneName, Action onFinish)
		{
			newScene = sceneName;
			finishCallback = onFinish;
			needAudioChange = CheckMapAudioChange(null, sceneName);

			BlackoutImage.gameObject.SetActive(true);
			var tween = LeanTween.value(gameObject, UpdateAlpha, 0, 1, 0.3f);
			tween.setOnComplete(StartTransitionScene);
			
			//StartTransitionScene();
		}

		private void StartTransitionScene()
		{
			StartCoroutine(BeginTransitionScene());
		}

		private void FixOcclusionCulling(Scene scene, LoadSceneMode mode)
		{
			Debug.Log($"{scene.name} {newScene}");
			if (scene.name == newScene)
				SceneManager.SetActiveScene(scene);
		}

		private IEnumerator BeginTransitionScene()
		{
			if (NetworkManager.Instance.TitleScreen != null)
			{
				Destroy(NetworkManager.Instance.TitleScreen.gameObject); //we won't need this until we implement return to character select.
				CameraFollower.Instance.UpdateCameraSize(); //we scale the login screen larger than the main game UI
			}

			//NetworkManager.Instance.TitleScreen?.gameObject.SetActive(false);
			LoadingImage.gameObject.SetActive(true);
			RoMapRenderSettings.ClearBakedLightmap();
			GameConfig.SaveConfig();

			yield return null;
			yield return null;
			
			if(unloadScene.IsValid())
				SceneManager.UnloadSceneAsync(unloadScene);
			
			RagnarokEffectData.SceneChangeCleanup();
			EffectSharedMaterialManager.CleanUpMaterialsOnSceneChange();

			MonsterHoverText.text = "";
			LightmapSettings.lightProbes = null;
			MinimapController.Instance.RemoveAllEntities();

			var sceneName = $"Assets/Scenes/Maps/{newScene}.unity";

			SceneManager.sceneLoaded += FixOcclusionCulling;

			var trans = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
			
			while (!trans.IsDone)
			{
				NetworkManager.Instance.LoadingText.text = $"Map {trans.PercentComplete*100:N0}%";
				yield return 0;
			}
			
			yield return trans;

			SceneManager.sceneLoaded -= FixOcclusionCulling;

			FinishSceneChange();
		}

		private void FinishSceneChange()
		{
			finishCallback();
			
			UiManager.Instance.SetEnabled(true);
			
            var newMap = mapEntries.FirstOrDefault(m => m.Code == newScene);
            
            LightProbes.Tetrahedralize();
            UiManager.Instance.ConfigManager.RefreshAudioLevels(); //why unity does doing this in onAwake do literally nothing?
            UiManager.Instance.StatusWindow.ResetStatChanges(); //clear existing changes

            PlayerState.Instance.MapName = newMap.Code;
            UiManager.Instance.PartyPanel.OnChangeMaps();
            UiManager.Instance.EmoteManager.GetComponent<EmoteWindow>()?.EnsureInitialized();

			if (needAudioChange)
			{
				AudioManager.Instance.PlayBgm(newMap.Music);
			}
			
			var fog = mapFogInfo.FirstOrDefault(m => m.Map == newScene);
			if (fog != null)
			{
				CameraFollower.Instance.FogNearRatio = fog.NearPlane * 1500f / 400f;
				CameraFollower.Instance.FogFarRatio = fog.FarPlane * 1500f / 400f;
			}
			else
			{
				CameraFollower.Instance.FogNearRatio = 0.1f * 1500f / 400f;
				CameraFollower.Instance.FogFarRatio = 0.9f * 1500f / 400f;
			}

			var type = (MapType)newMap.MapMode;

			if (type == MapType.None || type == MapType.Indoor)
				MinimapController.Instance.gameObject.SetActive(false);
			else
				MinimapController.Instance.LoadMinimap(newScene, type);

			if (newMap.Code == "yuno")
				CameraFollower.Instance.Camera.backgroundColor = new Color(0.6352f, 0.8039f, 0.9882f);
			else
				CameraFollower.Instance.Camera.backgroundColor = Color.black;

			var viewpoint = ClientDataLoader.Instance.GetMapViewpoint(newMap.Code);
			if (viewpoint != null)
			{
				CameraFollower.Instance.SetCameraViewpoint(viewpoint);
			}
			else
			{
				if (type == MapType.Indoors)
					CameraFollower.Instance.SetCameraMode(CameraMode.Indoor);
				else
					CameraFollower.Instance.SetCameraMode(CameraMode.Normal);
			}
			
			CameraFollower.Instance.ResetCursor();
		}

		// private void FinishSceneChange(AsyncOperation op)
		// {
		// 	finishCallback();
		// }

		private IEnumerator WaitAndStartFade()
		{
			EffectPool.ClearPrimitiveDataPools();
			Resources.UnloadUnusedAssets();
			System.GC.Collect();

			yield return null;
			yield return null;

			var tween = LeanTween.value(gameObject, UpdateAlpha, 1, 0, 0.3f);
			tween.setOnComplete(FinishHide);
		}

		public void FadeIn()
		{
			LoadingImage.gameObject.SetActive(false);
			BlackoutImage.gameObject.SetActive(true);
			UpdateAlpha(1);
			StartCoroutine(WaitAndStartFade());
		}

		private void UpdateAlpha(float f)
		{
			BlackoutImage.color = new Color(1, 1, 1, f);
		}

		private void FinishHide()
		{
			BlackoutImage.gameObject.SetActive(false);
		}
	}
}
