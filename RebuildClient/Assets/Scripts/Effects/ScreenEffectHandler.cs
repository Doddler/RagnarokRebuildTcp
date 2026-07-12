using UnityEngine;

namespace Assets.Scripts.Effects
{
    public class ScreenEffectHandler : MonoBehaviour
    {
        public static float HallucinationStrength { get; private set; }

        private const float TargetStrength = 1f;
        private float strength;
        private bool isEnabled;

        public void StartHallucination()
        {
            enabled = true;
            isEnabled = true;
        }

        public void EndHallucination()
        {
            isEnabled = false;
        }

        private void OnDisable()
        {
            HallucinationStrength = 0f;
        }

        private void Update()
        {
            if (isEnabled)
                strength = Mathf.Lerp(strength, TargetStrength, Time.deltaTime);
            else
            {
                strength = Mathf.Lerp(strength, 0f, Time.deltaTime * 3f);
                if (strength <= 0.005f)
                {
                    strength = 0f;
                    HallucinationStrength = 0f;
                    enabled = false;
                    return;
                }
            }

            HallucinationStrength = strength;
        }
    }
}
