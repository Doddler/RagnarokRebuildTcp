using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public class ActionTextDisplay : MonoBehaviour
    {
        public GameObject Container;
        public TextMeshProUGUI TopTextArea;
        public TextMeshProUGUI BottomTextArea;
        public RectTransform TopLayoutGroup;
        public RectTransform BottomLayoutGroup;

        private CharacterSkill activeSkill;
        private int selectedSkillLevel;

        
        public void SetItemTargeting(string itemName, int level)
        {
            Container.SetActive(true);

            TopTextArea.text = itemName;
            BottomTextArea.text = "Select a target.";

            selectedSkillLevel = level;
            
            
            LayoutRebuilder.ForceRebuildLayoutImmediate(TopLayoutGroup);
            LayoutRebuilder.ForceRebuildLayoutImmediate(BottomLayoutGroup);
            
            TopTextArea.ForceMeshUpdate();
            BottomTextArea.ForceMeshUpdate();
            
        }
        
        public void SetSkillTargeting(CharacterSkill skill, int level)
        {
            Container.SetActive(true);

            if (activeSkill == skill && selectedSkillLevel == level)
                return;

            var skillData = ClientDataLoader.Instance.GetSkillData(skill);
            if (skillData.AdjustableLevel)
                TopTextArea.text = $"{skillData.Name} Lv {level}";
            else
                TopTextArea.text = $"{skillData.Name}";

            BottomTextArea.text = "Select a target.";

            activeSkill = skill;
            selectedSkillLevel = level;
            
            
            LayoutRebuilder.ForceRebuildLayoutImmediate(TopLayoutGroup);
            LayoutRebuilder.ForceRebuildLayoutImmediate(BottomLayoutGroup);
            
            TopTextArea.ForceMeshUpdate();
            BottomTextArea.ForceMeshUpdate();
            
        }

        public void EndActionTextDisplay()
        {
            Container.SetActive(false);
        }
    }
}