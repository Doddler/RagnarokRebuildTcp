using System;
using Assets.Scripts.Network;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.Hud
{
    public class PartyInviteToast : MonoBehaviour
    {
        public TextMeshProUGUI PrimaryCaption;
        public TextMeshProUGUI SecondaryCaption;
        public GameObject ToastBox;
        
        [NonSerialized] public ToastNotificationArea Parent;
        [NonSerialized] public int PartyId;
        [NonSerialized] public string LeaderName;
        [NonSerialized] public string PartyName;
        
        public void OnClick()
        {
            var promptWindow = UiManager.Instance.YesNoOptionsWindow;
            
            promptWindow.BeginPrompt($"<color=#007700>{LeaderName}</color> has invited you to join their party '<color=#000077>{PartyName}</color>'.\nWould you like to accept?",
                "Accept", "Decline", AcceptPartyInvite, DeclinePartyInvite, false);
        }

        private void AcceptPartyInvite()
        {
            NetworkManager.Instance.PartyAcceptInvite(PartyId);
            OnDismiss();
        }

        private void DeclinePartyInvite()
        {
            OnDismiss();
        }

        public void OnDismiss()
        {
            Parent.OnCloseNotification(LeaderName);
            Destroy(gameObject);
        }
    }
}