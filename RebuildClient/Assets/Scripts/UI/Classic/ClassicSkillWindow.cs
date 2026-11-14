using System.Collections.Generic;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using RO_Flex_UI.Components;
using RO_Flex_UI.Panels;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Classic
{
    public class ClassicSkillWindow : Window, IStyledWindow
    {
        [SerializeField] private DraggableSkill skillEntryPrefab;
        [SerializeField] private ListPanel firstJob;
        public SpriteAtlas SkillAtlas;

        public override void ShowWindow()
        {
            base.ShowWindow();
            UiManagerV2.Instance?.TrackOpen(this);
        }

        public override void HideWindow()
        {
            UiManagerV2.Instance?.TrackClosed(this);
            base.HideWindow();
        }

        protected override void Awake()
        {
            base.Awake();
            // Only deactivate an in-scene template, never the source prefab asset.
            if (skillEntryPrefab != null && skillEntryPrefab.gameObject.scene.IsValid())
                skillEntryPrefab.gameObject.SetActive(false);
            UiManagerV2.Instance.RegisterWindow(WindowID.SKILLS, this, KeyCode.S);
        }

        private void Start()
        {
            PlayerState.Instance.OnSkillsChanged += RefreshSkills;
            RefreshSkills();
            HideWindow();
        }

        private void OnDestroy()
        {
            PlayerState.Instance.OnSkillsChanged -= RefreshSkills;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
        }

        public void RefreshSkills()
        {
            firstJob.Clear();
            Debug.Log("Starting to list granted skills.");
            var skills = PlayerState.Instance.KnownSkills;
            foreach (var skill in skills)
            {
                var skillId = skill.Key;
                var currLv = skill.Value;
                var data = ClientDataLoader.Instance.GetSkillData(skillId);

                Debug.Log($"NameLabel: {data.Name} {currLv}");
                // Passive skills (for example Basic Mastery) have no SP-cost array.
                // Resolve values before cloning so invalid input cannot leave an inactive orphan.
                var passive = data.Target == SkillTarget.Passive;
                var cost = !passive && data.SpCost != null && currLv > 0 && currLv <= data.SpCost.Length
                    ? data.SpCost[currLv - 1]
                    : 0;
                var icon = SkillAtlas.GetSprite(data.Icon);
                if (icon == null)
                    icon = SkillAtlas.GetSprite("skill_nv_basic");
                var entry = Instantiate(skillEntryPrefab, firstJob.transform);
                entry.IsPassive = passive;
                entry.IsFixedLevel = !data.AdjustableLevel;
                entry.Assign(
                    icon,
                    data.Name,
                    currLv,
                    data.MaxLevel,
                    cost,
                    passive,
                    passive
                );
                entry.gameObject.SetActive(true);
                firstJob.AddItem(entry.GetComponent<ListItem>());
            }
        }
    }
}
