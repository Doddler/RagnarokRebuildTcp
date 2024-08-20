using System.Collections.Generic;

namespace Assets.Scripts.Utility
{
    public static class DebugValueHolder
    {
#if DEBUG
        
        private static readonly Dictionary<string, int> values = new();
        private static readonly Dictionary<string, float> floatValues = new();
        
        public static void Set(string s, float f) => floatValues[s] = f;
        
        public static int GetOrDefault(string key, int defaultVal)
        {
            if (values.TryGetValue(key, out var res))
                return res;
        
            values.Add(key, defaultVal);
            return defaultVal;
        }
        
        
        public static float GetOrDefault(string key, float floatVal)
        {
            if (floatValues.TryGetValue(key, out var res))
                return res;
        
            floatValues.Add(key, floatVal);
            return floatVal;
        }
#else
        public static int GetOrDefault(string key, int defaultVal) => defaultVal;
        public static float GetOrDefault(string key, float floatVal) => floatVal;
        public static void Set(string s, float f) {}
#endif
    }
}