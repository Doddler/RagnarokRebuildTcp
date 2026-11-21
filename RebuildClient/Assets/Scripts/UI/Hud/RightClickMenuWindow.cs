using System.Collections.Generic;
using Assets.Scripts.Data;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using RebuildSharedData.Config;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Hud
{
    public class RightClickMenuWindow : WindowBase
    {
        public GameObject EntryPrefab;
        public GameObject BoundaryPrefab;
        public TextMeshProUGUI NameText;
        public GameObject Levels;
        public TextMeshProUGUI DetailsText;
        //the bar only shows while the exp needed is known
        public TextMeshProUGUI BaseExpText;
        public Slider BaseExpBar;
        public TextMeshProUGUI JobExpText;
        public Slider JobExpBar;

        private Transform container;
        private readonly Stack<Button> unusedButtons = new();
        private readonly List<Button> activeButtons = new();
        private GameObject boundary;

        private static readonly Vector2 CursorGap = new(30f, 0f);
        //lighter than the exp bar fills, for the dark menu
        private const string BaseExpColor = "#F2CC5A";
        private const string JobExpColor = "#5ED6CB";
        private const string LabelColor = "#AAAAAA";

        private void BeginMenu()
        {
            if(gameObject.activeInHierarchy)
                HideWindow();
            HideInfo();
        }

        private void HideInfo()
        {
            NameText.gameObject.SetActive(false);
            Levels.SetActive(false);
            DetailsText.gameObject.SetActive(false);
        }

        private void ShowInfo(string characterName, string details)
        {
            NameText.text = characterName;
            NameText.gameObject.SetActive(true);
            DetailsText.text = details;
            DetailsText.gameObject.SetActive(!string.IsNullOrEmpty(details));
        }

        //a row without a line is hidden
        private void ShowLevels(string baseLine, string jobLine = null)
        {
            ShowLevelRow(BaseExpText, BaseExpBar, baseLine, null);
            ShowLevelRow(JobExpText, JobExpBar, jobLine, null);
            Levels.SetActive(true);
        }

        private static void ShowLevelRow(TextMeshProUGUI text, Slider bar, string line, float? progress)
        {
            text.transform.parent.gameObject.SetActive(line != null);
            text.text = line;
            bar.gameObject.SetActive(progress.HasValue);
            if (progress.HasValue)
                bar.value = progress.Value;
        }

        //placed after showing, as the size follows the options
        private void OpenMenuAtCursor()
        {
            ShowWindow();
            ((RectTransform)transform).PlaceNextToCursor(CursorGap, false);
            CameraFollower.Instance.ActivePromptType = PromptType.RightClickMenu;
        }

        public void RightClickSelf()
        {
            BeginMenu();

            var state = PlayerState.Instance;
            ShowInfo(state.PlayerName, PlayerDetails(state.JobId, state.Party.IsInParty ? state.Party.Name : null));

            ShowExpRow(BaseExpText, BaseExpBar, "Base", BaseExpColor, state.Level, state.Exp, state.BaseExpRequired);
            ShowExpRow(JobExpText, JobExpBar, "Job", JobExpColor, state.JobLevel, state.JobExp, state.JobExpRequired);
            Levels.SetActive(true);

            if (state.Party.IsInParty)
                AddEntry("Leave party", LeaveParty);

            OpenMenuAtCursor();
        }

        //rounded down, so it only reads 100% once the level is reached
        private static void ShowExpRow(TextMeshProUGUI text, Slider bar, string kind, string color, int level, int exp, int required)
        {
            var progress = PlayerState.ExpProgress(exp, required);
            if (progress == null)
            {
                ShowLevelRow(text, bar, FormatLevel(kind, color, level), null);
                return;
            }

            var percent = Mathf.Floor(progress.Value * 1000f) / 10f;
            ShowLevelRow(text, bar, $"<color={color}>{kind} Lv. {level}: {exp:N0} / {required:N0} ({percent:F1}%)</color>", progress);
        }

        private static string FormatDetail(string label, string value) => $"<color={LabelColor}>{label}:</color> <color=#FFFFFF>{value}</color>";

        private static string FormatLevel(string kind, string color, int level) => $"<color={color}>{kind} Lv. {level}</color>";

        private static string PlayerDetails(int jobId, string partyName, string mapName = null)
        {
            var details = FormatDetail("Class", ClientDataLoader.Instance.GetJobNameForId(jobId));
            if (!string.IsNullOrWhiteSpace(partyName))
                details += $"\n{FormatDetail("Party", partyName)}";
            if (mapName != null)
                details += $"\n{FormatDetail("Map", mapName)}";
            return details;
        }

        //only the leader gets options; the map is left out for a member clicked in the world
        public void RightClickPartyMember(PartyMemberInfo info, bool showMap = true)
        {
            BeginMenu();

            ShowPartyMemberInfo(info, showMap);

            var state = PlayerState.Instance;
            if (state.Party.IsLeader)
            {
                if (info.Controllable != null)
                    AddPlayerOptions(info.Controllable);
                else
                {
                    var memberId = info.PartyMemberId;
                    AddEntry("Kick from party", () => KickFromParty(memberId));
                }
            }

            OpenMenuAtCursor();
        }

        private void ShowPartyMemberInfo(PartyMemberInfo info, bool showMap)
        {
            if (!info.IsOnline)
            {
                ShowInfo(info.PlayerName, $"<color={LabelColor}>Offline</color>");
                return;
            }

            var mapName = showMap ? ClientDataLoader.Instance.GetFullNameForMap(info.Map) : null;
            ShowInfo(info.PlayerName, PlayerDetails(info.Appearance.JobId, PlayerState.Instance.Party.Name, mapName));
            ShowLevels(FormatLevel("Base", BaseExpColor, info.Level), FormatLevel("Job", JobExpColor, info.JobLevel));
        }

        //the same menu as their party row, which knows their job level
        public void RightClickPlayer(ServerControllable target)
        {
            var state = PlayerState.Instance;
            if (state.EntityId == target.Id || target.CharacterType == CharacterType.PlayerLikeNpc)
                return;

            if (state.Party.TryGetMember(target.Id, out var member))
            {
                RightClickPartyMember(member, false);
                return;
            }

            BeginMenu();
            ShowInfo(target.Name, PlayerDetails(target.Appearance.JobId, target.PartyName));
            //only party members' job levels are sent
            ShowLevels(FormatLevel("Base", BaseExpColor, target.Level));

            AddPlayerOptions(target);

            OpenMenuAtCursor();
        }

        //no options for a monster; race, element and size are left out without client data
        public void RightClickMonster(ServerControllable target)
        {
            BeginMenu();

            var data = ClientDataLoader.Instance.GetMonsterData(target.ClassId);
            var details = data == null ? null
                : $"{FormatDetail("Race", data.Race.ToString())}\n{FormatDetail("Element", GetElementName(data.Element))}"
                  + $"\n{FormatDetail("Size", data.SizeType.ToString())}";
            ShowInfo(target.Name, details);
            ShowLevels(FormatDetail("Level", target.Level.ToString()));

            OpenMenuAtCursor();
        }

        //Water2 shows as "Water 2"
        private static string GetElementName(CharacterElement element)
        {
            var name = element.ToString();
            return char.IsDigit(name[^1]) ? $"{name[..^1]} {name[^1]}" : name;
        }

        private void AddPlayerOptions(ServerControllable target)
        {
            var state = PlayerState.Instance;
            var targetId = target.Id;
            var targetHasParty = !string.IsNullOrWhiteSpace(target.PartyName);

            if (!state.Party.IsInParty)
            {
                if (!targetHasParty)
                    AddEntry("Form a party", () => FormPartyWith(targetId));
                return;
            }

            if (!state.Party.IsLeader)
                return;

            if (target.PartyName == state.Party.Name)
            {
                if (!state.Party.MemberIdLookup.TryGetValue(targetId, out var memberId))
                    return;

                AddEntry("Make party lead", () => PromoteToLeader(memberId));
                AddEntry("Kick from party", () => KickFromParty(memberId));
            }
            else if (!targetHasParty)
                AddEntry("Invite to party", () => InviteToParty(targetId));
        }

        private void LeaveParty()
        {
            if(!PlayerState.Instance.Party.IsInParty)
                CameraFollower.Instance.AppendError("You are not currently in a party.");
            else
                NetworkManager.Instance.LeaveParty();
            HideWindow();
        }

        private void InviteToParty(int targetEntityId)
        {
            NetworkManager.Instance.PartyInviteById(targetEntityId);
            HideWindow();
        }

        private void KickFromParty(int memberId)
        {
            if (!PlayerState.Instance.Party.Members.TryGetValue(memberId, out var info))
                return;
            UiManager.Instance.YesNoOptionsWindow.BeginPrompt($"Kick {info.PlayerName} from your party?", "Yes", "No",
                () => NetworkManager.Instance.PartyUpdateAction(memberId, PartyClientAction.RemovePlayer), null, false);
        }

        private void PromoteToLeader(int memberId)
        {
            if (!PlayerState.Instance.Party.Members.TryGetValue(memberId, out var info))
                return;
            UiManager.Instance.YesNoOptionsWindow.BeginPrompt($"Promote {info.PlayerName} to party leader?", "Yes", "No",
                () => NetworkManager.Instance.PartyUpdateAction(memberId, PartyClientAction.ChangeLeader), null, false);
        }

        private void FormPartyWith(int targetEntityId)
        {
            var state = PlayerState.Instance;
            if (!state.KnownSkills.TryGetValue(CharacterSkill.BasicMastery, out var mastery) || mastery < 6)
            {
                CameraFollower.Instance.AppendError($"You need to have learned Basic Mastery level 6 to form a party.");
                return;
            }

            UiManager.Instance.TextInputWindow.BeginTextInput($"Name your party (must be unique)",
                partyName => FinishCreateParty(partyName, targetEntityId), SharedConfig.MaxPartyName);
        }

        private void FinishCreateParty(string partyName, int targetEntityId)
        {
            NetworkManager.Instance.OrganizeParty(partyName, targetEntityId);
            HideWindow();
        }
        
        public override void ShowWindow()
        {
            gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
            base.ShowWindow();
        }

        public override void HideWindow()
        {
            foreach (var b in activeButtons)
            {
                b.gameObject.SetActive(false);
                b.onClick.RemoveAllListeners();
                unusedButtons.Push(b);
            }
            activeButtons.Clear();

            if (boundary != null)
                boundary.SetActive(false);

            HideInfo();

            if (CameraFollower.Instance.ActivePromptType == PromptType.RightClickMenu)
                CameraFollower.Instance.ActivePromptType = PromptType.None;
            base.HideWindow();
        }

        private void AddEntry(string entryText, UnityAction onClick)
        {
            if (!unusedButtons.TryPop(out var button))
            {
                var go = Instantiate(EntryPrefab, transform);
                button = go.GetComponent<Button>();
            }

            button.GetComponentInChildren<TextMeshProUGUI>(true).text = entryText;
            button.onClick.AddListener(onClick);

            if (activeButtons.Count == 0 && NameText.gameObject.activeSelf)
            {
                if (boundary == null)
                    boundary = Instantiate(BoundaryPrefab, transform);
                boundary.SetActive(true);
                boundary.transform.SetAsLastSibling();
            }

            button.gameObject.transform.SetAsLastSibling();
            button.gameObject.SetActive(true);

            activeButtons.Add(button);
        }

        private void Awake()
        {
            EntryPrefab.SetActive(false);
            BoundaryPrefab.SetActive(false);
            container = transform.parent;
        }
        
        private void Update()
        {
            if (transform != container.GetChild(container.childCount - 1))
                HideWindow();
        }
    }
}