using UnityEngine;

namespace Assets.Scripts.Utility
{
    public static class TransformHelper
    {
        public static RectTransform RectTransform(this Transform t) => (RectTransform)t;
        public static RectTransform RectTransform(this GameObject t) => (RectTransform)t.transform;
        public static RectTransform RectTransform(this MonoBehaviour m) => (RectTransform)m.transform;
    }
}