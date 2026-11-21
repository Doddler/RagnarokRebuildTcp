using System;
using Assets.Scripts.Network;
using RebuildSharedData.ClientTypes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Hud
{
    public class StatusEffectIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Image StatusIcon;
        public TextMeshProUGUI RemainingTime;

        [NonSerialized] public float Expiration;

        private StatusEffectData status;
        private bool canCancel;
        private bool isPartyMember;
        private int secondsRemaining;

        private static bool IsShiftHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        public void Show(StatusEffectData status, float expiration, Sprite sprite, bool canCancel, bool isPartyMember)
        {
            this.status = status;
            Expiration = expiration;
            StatusIcon.sprite = sprite;
            this.canCancel = canCancel;
            this.isPartyMember = isPartyMember;
            secondsRemaining = 0;
            UpdateTime();
        }

        public void UpdateTime()
        {
            var timeRemaining = Expiration - Time.timeSinceLevelLoad;
            var seconds = (int)(timeRemaining + 1f);
            if (seconds <= 0 || seconds > 86400) //stop showing if the timer is greater than 24h
            {
                //-1 once hidden, so a status that never shows a time, like the falcon, isn't hidden again every frame
                if (secondsRemaining == -1)
                    return;
                RemainingTime.text = "";
                RemainingTime.gameObject.SetActive(false);
                secondsRemaining = -1;
                return;
            }

            if (secondsRemaining == seconds)
                return;

            RemainingTime.gameObject.SetActive(true);
            secondsRemaining = seconds;

            if (seconds <= 60)
                RemainingTime.SetText("{0}s", seconds);
            else if (seconds <= 3600)
                RemainingTime.SetText("{0}m", seconds / 60);
            else
                RemainingTime.SetText("{0}h", seconds / 3600);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (UiManager.Instance.IsDraggingItem)
                return;

            if (isPartyMember && !IsShiftHeld)
                return;

            var desc = status.Description;
            if (canCancel)
                desc += "\n<size=60%>(Shift-Right Click to Remove)";
            UiManager.Instance.ShowTooltip(gameObject, desc);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            UiManager.Instance.HideTooltip(gameObject);
        }

        //an ended icon can still be under the mouse; does nothing if another tooltip took over
        private void OnDisable()
        {
            if (UiManager.Instance != null)
                UiManager.Instance.HideTooltip(gameObject);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (canCancel && eventData.button == PointerEventData.InputButton.Right && IsShiftHeld)
                NetworkManager.Instance.SendRemoveStatusEffect(status.StatusEffect);
        }
    }
}