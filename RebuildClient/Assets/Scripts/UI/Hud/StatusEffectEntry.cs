using System;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Hud
{
    public class StatusEffectEntry : MonoBehaviour
    {
        public Image StatusIcon;
        public TextMeshProUGUI RemainingTime;
        
        [NonSerialized] public CharacterStatusEffect StatusEffect;
        [NonSerialized] public float Expiration;
        [NonSerialized] public bool NeedsUpdate;

        private int secondsRemaining;

        public void UpdateTime()
        {
            var timeRemaining = Expiration - Time.timeSinceLevelLoad;
            var seconds = (int)(timeRemaining + 1f);
            if (seconds <= 0)
            {
                RemainingTime.text = "";
                RemainingTime.gameObject.SetActive(false);
                return;
            }

            if (secondsRemaining == seconds)
                return;

            RemainingTime.gameObject.SetActive(true);
            
            if (seconds <= 60)
                RemainingTime.text = $"{seconds}s";
            else if (seconds <= 3600)
                RemainingTime.text = $"{seconds / 60}m";
            else
                RemainingTime.text = $"{seconds / 3600}h";
        }
    }
}