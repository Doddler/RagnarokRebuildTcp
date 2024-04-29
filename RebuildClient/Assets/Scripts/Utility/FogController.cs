using Assets.Scripts;
using UnityEngine;


namespace Utility
{
    public class FogController : MonoBehaviour
    {
        // public float Start;
        // public float End;
        // public Color Color;

        public float CameraDistanceMultiplier = 1f;
        public float FogFadeSpeed = 1f;

        private float fogDistance = 100f;

        public bool StopUpdatingDistance = false;
        public bool OverrideDistance = false;
        public bool OverrideBgColor = false;
        public bool OverrideNearFar = false;
        public bool ForceDistanceChange = false;
        public Color OverrideBgColorValue = Color.black;
        public float OverrideDistanceValue = 100f;
        public float OverrideNearValue = 40f;
        public float OverrideFarValue = 395f;
        public CameraClearFlags ClearFlags = CameraClearFlags.Skybox;


        //this is out of control
        public Material WoeBannerMaterial;
        public Texture2D WoeTexture;
        public bool UpdateWoeBanner;

        private bool firstUpdate = false;
        
        private CameraFollower cameraFollower;

        public float GetFogDistance => fogDistance;
        
        [ContextMenu("Load Fog Settings From Scene")]
        public void LoadFromScene()
        {
            // Start = RenderSettings.fogStartDistance;
            // End = RenderSettings.fogEndDistance;
            // Color = RenderSettings.fogColor;
        }

        public void Awake()
        {
            cameraFollower = CameraFollower.Instance;
            firstUpdate = true;
        }

        public void UpdateFog(float nearRatio, float farRatio, bool forceUpdate)
        {
            var cam = CameraFollower.Instance.Recorder.gameObject.GetComponent<Camera>();
            if(ClearFlags != 0)
                cam.clearFlags = ClearFlags;

            if (!ClearFlags.HasFlag(CameraClearFlags.Skybox))
            {
                if (!OverrideBgColor) //cinemachine mode
                {
                    var val = (RenderSettings.fogEndDistance - fogDistance) / (RenderSettings.fogEndDistance - RenderSettings.fogStartDistance);
                    cam.backgroundColor = RenderSettings.fogColor * (1 - val);
                }
                else
                    cam.backgroundColor = OverrideBgColorValue;
            }


            if (OverrideNearFar)
            {
                if (ForceDistanceChange)
                {
                    RenderSettings.fogStartDistance = OverrideNearValue;
                    RenderSettings.fogEndDistance = OverrideFarValue;
                }
                else
                {
                    RenderSettings.fogStartDistance = Mathf.Lerp(RenderSettings.fogStartDistance, OverrideNearValue, Time.deltaTime * FogFadeSpeed);
                    RenderSettings.fogEndDistance =  Mathf.Lerp(RenderSettings.fogEndDistance, OverrideFarValue, Time.deltaTime * FogFadeSpeed);;
                }

                return;
            }
            
            if (OverrideDistance && ForceDistanceChange)
            {
                var dist = OverrideDistanceValue;
                dist *= CameraDistanceMultiplier;
                fogDistance = dist;

                var near = dist * nearRatio;
                var far = dist * farRatio;

                RenderSettings.fogStartDistance = near;
                RenderSettings.fogEndDistance = far;
                
                return;
            }
        
            if (Physics.Raycast(transform.position, transform.forward, out var hit, 900, 1 << LayerMask.NameToLayer("Ground")))
            {
                var dist = hit.distance;

                dist *= CameraDistanceMultiplier;

                if (OverrideDistance)
                    dist = OverrideDistanceValue;
                
                if (!StopUpdatingDistance)
                {
                    fogDistance = dist;
                    if (forceUpdate)
                        fogDistance = dist;
                }

                var near = fogDistance * nearRatio;
                var far = fogDistance * farRatio;

                if (ForceDistanceChange || forceUpdate)
                {
                    RenderSettings.fogStartDistance = near;
                    RenderSettings.fogEndDistance = far;
                }
                else
                {
                    RenderSettings.fogStartDistance = Mathf.Lerp(RenderSettings.fogStartDistance, near, Time.deltaTime * FogFadeSpeed);;
                    RenderSettings.fogEndDistance = Mathf.Lerp(RenderSettings.fogEndDistance, far, Time.deltaTime * FogFadeSpeed);;
                }
            }
            else
                Debug.Log("No Ground for fog update!");
        
        }

        public void LateUpdate()
        {
            UpdateFog(cameraFollower.FogNearRatio, cameraFollower.FogFarRatio, firstUpdate);

            if (UpdateWoeBanner)
                WoeBannerMaterial.mainTexture = WoeTexture;
            
            if(firstUpdate)
                firstUpdate = false;
        }
    }
}