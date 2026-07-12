using UnityEngine;

namespace Assets.Scripts.MapEditor
{
    public class RoWaterAnimator : MonoBehaviour
    {
        public MapWater Water;
        public Material Material;

        private int curFrame;
        private int maxFrames;
        private float cooldown;
        private float fullFrameTime;
        private bool isActive;
        
        private static RoWaterAnimator instance;

        public static RoWaterAnimator Instance
        {
            get
            {
                if (instance != null)
                    return instance;
                instance = GameObject.FindObjectOfType<RoWaterAnimator>();
                return instance;
            }
        }

        // Use this for initialization
        void Start()
        {
            if (Water == null || Water.Images == null || Water.Images.Length <= 0)
            {
                // still publish so the water-depth pass doesn't read the previous map's values
                if (Water != null)
                    PublishWaveGlobals();
                isActive = false;
                return;
            }

            curFrame = 0;
            maxFrames = Water.Images.Length;
            fullFrameTime = 3 / 60f;
            if(Water.AnimSpeed > 0)
                fullFrameTime = Water.AnimSpeed / 60f;

            Material.SetFloat("_WaveHeight", Water.WaveHeight / 5f);
            Material.SetFloat("_WaveSpeed", Water.WaveSpeed);
            Material.SetFloat("_WavePitch", Water.WavePitch);

            PublishWaveGlobals();

            isActive = true;
        }

        // Update is called once per frame
        void Update()
        {
            if (!isActive)
                return;

            cooldown -= Time.deltaTime;
            if (cooldown > 0)
                return;

            curFrame++;
            cooldown += fullFrameTime;
            if (curFrame >= maxFrames)
                curFrame = 0;

            Material.mainTexture = Water.Images[curFrame];
            Shader.SetGlobalTexture("_WaterImageTexture", Water.Images[curFrame]);
            
#if UNITY_EDITOR
            Material.SetFloat("_WaveHeight", Water.WaveHeight / 5f);
            Material.SetFloat("_WaveSpeed", Water.WaveSpeed);
            Material.SetFloat("_WavePitch", Water.WavePitch);
            PublishWaveGlobals();
#endif
        }

        private static readonly int RoWaterWaveHeightId = Shader.PropertyToID("_RoWaterWaveHeight");
        private static readonly int RoWaterWaveSpeedId = Shader.PropertyToID("_RoWaterWaveSpeed");
        private static readonly int RoWaterWavePitchId = Shader.PropertyToID("_RoWaterWavePitch");

        private void PublishWaveGlobals()
        {
            Shader.SetGlobalFloat(RoWaterWaveHeightId, Water.WaveHeight / 5f);
            Shader.SetGlobalFloat(RoWaterWaveSpeedId, Water.WaveSpeed);
            Shader.SetGlobalFloat(RoWaterWavePitchId, Water.WavePitch);
        }
    }
}