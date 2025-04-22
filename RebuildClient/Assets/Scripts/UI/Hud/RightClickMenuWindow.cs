using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Utility;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Hud
{
    public class RightClickMenuWindow : WindowBase
    {
        public GameObject EntryPrefab;
        public GameObject BoundaryPrefab;

        private Transform container;
        private Stack<Button> unusedButtons;
        private Stack<GameObject> unusedBoundaries;
        private List<Button> activeButtons;
        private List<GameObject> activeBoundaries;

        private int targetEntityId;

        public bool RightClickSelf()
        {
            unusedButtons ??= new Stack<Button>();
            unusedBoundaries ??= new Stack<GameObject>();
            activeButtons ??= new List<Button>();
            activeBoundaries ??= new List<GameObject>();
            
            if(gameObject.activeInHierarchy)
                HideWindow();
            
            var state = PlayerState.Instance;
            if (!state.IsInParty)
                return false;

            var button = AddEntry($"Leave party");
            button.onClick.AddListener(LeaveParty);
            
            transform.position = UiManager.Instance.GetScreenPositionOfCursor();
            
            ShowWindow();
            CameraFollower.Instance.ActivePromptType = PromptType.RightClickMenu;

            return true;
        }

        public void RightClickPlayer(ServerControllable target)
        {
            unusedButtons ??= new Stack<Button>();
            unusedBoundaries ??= new Stack<GameObject>();
            activeButtons ??= new List<Button>();
            activeBoundaries ??= new List<GameObject>();
            
            if(gameObject.activeInHierarchy)
                HideWindow();

            targetEntityId = target.Id;
            
            var state = PlayerState.Instance;
            if (state.EntityId == target.Id)
                return;
            
            if (state.IsInParty)
            {
                if (state.PartyLeader == state.PartyMemberId)
                {
                    if (target.PartyName == state.PartyName)
                    {
                        return;
                        var button = AddEntry($"Kick {target.Name} from the party");
                        //button.onClick.AddListener(() => );
                    }
                    else if (string.IsNullOrWhiteSpace(target.PartyName))
                    {
                        var button = AddEntry($"Invite {target.Name} to party");
                        button.onClick.AddListener(InviteToParty);
                    }
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(target.PartyName))
                {
                    var button = AddEntry($"Form a party with {target.Name}");
                    button.onClick.AddListener(FormPartyWith);
                }
            }

            if (activeButtons.Count <= 0)
                return;

            transform.position = UiManager.Instance.GetScreenPositionOfCursor();

            //StartCoroutine(DelayedRebuild());
            ShowWindow();
            CameraFollower.Instance.ActivePromptType = PromptType.RightClickMenu;
        }

        public void LeaveParty()
        {
            if(!PlayerState.Instance.IsInParty)
                CameraFollower.Instance.AppendChatText($"<color=yellow>You are not currently in a party.</color>");
            else
                NetworkManager.Instance.LeaveParty();
            HideWindow();
        }

        public void InviteToParty()
        {
            NetworkManager.Instance.PartyInviteById(targetEntityId);
            HideWindow();
        }

        public void FormPartyWith()
        {
            var state = PlayerState.Instance;
            if (!state.KnownSkills.TryGetValue(CharacterSkill.BasicMastery, out var mastery) || mastery < 6)
            {
                CameraFollower.Instance.AppendError($"You need to have learned Basic Mastery level 6 to form a party.");
                return;
            }
            
            UiManager.Instance.TextInputWindow.BeginTextInput($"Name your party (must be unique)", FinishCreateParty);
        }

        public void FinishCreateParty(string partyName)
        {
            NetworkManager.Instance.OrganizeParty(partyName, targetEntityId);
            HideWindow();
        }
        
        public override void HideWindow()
        {
            if (activeButtons != null)
            {
                foreach (var b in activeButtons)
                {
                    b.gameObject.SetActive(false);
                    b.onClick.RemoveAllListeners();
                    unusedButtons.Push(b);
                }
                activeButtons.Clear();
            }

            if (activeBoundaries != null)
            {
                foreach (var g in activeBoundaries)
                {
                    g.SetActive(false);
                    unusedBoundaries.Push(g);
                }
                activeBoundaries.Clear();
            }

            if (CameraFollower.Instance.ActivePromptType == PromptType.RightClickMenu)
                CameraFollower.Instance.ActivePromptType = PromptType.None;
            base.HideWindow();
        }

        public Button AddEntry(string entryText)
        {
            if (!unusedButtons.TryPop(out var button))
            {
                var go = GameObject.Instantiate(EntryPrefab, transform);
                button = go.GetComponent<Button>();
            }

            var text = button.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            text.text = entryText;
            
            if (activeButtons.Count > 0)
            {
                if (!unusedBoundaries.TryPop(out var boundary))
                    boundary = Instantiate(BoundaryPrefab, transform);
                boundary.SetActive(true);
                boundary.transform.SetAsLastSibling();
                activeBoundaries.Add(boundary);
            }
            
            button.gameObject.transform.SetAsLastSibling();
            button.gameObject.SetActive(true);
            
            activeButtons.Add(button);

            return button;
        }

        public void Awake()
        {
            EntryPrefab.SetActive(false);
            BoundaryPrefab.SetActive(false);
            container = transform.parent;
        }
        
        public void Update()
        {
            if (transform != container.GetChild(container.childCount - 1))
                HideWindow();
        }
    }
}