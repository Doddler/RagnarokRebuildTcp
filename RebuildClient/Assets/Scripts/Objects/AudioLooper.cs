using UnityEngine;

namespace Assets.Scripts
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioLooper : MonoBehaviour
    {
        public float LoopTime;
        public float Countdown;

        private AudioSource source;
        private GameObject listener;

        private int defaultPriority;

        public void Awake()
        {
            source = GetComponent<AudioSource>();
            source.loop = false;
            Countdown = LoopTime;
            var camFollower = CameraFollower.Instance;
            if (camFollower == null)
	            return;
            listener = CameraFollower.Instance.ListenerProbe;
            if(defaultPriority == 0)
                defaultPriority = source.priority;

            if (Mathf.Approximately(LoopTime, 0))
	            LoopTime = source.clip.length;

            if (CalcDistance() > source.maxDistance)
            {
                //source.priority = 255;
                source.enabled = false;
                source.Stop();
            }
        }

        private float CalcDistance()
        {
	        return (transform.position - listener.transform.position).magnitude;
        }

        public void Update()
        {
            if (listener == null)
            {
                listener = CameraFollower.Instance.ListenerProbe;
                return;
            }

            if (Mathf.Approximately(LoopTime, 0f))
                return;

            var dist = CalcDistance();
            
            if (!source.enabled && dist < source.maxDistance * 2)
            {
                source.enabled = true;
                if (!source.isPlaying)
                {
                    source.Play();
                    source.priority = defaultPriority;
                }
            }

            if (source.enabled && dist > source.maxDistance * 2)
            {
                source.Stop();
                source.enabled = false;
                // source.priority = 255;
                return;
            }

            if (!source.enabled)
                return;
            
            // Debug.Log(dist);

            Countdown -= Time.deltaTime;
            if (Countdown < 0)
            {
                Countdown = LoopTime;
                source.Play();
            }
        }
    }
}
