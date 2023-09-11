using TMPro;
using UnityEngine;

namespace Assets.Scripts.Objects
{
    [CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/DamageIndicatorPathData", order = 1)]
    public class DamageIndicatorPathData : ScriptableObject
    {
        public AnimationCurve Trajectory;
        public AnimationCurve Size;
        public AnimationCurve Alpha;
        public bool FliesAwayFromTarget = true;
        public int HeightMultiplier = 6;
        public float TweenTime = 1f;
    }
}