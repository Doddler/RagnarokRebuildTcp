using System.Collections.Generic;
using Assets.Scripts.Objects;
using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.UI.Hud
{
    public class ToastNotificationArea : MonoBehaviour
    {
        public GameObject PartyInvitePrefab;
        public Transform ToastZone;

        private Dictionary<string, PartyInviteToast> partyInvites;

        public void AddPartyInvite(int partyId, string leaderName, string partyName)
        {
            partyInvites ??= new();

            if (partyInvites.ContainsKey(leaderName))
                return;
            
            CameraFollower.Instance.AppendChatText($"<color=#77FF77>{leaderName} has invited you to join their party '{partyName}'.</color>");
            
            var toastObject = GameObject.Instantiate(PartyInvitePrefab, ToastZone);
            var toast = toastObject.GetComponent<PartyInviteToast>();

            toast.PartyId = partyId;
            toast.LeaderName = leaderName;
            toast.PartyName = partyName;
            toast.Parent = this;
            toast.SecondaryCaption.text = $"<size=-2>From</size> <i>{leaderName}";
            toast.ToastBox.RectTransform().anchoredPosition = new Vector3(400, 0);

            var lt = LeanTween.moveLocalX(toast.ToastBox, 150, 0.5f);
            lt.setEaseOutQuad();
            
            partyInvites.Add(leaderName, toast);
            
            AudioManager.Instance.PlaySystemSound("버튼소리_.ogg");
            
            gameObject.SetActive(true);
        }

        public void CloseAllPartyInvites()
        {
            foreach (var (_, p) in partyInvites)
            {
                Destroy(p.gameObject);
            }
            partyInvites.Clear();
            gameObject.SetActive(false);
        }

        public void OnCloseNotification(string partyLeader)
        {
            partyInvites?.Remove(partyLeader);
            if(ToastZone.childCount == 0)
                gameObject.SetActive(false);
        }
        
    }
}