using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Data;
using Assets.Scripts.PlayerControl;
using UnityEngine;

namespace Assets.Scripts.UI.Hud
{
    public class PartyPanel : MonoBehaviour
    {
        public RectTransform EntryArea;
        public PartyPanelEntry PanelTemplate;
        public Stack<PartyPanelEntry> UnusedPanels = new();
        public Dictionary<int, PartyPanelEntry> PartyEntryLookup = new();
        public CanvasGroup CanvasGroup;

        [NonSerialized] public PartyPanelEntry HoverEntry;

        public void StartSkillOnCursor()
        {
            if(HoverEntry != null)
                HoverEntry.SkillTargetHover.gameObject.SetActive(true);
            CanvasGroup.blocksRaycasts = true;
        }

        public void EndSkillOnCursor()
        {
            if(HoverEntry != null)
                HoverEntry.SkillTargetHover.gameObject.SetActive(false);
            CanvasGroup.blocksRaycasts = false;
        }
        
        public void RefreshPartyMember(int memberId)
        {
            if (PartyEntryLookup.TryGetValue(memberId, out var entry))
            {
                if (PlayerState.Instance.PartyMembers.TryGetValue(memberId, out var updatedEntry))
                {
                    entry.PartyMemberInfo = updatedEntry;
                    entry.FullRefreshPartyMemberInfo();
                    entry.UpdatePlayerProximity(CameraFollower.Instance.PlayerPosition);
                }
                else
                {
                    PartyEntryLookup.Remove(memberId);
                    UnusedPanels.Push(entry);
                    entry.gameObject.SetActive(false);
                }
            }
        }

        public void UpdateHpSpOfPartyMember(int memberId)
        {
            if (PartyEntryLookup.TryGetValue(memberId, out var entry))
            {
                entry.RefreshHpSp();
            }
        }

        public void RemovePartyMember(int memberId)
        {
            if (PartyEntryLookup.Remove(memberId, out var entry))
            {
                UnusedPanels.Push(entry);
                entry.gameObject.SetActive(false);
            }
        }

        public void AddPartyMember(PartyMemberInfo info)
        {
            if (info.PartyMemberId == PlayerState.Instance.PartyMemberId)
                return;
            
            if(!UnusedPanels.TryPop(out var entry))
                entry = GameObject.Instantiate(PanelTemplate, EntryArea);
            entry.gameObject.SetActive(true);
            entry.Parent = this;
            entry.PartyMemberInfo = info;
            entry.FullRefreshPartyMemberInfo();
            entry.UpdatePlayerProximity(CameraFollower.Instance.PlayerPosition);
            PartyEntryLookup.Add(info.PartyMemberId, entry);
        }
        
        public void FullRefreshPartyMemberPanel()
        {
            var s = PlayerState.Instance;
            
            foreach (var (_, entry) in PartyEntryLookup)
            {
                entry.gameObject.SetActive(false);
                UnusedPanels.Push(entry);
            }
            PartyEntryLookup.Clear();

            
            if (!s.IsInParty || s.PartyMembers == null || s.PartyMembers.Count == 0)
            {
                gameObject.SetActive(false);
                return;
            }
            
            gameObject.SetActive(true);
            
            var members = s.PartyMembers.Values.ToList();
            members.Sort();
            
            foreach (var info in members)
            {
                var id = info.PartyMemberId;
                // if (id == s.PartyMemberId)
                //     continue;
                if (PartyEntryLookup.TryGetValue(id, out var entry))
                {
                    entry.FullRefreshPartyMemberInfo();
                    entry.UpdatePlayerProximity(CameraFollower.Instance.PlayerPosition);
                }
                else
                {
                    AddPartyMember(info);
                }
            }
        }

        public void Awake()
        {
            PanelTemplate.gameObject.SetActive(false);
        }

        public void Update()
        {
            var s = PlayerState.Instance;
            if (!s.IsInParty || s.PartyMembers == null || s.PartyMembers.Count == 0)
                return;

            var playerPosition = CameraFollower.Instance.PlayerPosition;
            if (playerPosition == Vector2Int.zero)
                return;
            
            foreach (var (_, entry) in PartyEntryLookup)
                entry.UpdatePlayerProximity(playerPosition);

        }
    }
}