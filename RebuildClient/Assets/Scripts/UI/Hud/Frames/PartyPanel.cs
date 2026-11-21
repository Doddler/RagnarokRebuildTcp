using System.Collections.Generic;
using Assets.Scripts.Data;
using Assets.Scripts.PlayerControl;
using UnityEngine;

namespace Assets.Scripts.UI.Hud
{
    //Hidden while no one else is in the party.
    public class PartyPanel : MonoBehaviour
    {
        public RectTransform EntryArea;
        public PartyFrame PanelTemplate;

        private readonly Stack<PartyFrame> unusedPanels = new();
        private readonly Dictionary<int, PartyFrame> partyEntryLookup = new();
        private readonly List<PartyFrame> sortedEntries = new();

        //for the scene's lifetime, as the panel is hidden rather than destroyed
        private void Awake()
        {
            PanelTemplate.gameObject.SetActive(false);

            var state = PlayerState.Instance;
            state.Party.Changed += FullRefreshPartyMemberPanel;
            state.Party.MemberAdded += AddPartyMember;
            state.Party.MemberChanged += RefreshPartyMember;
            state.Party.MemberEntityChanged += RefreshPartyMemberEntity;
            state.Party.MemberRemoved += RemovePartyMember;
            state.Party.MemberVitalsChanged += UpdateHpSpOfPartyMember;
            state.MapChanged += OnChangeMaps;
        }

        private void OnDestroy()
        {
            var state = PlayerState.Instance;
            state.Party.Changed -= FullRefreshPartyMemberPanel;
            state.Party.MemberAdded -= AddPartyMember;
            state.Party.MemberChanged -= RefreshPartyMember;
            state.Party.MemberEntityChanged -= RefreshPartyMemberEntity;
            state.Party.MemberRemoved -= RemovePartyMember;
            state.Party.MemberVitalsChanged -= UpdateHpSpOfPartyMember;
            state.MapChanged -= OnChangeMaps;
        }

        private void OnChangeMaps()
        {
            //every row's map suffix depends on the player's map
            foreach (var (_, entry) in partyEntryLookup)
                entry.FullRefreshPartyMemberInfo();
        }

        private void RefreshPartyMember(int memberId)
        {
            if (!partyEntryLookup.TryGetValue(memberId, out var entry))
                return;

            entry.FullRefreshPartyMemberInfo();
            //logging in or out moves the member between the online and offline groups
            SortEntries();
        }

        private void RefreshPartyMemberEntity(int memberId)
        {
            if (partyEntryLookup.TryGetValue(memberId, out var entry))
                entry.RefreshEntity();
        }

        private void UpdateHpSpOfPartyMember(int memberId)
        {
            if (partyEntryLookup.TryGetValue(memberId, out var entry))
                entry.RefreshHpSp();
        }

        private void RemovePartyMember(int memberId)
        {
            if (partyEntryLookup.Remove(memberId, out var entry))
                ReleaseRow(entry);
            RefreshVisibility();
        }

        private void AddPartyMember(PartyMemberInfo info)
        {
            AddRow(info);
            SortEntries();
            RefreshVisibility();
        }

        private void AddRow(PartyMemberInfo info)
        {
            if (info.PartyMemberId == PlayerState.Instance.Party.MemberId)
                return;

            if (!unusedPanels.TryPop(out var entry))
            {
                entry = Instantiate(PanelTemplate, EntryArea);
                entry.name = "PartyMember";
            }
            entry.PartyMemberInfo = info;
            entry.gameObject.SetActive(true);
            partyEntryLookup.Add(info.PartyMemberId, entry);
        }

        private void ReleaseRow(PartyFrame entry)
        {
            entry.gameObject.SetActive(false);
            unusedPanels.Push(entry);
        }

        private void RefreshVisibility() => gameObject.SetActive(partyEntryLookup.Count > 0);

        //reused rows keep their old place in the hierarchy until sorted here
        private void SortEntries()
        {
            sortedEntries.Clear();
            sortedEntries.AddRange(partyEntryLookup.Values);
            sortedEntries.Sort((a, b) => a.PartyMemberInfo.CompareTo(b.PartyMemberInfo));
            for (var i = 0; i < sortedEntries.Count; i++)
                sortedEntries[i].transform.SetSiblingIndex(i);
        }

        private void FullRefreshPartyMemberPanel()
        {
            foreach (var (_, entry) in partyEntryLookup)
                ReleaseRow(entry);
            partyEntryLookup.Clear();

            var state = PlayerState.Instance;
            if (state.Party.IsInParty)
            {
                foreach (var info in state.Party.Members.Values)
                    AddRow(info);
                SortEntries();
            }

            RefreshVisibility();
        }
    }
}
