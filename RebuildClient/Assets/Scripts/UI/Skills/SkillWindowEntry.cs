using System;
using System.Collections.Generic;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public class SkillWindowEntry : UIBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Button LevelUpButton;
        public GameObject LevelAdjustArea;
        public GameObject LevelViewArea;
        public GameObject AdjustDownButton;
        public GameObject AdjustUpButton;
        public TextMeshProUGUI SkillName;
        public TextMeshProUGUI TextCurLevel;
        public TextMeshProUGUI TextMaxLevel;
        public TextMeshProUGUI SPCost;
        public Image Icon;
        [NonSerialized] public CharacterSkill SkillId;
        [NonSerialized] public int CurrentLevel;
        [NonSerialized] public int MaxLevel;
        [NonSerialized] public bool IsPassive;
        [NonSerialized] public int SkillRank;
        [NonSerialized] public ClientPrereq[] Requirements;

        private SkillData data;
        private SkillWindow parent;
        private SkillDragSource dragSource;
        private Image background;
        private bool availableToLevelUp;

        public void ChangeSelectedLevel(int dir)
        {
            CurrentLevel = Mathf.Clamp(CurrentLevel + dir, 1, MaxLevel);
            TextCurLevel.text = CurrentLevel.ToString();
            dragSource.ItemCount = CurrentLevel;
            RefreshSpCost();
        }

        public void ReleaseHighlightSkillBox()
        {
            background.color = new Color(0, 0.32f, 1f, 0f);
        }

        public void HighlightSkillBox()
        {
            parent.HighlightedEntry = this;
            background.color = new Color(0, 0.32f, 1f, 0.25f);
        }

        public void RefreshLevelText()
        {
            TextCurLevel.text = CurrentLevel.ToString();
            TextMaxLevel.text = MaxLevel.ToString();
        }

        public void RefreshAdjustLevelButtons()
        {
            if (CurrentLevel <= 1)
                AdjustDownButton.SetActive(false);
            if (CurrentLevel == MaxLevel)
                AdjustUpButton.SetActive(false);
        }

        public void RefreshSpCost()
        {
            if (MaxLevel <= 0)
            {
                SPCost.text = "";
                return;
            }

            if (data.Target == SkillTarget.Passive)
            {
                SPCost.text = CurrentLevel > 0 ? "Passive" : "";
                return;
            }

            if (data.SpCost == null)
            {
                SPCost.text = "Sp: ???";
                return;
            }

            var level = CurrentLevel > data.SpCost.Length ? data.SpCost.Length : CurrentLevel;
            if (level > 0)
                SPCost.text = CurrentLevel > 0 ? $"Sp: {data.SpCost[level - 1]}" : "";
            else
                SPCost.text = "Sp: 0";
        }

        public void UpdateLevelUpButton(bool hasPointsToSpend, bool isAllowedToSpendPoints)
        {
            if (!hasPointsToSpend || MaxLevel >= data.MaxLevel || !availableToLevelUp)
            {
                LevelUpButton.gameObject.SetActive(false);
                return;
            }

            LevelUpButton.gameObject.SetActive(true);
            LevelUpButton.interactable = isAllowedToSpendPoints;
            if (isAllowedToSpendPoints)
                LevelUpButton.image.material = null;
            else
                LevelUpButton.image.material = parent.GrayScaleMaterial;
        }

        public void UpdateLevel(int newLevel, bool meetsRequirements) => Init(parent, data, newLevel, meetsRequirements, false);

        public void Init(SkillWindow skillWindow, SkillData skillData, int learnedLevel, bool hasRequiredSkills, bool isFullReset = true)
        {
            background = GetComponent<Image>();
            
            parent = skillWindow;
            SkillName.text = skillData.Name;
            data = skillData;
            SkillId = data.SkillId;
            MaxLevel = learnedLevel;
            CurrentLevel = MaxLevel;
            Icon.sprite = skillWindow.SkillAtlas.GetSprite(skillData.Icon);
            if(Icon.sprite != null)
                ((RectTransform)Icon.transform).sizeDelta = Icon.sprite.rect.size * 2;
            
            availableToLevelUp = hasRequiredSkills;
            if (!hasRequiredSkills || learnedLevel == 0)
            {
                var textPos = (RectTransform)SkillName.transform;
                textPos.anchoredPosition = new Vector2(textPos.anchoredPosition.x, -12f);
                LevelViewArea.SetActive(false);
                if (!hasRequiredSkills)
                {
                    Icon.material = this.parent.GrayScaleMaterial;
                    Icon.color = new Color(1f, 1f, 1f, 0.4f);
                }
                else
                {
                    Icon.material = Graphic.defaultGraphicMaterial;
                    Icon.color = new Color(1f, 1f, 1f, 0.8f);
                }
            }
            else
            {
                var textPos = (RectTransform)SkillName.transform;
                textPos.anchoredPosition = new Vector2(textPos.anchoredPosition.x, 0f);
                LevelViewArea.SetActive(true);
                Icon.material = Graphic.defaultGraphicMaterial;
                Icon.color = Color.white;
            }

            LevelAdjustArea.SetActive(skillData.AdjustableLevel && CurrentLevel > 1);
            LevelUpButton.onClick.RemoveAllListeners();
            LevelUpButton.onClick.AddListener(() => this.parent.SkillLevelUp(skillData.SkillId));

            RefreshLevelText();
            RefreshSpCost();

            dragSource = GetComponent<SkillDragSource>();
            dragSource.Assign(DragItemType.Skill, Icon.sprite, (int)skillData.SkillId, learnedLevel);
            dragSource.enabled = learnedLevel > 0;
            if (isFullReset)
            {
                Requirements = null;
                transform.SetAsLastSibling();
            }
        }

        public void HoverTooltip()
        {
            if (!Input.GetMouseButton(0) && !CameraFollower.Instance.HasSkillOnCursor) //if we're not dragging anything basically
                parent.ShowTooltip(data.SkillId, this);
        }

        public void LeaveHoverTooltip()
        {
            parent.HideTooltip();
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            HighlightSkillBox();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ReleaseHighlightSkillBox();
        }
    }
}