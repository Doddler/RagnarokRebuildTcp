using System;
using Assets.Scripts.Utility;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Assets.Scripts.Objects
{
    public class AudioPlayer : MonoBehaviour
    {
        public AudioManager AudioManager;
        public int ChannelId;

        private AudioSource audioSource;
        
        private GameObject followTarget;
        private bool hasFollowTarget;
        
        private bool isLoading;
        private bool isInUse;
        private AsyncOperationHandle<AudioClip> loadHandle;

        private float endTime;

        public void Init(AudioManager manager, int id)
        {
            AudioManager = manager;
            ChannelId = id;

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0.5f;
            audioSource.priority = 80;
            audioSource.maxDistance = 50;
            audioSource.rolloffMode = AudioRolloffMode.Custom;
            audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AudioManager.Instance.FalloffCurve);
            audioSource.volume = 1f;
            audioSource.dopplerLevel = 0;
            audioSource.outputAudioMixerGroup = manager.Mixer.FindMatchingGroups("Sounds")[0];;
            
            gameObject.transform.SetParent(AudioManager.gameObject.transform);
        }
        
        public bool PlayAudioClip(AudioClip clip, GameObject attachTarget, float volume)
        {
            isInUse = true;
            isLoading = false;
            gameObject.SetActive(true);

            hasFollowTarget = true;
            followTarget = attachTarget;

            audioSource.volume = volume;
            audioSource.clip = clip;
            audioSource.Play();
            endTime = Time.fixedTime + clip.length;
            
            return audioSource.isPlaying;
        }

        public bool PlayAudioClip(AudioClip clip, Vector3 position, float volume)
        {
            //AudioManager.Mixer.GetFloat("Sounds", out var db);
            //var targetVolume = Mathf.Pow(10f, db / 20f) * volume;
            isInUse = true;
            isLoading = false;
            gameObject.SetActive(true);
            
            gameObject.transform.localPosition = position;
            
            audioSource.volume = volume;
            audioSource.clip = clip;
            audioSource.Play();
            endTime = Time.fixedTime + clip.length;
            
            return audioSource.isPlaying;
        }


        public bool PlayAudioClip(string filename, GameObject attachTarget, float volume)
        {
            isInUse = true;
            isLoading = true;
            gameObject.SetActive(true);

            hasFollowTarget = true;
            followTarget = attachTarget;
            
            audioSource.volume = volume;

            loadHandle = AddressableUtility.Load<AudioClip>(gameObject, "Assets/Sounds/Effects/" + filename, OnFinishLoad);
            
            return true;
        }

        public bool PlayAudioClip(string filename, Vector3 position, float volume)
        {
            //AudioManager.Mixer.GetFloat("Sounds", out var db);
            //var targetVolume = Mathf.Pow(10f, db / 20f) * volume;
            isInUse = true;
            isLoading = true;
            gameObject.SetActive(true);
            
            gameObject.transform.localPosition = position;
            
            audioSource.volume = volume;

            loadHandle = AddressableUtility.Load<AudioClip>(gameObject, "Assets/Sounds/Effects/" + filename, OnFinishLoad);
            
            return true;
        }

        private void OnFinishLoad(AudioClip clip)
        {
            if (!isInUse)
                return; //why would this happen?

            audioSource.clip = clip;
            audioSource.Play();
            endTime = Time.fixedTime + clip.length;
        }

        public void Update()
        {
            if (!isInUse)
            {
                gameObject.SetActive(false);
                return;
            }

            if (hasFollowTarget && followTarget != null)
                transform.localPosition = followTarget.transform.position;

            if (isLoading)
            {
                if (loadHandle.Status == AsyncOperationStatus.Failed)
                {
                    AudioManager.MarkAudioChannelAsFree(ChannelId);
                    throw new Exception($"Failed to load audio clip!", loadHandle.OperationException);
                }
                
                if(loadHandle.Status == AsyncOperationStatus.Succeeded)
                    isLoading = false;

                return;
            }
            
            if (!isLoading && Time.fixedTime > endTime)
            {
                audioSource.Stop();
                isInUse = false;
                followTarget = null;
                hasFollowTarget = false;
                gameObject.SetActive(false);
                AudioManager.MarkAudioChannelAsFree(ChannelId);
            }
            
        }
    }
}