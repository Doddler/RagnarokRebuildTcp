using Assets.Scripts.Utility;
using UnityEngine;
using UnityEngine.Audio;
using Utility;

namespace Assets.Scripts.Objects
{
    public class AudioManager : MonoBehaviorSingleton<AudioManager>
    {
        public TextAsset MapInfo;
        public AudioMixer Mixer;
        public AnimationCurve FalloffCurve;
        private float bgmLevel;
        private bool muteBgm;

        private AudioMixerGroup musicGroup;
        private AudioMixerGroup soundGroup;

        private AudioSource[] bgmChannels;
        private AudioSource[] uiChannels;
        private int curBgmChannel;

        public struct ChannelUsage
        {
            public int OwnerId;
            public string Filename;
        }

        //Channels 0-19: Anyone can use, first come, first served
        //Channels 20-29: Will play if the owner is not currently playing that clip
        //Channels 30-39: Will play if no one is currently playing that clip
        //Channels 40-47: Reserved for environmental sounds, UI, and BGM

        public int LastFreeUseChannel = 19;
        public int LastLimitedChannel = 29;
        public int LastEntityChannel = 39;

        private AudioPlayer[] entityChannels;
        private bool[] entityChannelInUse;
        private ChannelUsage[] channelUsageInfo;

        public void Awake()
        {
            bgmChannels = new AudioSource[2];
            uiChannels = new AudioSource[2];
            musicGroup = Mixer.FindMatchingGroups("Music")[0];
            soundGroup = Mixer.FindMatchingGroups("Sounds")[0];
            Mixer.GetFloat("Music", out bgmLevel);

            for (var i = 0; i < 2; i++)
            {
                var go = new GameObject("BgmChannel " + (i + 1));
                go.transform.SetParent(gameObject.transform);
                bgmChannels[i] = go.AddComponent<AudioSource>();
                bgmChannels[i].outputAudioMixerGroup = musicGroup;
                bgmChannels[i].priority = 1;
            }

            for (var i = 0; i < 2; i++)
            {
                var go = new GameObject("UIChannel " + (i + 1));
                go.transform.SetParent(gameObject.transform);
                uiChannels[i] = go.AddComponent<AudioSource>();
                uiChannels[i].outputAudioMixerGroup = soundGroup;
                uiChannels[i].priority = 1;
            }
            

            var channelCount = LastEntityChannel + 1;

            entityChannels = new AudioPlayer[channelCount];
            entityChannelInUse = new bool[channelCount];
            channelUsageInfo = new ChannelUsage[channelCount];

            for (var i = 0; i < channelCount; i++)
            {
                var go = new GameObject("EntityAudioChannel " + i.ToString("00"));
                var player = go.AddComponent<AudioPlayer>();
                player.Init(this, i);

                entityChannels[i] = player;
            }
        }

        private void FadeOutCurrentChannel()
        {
            var channel = curBgmChannel; //capture channel
            var go = bgmChannels[channel].gameObject;
            LeanTween.cancel(go);
            var lt = LeanTween.value(go, f => bgmChannels[channel].volume = f, 1, 0, 0.3f);
            lt.setOnComplete(() => bgmChannels[channel].Stop());
        }

        private void OnLoad(AudioClip clip)
        {
            bgmChannels[curBgmChannel].clip = clip;
            bgmChannels[curBgmChannel].volume = 1f;
            bgmChannels[curBgmChannel].loop = true;
            bgmChannels[curBgmChannel].Play();
        }
        
        public void PlaySystemSound(AudioClip clip)
        {
            var curChannel = -1;
            for (var i = 0; i < uiChannels.Length; i++)
            {
                if (!uiChannels[i].isPlaying)
                {
                    curChannel = i;
                    break;
                }
            }

            if (curChannel == -1)
            {
                Debug.Log($"Unable to play sound {clip}, there are no free channels.");
                return;
            }

            uiChannels[curChannel].clip = clip;
            uiChannels[curChannel].volume = 1f;
            uiChannels[curChannel].loop = false;
            uiChannels[curChannel].Play();
        }

        public void FadeOutCurrentBgm()
        {
            if (bgmChannels[curBgmChannel].isPlaying)
            {
                FadeOutCurrentChannel();
                curBgmChannel++;
                if (curBgmChannel > 1)
                    curBgmChannel = 0;
            }
        }

        private int FindFreeAudioChannel(int ownerId, string filename)
        {
            var isEntityPlayingClip = false;
            var isAnyonePlayingClip = false;

            for (var i = 0; i <= LastEntityChannel; i++)
            {
                if (i > LastFreeUseChannel && isEntityPlayingClip)
                {
// #if UNITY_EDITOR
//                     Debug.Log($"Skipping playback of audio clip {filename}, channels are limited and {ownerId} is already playing this clip.");
// #endif
                    return -1;
                }

                if (i > LastLimitedChannel && isAnyonePlayingClip)
                {
// #if UNITY_EDITOR
//                     Debug.Log($"Skipping playback of audio clip {filename}, channels are very limited and someone is already playing this clip.");
// #endif
                    return -1;
                }

                if (entityChannelInUse[i])
                {
                    if (channelUsageInfo[i].Filename == filename)
                    {
                        if (channelUsageInfo[i].OwnerId == ownerId)
                            isEntityPlayingClip = true;
                        else
                            isAnyonePlayingClip = true;
                    }

                    continue;
                }

                return i;
            }

            return -1;
        }
        
        
        public void AttachSoundToEntity(int ownerId, AudioClip clip, GameObject attachTarget, float volume = 1)
        {
            var channel = FindFreeAudioChannel(ownerId, clip.name);

            if (channel >= 0)
            {
                channelUsageInfo[channel].Filename = clip.name;
                channelUsageInfo[channel].OwnerId = ownerId;

                entityChannelInUse[channel] = entityChannels[channel].PlayAudioClip(clip, attachTarget, volume);
            }
        }


        public void OneShotSoundEffect(int ownerId, AudioClip clip, Vector3 position, float volume = 1f)
        {
            var channel = FindFreeAudioChannel(ownerId, clip.name);

            if (channel >= 0)
            {
                channelUsageInfo[channel].Filename = clip.name;
                channelUsageInfo[channel].OwnerId = ownerId;
                
                entityChannelInUse[channel] = entityChannels[channel].PlayAudioClip(clip, position, volume);
            }
        }

        public void AttachSoundToEntity(int ownerId, string filename, GameObject attachTarget, float volume = 1)
        {
            var channel = FindFreeAudioChannel(ownerId, filename);

            if (channel >= 0)
            {
                channelUsageInfo[channel].Filename = filename;
                channelUsageInfo[channel].OwnerId = ownerId;

                entityChannelInUse[channel] = entityChannels[channel].PlayAudioClip(filename, attachTarget, volume);
            }
        }


        public void OneShotSoundEffect(int ownerId, string filename, Vector3 position, float volume = 1f, float delayTime = 0f)
        {
            var channel = FindFreeAudioChannel(ownerId, filename);
            Debug.Log($"Free audio channel: " + channel);

            if (channel >= 0)
            {
                channelUsageInfo[channel].Filename = filename;
                channelUsageInfo[channel].OwnerId = ownerId;
                
                entityChannelInUse[channel] = entityChannels[channel].PlayAudioClip(filename, position, volume, delayTime);
            }
        }
        //
        // public void OneShotSoundEffect(string filename, Vector3 position, float volume = 1f)
        // {
        //     Mixer.GetFloat("Sounds", out var db);
        //
        //     var targetVolume = Mathf.Pow(10f, db / 20f) * volume;
        //
        //     AddressableUtility.Load<AudioClip>(gameObject, "Assets/Sounds/Effects/" + filename, ac =>
        //     {
        //         // Debug.Log("CLIP!" + filename);
        //         AudioSource.PlayClipAtPoint(ac, position, targetVolume);
        //     });
        // }

        public void MarkAudioChannelAsFree(int id)
        {
            entityChannelInUse[id] = false;
        }

        // private void Play

        public void MuteBGM()
        {
            Mixer.SetFloat("Music", -80);
            muteBgm = true;
        }

        public void ToggleMute()
        {
            if (muteBgm)
                Mixer.SetFloat("Music", bgmLevel);
            else
                Mixer.SetFloat("Music", -80);
            muteBgm = !muteBgm;
        }

        public void PlaySystemSound(string name)
        {
            AddressableUtility.Load<AudioClip>(gameObject, "Assets/Sounds/Effects/" + name, PlaySystemSound);
        }
        
        public void PlayBgm(string name)
        {
            AddressableUtility.Load<AudioClip>(gameObject, "Assets/Music/" + name, OnLoad);
        }
        
        public void Update()
        {
        }
    }
}