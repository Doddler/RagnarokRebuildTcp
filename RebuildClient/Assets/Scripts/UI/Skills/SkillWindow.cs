using System;
using System.Collections.Generic;
using System.Text;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.ConfigWindow;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public class SkillWindow : WindowBase
    {
        public SkillWindowEntry TemplateObject;
        public Sprite LevelUpNormal;
        public SpriteAtlas SkillAtlas;
        public Toggle LockLevelUpButton;
        public Material GrayScaleMaterial;
        public RectTransform TooltipBox;
        public TextMeshProUGUI TooltipText;
        public TextMeshProUGUI PointsText;
        public AutoHeightFitter TooltipResizeArea;
        public List<Button> Tabs;

        [NonSerialized] public SkillWindowEntry HighlightedEntry;

        private Transform SkillContainer;

        private List<SkillWindowEntry> Entries;
        private bool lockSkillLevelUp;
        private bool isInitialized;
        private int skillPoints;
        private bool lastHasCursorSkill;
        private float tooltipWidth;
        private string tooltipTextTemplate;
        private int selectedRank;
        private int maxRank;
        private StringBuilder tooltipBuilder = new();

        public void Update()
        {
            if (CameraFollower.Instance.HasSkillOnCursor != lastHasCursorSkill)
            {
                lastHasCursorSkill = CameraFollower.Instance.HasSkillOnCursor;
                if (!lastHasCursorSkill && HighlightedEntry != null)
                {
                    HighlightedEntry.ReleaseHighlightSkillBox();
                    HighlightedEntry = null;
                }
            }
        }
        
        public void ShowTooltip(CharacterSkill skill, SkillWindowEntry entry)
        {
            var requiredSkills = entry.Requirements;
            var data = ClientDataLoader.Instance.GetSkillData(skill);

            tooltipBuilder.Clear();

            tooltipBuilder.Append($"{data.Name}\n");
            tooltipBuilder.Append($"<size=-4>Prerequisites: ");
            
            if (requiredSkills == null || requiredSkills.Length <= 0 || entry.SkillRank == -1)
                tooltipBuilder.Append("<color=#4444FF>None</color>");
            else
            {
                for (var i = 0; i < requiredSkills.Length; i++)
                {
                    if (i > 0)
                        tooltipBuilder.Append(", ");
                    var name = ClientDataLoader.Instance.GetSkillName(requiredSkills[i].Skill);
                    if (NetworkManager.Instance.PlayerState.KnownSkills.TryGetValue(requiredSkills[i].Skill, out var lvl) && requiredSkills[i].Level <= lvl)
                        tooltipBuilder.Append($"<color=#4444FF>{name} {requiredSkills[i].Level}</color>");
                    else
                        tooltipBuilder.Append($"<color=#FF4444>{name} {requiredSkills[i].Level}</color>");
                }
            }
            
            if(!string.IsNullOrWhiteSpace(data.Description))
            tooltipBuilder.Append($"\n<line-height=5>\n</line-height>{data.Description}");

            TooltipText.text = tooltipBuilder.ToString();

            
            // TooltipText.text = tooltipTextTemplate.Replace("{SkillName}", data.Name)
            //                                       .Replace("{Prereqs}", "<color=#4444FF>None</color>")
            //                                       .Replace("{Description}","A skill.");

            var rect = (RectTransform)transform;
            
            var x = rect.sizeDelta.x;
            if (rect.position.x + (rect.sizeDelta.x + tooltipWidth) * rect.lossyScale.x > Screen.width)
                x = -TooltipBox.sizeDelta.x;
            // Debug.Log($"{entry.transform.localPosition.y} - 60 + {SkillContainer.localPosition.y}");
            var y = entry.transform.localPosition.y - 60 + SkillContainer.localPosition.y;

            TooltipBox.localPosition = new Vector3(x, y, TooltipBox.localPosition.z);
            
            TooltipBox.gameObject.SetActive(true);
            TooltipText.ForceMeshUpdate();
            
            Vector2 preferredDimensions = TooltipText.GetPreferredValues(280, 0); //300 minus 20 for margins
            TooltipBox.sizeDelta = new Vector2(300, preferredDimensions.y);
            //
            // LayoutRebuilder.ForceRebuildLayoutImmediate(TooltipBox);
            //
            
            // TooltipResizeArea.UpdateRectSize();
        }

        public void HideTooltip()
        {
            TooltipBox.gameObject.SetActive(false);
        }

        public void SkillLevelUp(CharacterSkill skillId)
        {
            Debug.Log($"Skill Level UP " + skillId);
            EventSystem.current.SetSelectedGameObject(null);
            HighlightedEntry = null;
            NetworkManager.Instance.SendApplySkillPoint(skillId);
        }

        public void ApplySkillUpdateFromServer(CharacterSkill skillId, int level)
        {
            var state = NetworkManager.Instance.PlayerState;
            for (var i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];

                var hasPrereqs = entry.SkillRank >= 0 && CheckSkillPrereqs(entry.Requirements, state);
                    
                if (Entries[i].SkillId == skillId)
                    Entries[i].UpdateLevel(level, hasPrereqs);
                else
                    Entries[i].UpdateLevel(entry.MaxLevel, hasPrereqs);
            }
        }
        
        public SkillWindowEntry AddSkillToSkillWindow(CharacterSkill skill, int currentLevel, int rank, bool meetsPrereqs, SkillWindowEntry existing = null)
        {
            var data = ClientDataLoader.Instance.GetSkillData(skill);

            if (data.MaxLevel == 0)
            {
                Debug.LogWarning($"Could not add skill {data.Name}, it has no max level set.");
                if (existing != null)
                    existing.SkillName.text = "!!ERROR!!";
                return existing;
            }

            var newSkill = existing;
            if(newSkill == null)
                newSkill = GameObject.Instantiate(TemplateObject, SkillContainer);
            newSkill.gameObject.SetActive(true);
            // var level = maxLevel;
            newSkill.Init(this, data, currentLevel, meetsPrereqs);
            newSkill.SkillRank = rank;
            Entries.Add(newSkill);
            
            return newSkill;
        }

        public void Initialize()
        {
            if (GameConfig.Data.AutoLockSkillWindow)
            {
                lockSkillLevelUp = true;
                LockLevelUpButton.SetIsOnWithoutNotify(lockSkillLevelUp);
            }

            if (isInitialized)
                UpdateSkillPointsAndLock();

            if (isInitialized)
                return;

            Entries = new List<SkillWindowEntry>();
            SkillContainer = TemplateObject.transform.parent;

            isInitialized = true;
            UpdateSkillPointsAndLock();
        }

        public override void ShowWindow()
        {
            base.ShowWindow();
            Initialize();
        }

        public void ChangeTab(int tabRank)
        {
            //unselect the old tab (by making it interactable)
            var index = selectedRank < 0 ? Tabs.Count - 1 : selectedRank;
            Tabs[index].interactable = true;
            
            selectedRank = tabRank;
            
            //select the new tab 
            index = selectedRank < 0 ? Tabs.Count - 1 : selectedRank;
            Tabs[index].interactable = false;
            
            for (var i = 0; i < Entries.Count; i++)
                Entries[i].gameObject.SetActive(Entries[i].SkillRank == selectedRank);
        }

        private int existingEntries;
        private HashSet<CharacterSkill> activeSkills = new();
        private List<SkillWindowEntry> oldEntries = new();

        private bool CheckSkillPrereqs(ClientPrereq[] prereqs, PlayerState state)
        {
            if (prereqs == null || prereqs.Length <= 0)
                return true;
            for (var i = 0; i < prereqs.Length; i++)
            {
                var learnedLevel = state.KnownSkills.GetValueOrDefault(prereqs[i].Skill, -1);
                if (prereqs[i].Level > learnedLevel)
                    return false;
            }

            return true;
        }
        
        private SkillWindowEntry GrabEntryToReuse()
        {
            if (existingEntries > 0)
            {
                existingEntries--;
                return oldEntries[existingEntries];
            }

            return null; //it'll make a new entry automatically for us if we return null here
        }

        private void PopulateSkillTreeCategory(List<ClientSkillTreeEntry> skills, int rank, PlayerState state)
        {
            foreach (var skill in skills)
            {
                var entry = GrabEntryToReuse(); //this could be null, but AddSkillToSkillWindow handles it gracefully
                var currentLevel = 0;
                var skillData = ClientDataLoader.Instance.GetSkillData(skill.Skill);
                var hasReq = CheckSkillPrereqs(skill.Prerequisites, state);
                if (hasReq)
                    currentLevel = state.KnownSkills.GetValueOrDefault(skill.Skill, 0);
                entry = AddSkillToSkillWindow(skill.Skill, currentLevel, rank, hasReq, entry);
                entry.Requirements = skill.Prerequisites;
                activeSkills.Add(skill.Skill);
                if (maxRank < rank)
                    maxRank = rank;
            }
        }

        private void PopulateUnrankedSkills(PlayerState state)
        {
            var hasUnranked = false;
            foreach (var skill in state.KnownSkills)
            {
                if (!activeSkills.Contains(skill.Key))
                {
                    var entry = GrabEntryToReuse();
                    AddSkillToSkillWindow(skill.Key, skill.Value, -1, true, entry);
                    hasUnranked = true;
                }
            }
            if(hasUnranked)
                Tabs[^1].gameObject.SetActive(true);
        }
        
        public void UpdateAvailableSkills()
        {
            Initialize();
            HighlightedEntry = null; //safety first
            activeSkills.Clear();
            maxRank = 0;
            
            var state = NetworkManager.Instance.PlayerState;
            var id = state.JobId;
            var tree = ClientDataLoader.Instance.GetSkillTree(id);
            
            // Debug.Log($"Loading skill tree data for job {id}");
            
            for (var i = 0; i < Tabs.Count; i++)
            {
                Tabs[i].interactable = true;
                Tabs[i].gameObject.SetActive(false);
            }

            if (tree == null)
            {
                //since we have no tree we need to dispose of all the skills currently in the window
                for(var i = 0; i < Entries.Count; i++)
                    Destroy(Entries[i].gameObject);
                Entries.Clear();

                return;
            }
            
            oldEntries.Clear();
            for(var i = 0; i < Entries.Count; i++)
                oldEntries.Add(Entries[i]);
            
            Entries.Clear();
            existingEntries = oldEntries.Count;

            PopulateSkillTreeCategory(tree.Skills, tree.JobRank, state);
            
            while (tree.ExtendsClass >= 0)
            {
                tree = ClientDataLoader.Instance.GetSkillTree(tree.ExtendsClass);
                PopulateSkillTreeCategory(tree.Skills, tree.JobRank, state);
            }
            
            for(var i = 0; i <= maxRank; i++)
                Tabs[i].gameObject.SetActive(true);

            PopulateUnrankedSkills(state);

            //we weren't able to reuse as many entries as we have, so we'll turf the rest
            if (existingEntries > 0)
            {
                //only destroy the entries we haven't reused
                for (var i = existingEntries - 1; i >= 0; i--)
                    Destroy(oldEntries[i].gameObject);
                oldEntries.Clear();
            }

            UpdateSkillPointsAndLock();
            ChangeTab(maxRank);
        }

        private void UpdateSkillPointsAndLock()
        {
            var points = NetworkManager.Instance.PlayerState.SkillPoints; 
            
            PointsText.text = $"Skill Points {points}";
            for (var i = 0; i < Entries.Count; i++)
            {
                Entries[i].UpdateLevelUpButton(points > 0, !lockSkillLevelUp);
            }
        }

        public void RefreshSkillAvailability() => UpdateSkillPointsAndLock();

        public void ToggleSkillLock()
        {
            lockSkillLevelUp = !lockSkillLevelUp;
            UpdateSkillPointsAndLock();
        }

        public void Awake()
        {
            TemplateObject.gameObject.SetActive(false);
            tooltipWidth = TooltipBox.sizeDelta.x;
            tooltipTextTemplate = TooltipText.text;
            TooltipBox.gameObject.SetActive(false);
        }
    }
}