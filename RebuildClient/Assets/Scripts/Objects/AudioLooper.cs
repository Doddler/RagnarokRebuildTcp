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

        public void Awake()
        {
            source = GetComponent<AudioSource>();
            Countdown = LoopTime;
            var camFollower = CameraFollower.Instance;
            if (camFollower == null)
	            return;
            listener = CameraFollower.Instance.ListenerProbe;

            if (Mathf.Approximately(LoopTime, 0))
	            LoopTime = source.clip.length;

            if(CalcDistance() > source.maxDistance)
                source.Stop();
        }

        private float CalcDistance()
        {
	        return Mathf.Max(Mathf.Abs(transform.position.x - listener.transform.position.x),
		        Mathf.Abs(transform.position.y - listener.transform.position.y));
        }

        public void Update()
        {
	        if (listener == null)
		        return;

            if (Mathf.Approximately(LoopTime, 0f))
                return;
            if(!source.isPlaying && CalcDistance() < source.maxDistance * 2)
                source.Play();

            if(source.isPlaying && CalcDistance() > source.maxDistance * 2)
                source.Stop();

            if (!source.isPlaying)
                return;
            Countdown -= Time.deltaTime;
            if (Countdown < 0)
            {
                Countdown = LoopTime;
                source.Play();
            }
        }
    }
}
