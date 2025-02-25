using System;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Hud
{
    public class StatusEffectEntry : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (UiManager.Instance.IsDraggingItem)
                return;

            var data = ClientDataLoader.Instance.GetStatusEffect((int)StatusEffect);
            
            if (data != null)
            {
                UiManager.Instance.ShowTooltip(gameObject, data.Description);
                return;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            UiManager.Instance.HideTooltip(gameObject);
        }

        public void OnDestroy()
        {
            UiManager.Instance.HideTooltip(gameObject); //will do nothing if this isn't the actively hovered object
        }
    }
}