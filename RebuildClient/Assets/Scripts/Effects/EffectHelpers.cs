using UnityEngine;

namespace Assets.Scripts.Effects
{
    public static class EffectHelpers
    {
        private static Material noZTestMaterial;

        public static Material NoZTestMaterial
        {
            get
            {
                if (noZTestMaterial != null)
                    return noZTestMaterial;
                noZTestMaterial = new Material(ShaderCache.Instance.SpriteShaderNoZTest);
                noZTestMaterial.renderQueue = 3005; //above water and all other sprites
                return noZTestMaterial;
            }
        }

        public static bool TryChangeAndCycleColor(Color color, Vector3 change, out Color colorOut)
        {
            var needsCycle = false;
            needsCycle |= EffectHelpers.TryChangeAndCycleValue(color.r, change.x * Time.deltaTime, 0f, 1f, out var red);
            needsCycle |= EffectHelpers.TryChangeAndCycleValue(color.g, change.y * Time.deltaTime, 0f, 1f, out var blue);
            needsCycle |= EffectHelpers.TryChangeAndCycleValue(color.b, change.z * Time.deltaTime, 0f, 1f, out var green);

            colorOut = new Color(red, blue, green, color.a);
            return needsCycle;
        }

        public static bool TryChangeAndCycleValue(float val, float change, float lowBounds, float highBounds, out float valOut)
        {
            if (val >= highBounds)
            {
                valOut = lowBounds;
                return false;
            }

            valOut = val + change;
            if (val > highBounds)
            {
                valOut = highBounds;
                return true;
            }

            if (val < lowBounds)
            {
                valOut = highBounds;
                return true;
            }

            return false;
        }
    }
}