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
        public GameObject LevelUpPrefab;
        public GameObject ResurrectPrefab;
        public GameObject DeathPrefab;
    }
}