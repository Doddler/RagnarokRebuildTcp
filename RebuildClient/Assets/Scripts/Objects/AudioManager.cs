using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts.Utility;
using UnityEngine;
using UnityEngine.Audio;

namespace Assets.Scripts.Objects
{
	class AudioManager : MonoBehaviour
	{
		private static AudioManager instance;

		public static AudioManager Instance
		{
			get
			{
				if (instance == null)
					instance = GameObject.FindObjectOfType<AudioManager>();
				return instance;
			}
		}

		public TextAsset MapInfo;
		public AudioMixer Mixer;
		private float bgmLevel;
		private bool muteBgm;

		private AudioMixerGroup musicGroup;

		private AudioSource[] channels;
		private int curChannel;
		


		public void Awake()
		{
			channels = new AudioSource[2];
			musicGroup = Mixer.FindMatchingGroups("Music")[0];
			Mixer.GetFloat("Music", out bgmLevel);

			for (var i = 0; i < 2; i++)
			{
				var go = new GameObject("Channel " + (i + 1));
				go.transform.SetParent(gameObject.transform);
				channels[i] = go.AddComponent<AudioSource>();
				channels[i].outputAudioMixerGroup = musicGroup;
				channels[i].priority = 1;
			}
		}

		private void FadeOutCurrentChannel()
		{
			var channel = curChannel; //capture channel
			var go = channels[channel].gameObject;
			LeanTween.cancel(go);
			var lt = LeanTween.value(go, f => channels[channel].volume = f, 1, 0, 0.3f);
			lt.setOnComplete(() => channels[channel].Stop());
		}

		private void OnLoad(AudioClip clip)
		{
			channels[curChannel].clip = clip;
			channels[curChannel].volume = 1f;
			channels[curChannel].loop = true;
			channels[curChannel].Play();
		}

		public void FadeOutCurrentBgm()
		{
			if (channels[curChannel].isPlaying)
			{
				FadeOutCurrentChannel();
				curChannel++;
				if (curChannel > 1)
					curChannel = 0;
			}
		}

		public void PlayBgm(string name)
		{
			AddressableUtility.Load<AudioClip>(gameObject, "Assets/Music/" + name, OnLoad);
		}

		public void Update()
		{

			if (Input.GetKeyDown(KeyCode.M))
			{
				if (muteBgm)
					Mixer.SetFloat("Music", bgmLevel);
				else
					Mixer.SetFloat("Music", -80);
				muteBgm = !muteBgm;
			}
		}
	}
}
