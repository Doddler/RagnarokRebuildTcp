using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
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

		private List<ClientMapEntry> mapEntries;
		private List<FogInfo> mapFogInfo;

		private bool needAudioChange = false;

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
					SceneManager.UnloadSceneAsync(unloadScene);
					finishCallback();
				});
		}

		public void LoadScene(string sceneName, Action onFinish)
		{
			newScene = sceneName;
			finishCallback = onFinish;
			needAudioChange = CheckMapAudioChange(null, sceneName);

			StartTransitionScene();
		}

		private void StartTransitionScene()
		{
			StartCoroutine(BeginTransitionScene());
		}

		private IEnumerator BeginTransitionScene()
		{
			LoadingImage.gameObject.SetActive(true);

			yield return null;
			yield return null;

			TransitionScenes();
		}

		private void TransitionScenes()
		{
			if(unloadScene.IsValid())
				SceneManager.UnloadSceneAsync(unloadScene);

			MonsterHoverText.text = "";
			LightmapSettings.lightProbes = null;
			
			var trans = Addressables.LoadSceneAsync($"Assets/Scenes/Maps/{newScene}.unity", LoadSceneMode.Additive);

			//var trans = SceneManager.LoadSceneAsync(newScene, LoadSceneMode.Additive);
			trans.Completed += FinishSceneChange;
			//trans.completed += FinishSceneChange;
		}

		private void FinishSceneChange(AsyncOperationHandle<SceneInstance> obj)
		{
			finishCallback();
			
            var newMap = mapEntries.FirstOrDefault(m => m.Code == newScene);
            
            LightProbes.Tetrahedralize();

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

			var type = (MapMinimapType)newMap.MapMode;

			if (type == MapMinimapType.None)
				MinimapController.Instance.gameObject.SetActive(false);
			else
				MinimapController.Instance.LoadMinimap(newScene, type);
		}

		private void FinishSceneChange(AsyncOperation op)
		{
			finishCallback();
		}

		private IEnumerator WaitAndStartFade()
		{
			Resources.UnloadUnusedAssets();

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
