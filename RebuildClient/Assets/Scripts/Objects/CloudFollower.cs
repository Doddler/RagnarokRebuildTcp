using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Objects
{
    public class CloudFollower : MonoBehaviour, IMapEffect
    {
        public ParticleSystem ParticleSystem;
        public CameraFollower Camera;
        public bool TrackYAxis;
        
        public void Update()
        {
            if (Camera == null)
                Camera = CameraFollower.Instance;

            if(TrackYAxis)
                transform.position = new Vector3(Camera.TargetFollow.x, Camera.TargetFollow.y, Camera.TargetFollow.z);
            else
                transform.position = new Vector3(Camera.TargetFollow.x, transform.position.y, Camera.TargetFollow.z);
        }


        public void CreateWhenEnteringMap()
        {
            var main = ParticleSystem.main;
            main.prewarm = true;

            ParticleSystem.Play();
        }

        public void CreateWhileOnMap()
        {
            var main = ParticleSystem.main;
            main.prewarm = false;

            ParticleSystem.Play();
        }

        public void Create()
        {
            var len = 5 - SceneTransitioner.TimeSinceMapLoaded;
            if (len > 0)
            {
                ParticleSystem.Stop();
                ParticleSystem.Clear();
                Update();
                
                var main = ParticleSystem.main;

                main.prewarm = true;
            }

            ParticleSystem.Play();
            
        }

        public void Remove()
        {
            if (ParticleSystem == null)
            {
                Destroy(gameObject);
                return;
            }
            
            ParticleSystem.Stop();
            var removal = gameObject.AddComponent<DestroyAfterTime>();
            removal.Lifetime = 15f;
        }
    }
}