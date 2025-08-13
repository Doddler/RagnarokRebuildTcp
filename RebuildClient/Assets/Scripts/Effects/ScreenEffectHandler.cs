using UnityEngine;

namespace Assets.Scripts.Effects
{
    public class ScreenEffectHandler : MonoBehaviour
    {
        private static readonly int Strength = Shader.PropertyToID("_Strength");
        public Shader EffectShader = null;
        public Material EffectMaterial;
        private float strength = 0;
        private float targetStrength = 1;
        private float startTransitionTime = 0;
        private bool isEnabled = false;
    
        void Start()
        {
            if (EffectShader == null)
            {
                Debug.LogError("no screen effect shader.");
                EffectMaterial = null;
                return;
            }

            EffectMaterial = new Material(EffectShader);
        }

        public void StartHallucination()
        {
            enabled = true;
            isEnabled = true;
        }

        public void EndHallucination()
        {
            startTransitionTime = Time.timeSinceLevelLoad;
            isEnabled = false;
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (isEnabled)
                strength = Mathf.Lerp(strength, targetStrength, Time.deltaTime);
            else
            {
                strength = Mathf.Lerp(strength, 0, Time.deltaTime * 3);
                if (strength <= 0.005f)
                    enabled = false;
            }
            EffectMaterial.SetFloat(Strength, strength);

            Graphics.Blit(source, destination, EffectMaterial);
        }
    }
}