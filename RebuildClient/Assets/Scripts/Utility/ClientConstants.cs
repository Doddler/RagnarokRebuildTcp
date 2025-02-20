using Assets.Scripts.Objects;
using UnityEngine;
using Utility;

namespace Assets.Scripts.Utility
{
    public class ClientConstants : MonoBehaviorSingleton<ClientConstants>
    {
        public DamageIndicatorPathData DamagePath;
        public DamageIndicatorPathData HealPath;
        public DamageIndicatorPathData ComboPath;
        public DamageIndicatorPathData ExpPath;
        public DamageIndicatorPathData MissPath;
        public DamageIndicatorPathData CriticalPath;
        public DamageIndicatorPathData EffectPath;
    }
}