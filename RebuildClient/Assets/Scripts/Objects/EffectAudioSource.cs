using UnityEngine;

namespace Assets.Scripts.Objects
{
    public class EffectAudioSource : MonoBehaviour
    {
        public int OwnerId = -1;
        public float Volume = 1f;
        public AudioClip Clip;
        
        public void Play()
        {
            AudioManager.Instance.AttachSoundToEntity(OwnerId, Clip, gameObject, Volume);
        }
    }
}