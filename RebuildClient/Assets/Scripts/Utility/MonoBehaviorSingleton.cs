using UnityEngine;

namespace Utility
{
    public class MonoBehaviorSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T instance;

        public static T Instance
        {
            get
            {
                if (instance != null)
                    return instance;

                instance = FindObjectOfType<T>();
                if (instance != null)
                    return instance;

                var go = new GameObject();

                instance = go.AddComponent<T>();
                DontDestroyOnLoad(go);

                var type = instance.GetType();
                var n = type.ToString();

                go.name = n.Substring(n.LastIndexOf(".") + 1);

                return instance;
            }
        }
    }
}