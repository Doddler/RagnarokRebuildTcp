using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI.TitleScreen
{
    public class CharacterCreatorWindow : UIBehaviour
    {
        public TitleScreen Parent;
        public GameObject Pane;

        public Button[] ColorButtons;
        public Button[] StatUpButtons;
        public Button[] StatDownButtons;
        public Button[] GenderButtons;
        public TextMeshProUGUI[] StatTexts;
        public Button SaveButton;
        
        public int hairColor;
        public int hairStyle;
        public bool isGenderMale;

        public UiPlayerSprite PlayerSprite;
        public StatGimbal StatGimbal;
        
        public TextMeshProUGUI StatsRemainingText;
        public TMP_InputField PlayerNameText;

        private int[] statValues = new int[6];
        private int statsRemaining;

        private Direction Direction = Direction.South;

        private int[] gimbalLookup = { 0, 5, 1, 3, 4, 2 };

        private const int TotalStatPoints = 33;
        private const int StartingStatVal = 5;
        private const int MaxStartingStat = 9;
        private const int MaxHairId = 19;

        public void AddStat(int statId) => UpdateStats(statId, 1);
        public void SubStat(int statId) => UpdateStats(statId, -1);

        private void UpdateStats(int statId, int change)
        {
            if (statValues[statId] + change > MaxStartingStat || statValues[statId] + change < 1 || statsRemaining - change < 0)
                return;
            
            
            statValues[statId] += change;
            statsRemaining -= change;
            
            StatGimbal.StatValues[gimbalLookup[statId]] = statValues[statId];
            StatGimbal.Refresh();

            //StatUpButtons[statId].interactable = statValues[statId] < 10;
            StatDownButtons[statId].interactable = statValues[statId] > 1;
            StatTexts[statId].text = statValues[statId].ToString();
            StatsRemainingText.text = statsRemaining.ToString();

            if (statsRemaining <= 0)
                for (var i = 0; i < 6; i++)
                    StatUpButtons[i].interactable = false;
            else
                for (var i = 0; i < 6; i++)
                    StatUpButtons[i].interactable = statValues[i] < MaxStartingStat;
            
        }

        public void Awake()
        {
            Pane.SetActive(false);
            statsRemaining = TotalStatPoints;
            for (var i = 0; i < StatTexts.Length; i++)
            {
                StatTexts[i].text = $"{StartingStatVal}";
                statValues[i] = StartingStatVal;
                statsRemaining -= statValues[i];
            }

            isGenderMale = true;
            
            PlayerSprite.PrepareDisplayPlayerCharacter(0, hairStyle, hairColor, 0, 0, 0, isGenderMale);

            StatsRemainingText.text = statsRemaining.ToString();
        }
        
        public void Open()
        {
            Pane.SetActive(true);
            Parent.TitleState = TitleScreen.TitleScreenState.CharacterCreation;
        }
        
        public void HidePane()
        {
            Pane.SetActive(false);
        }

        public void ChangeHairColor(int id)
        {
            ColorButtons[hairColor].interactable = true;
            ColorButtons[id].interactable = false;
            hairColor = id;
            
            PlayerSprite.PrepareDisplayPlayerCharacter(0, hairStyle, hairColor, 0, 0, 0, isGenderMale);
        }

        public void ChangeHair(bool isLeft)
        {
            var change = isLeft ? 1 : -1;
            hairStyle += change;
            if (hairStyle < 0)
                hairStyle = MaxHairId;
            if (hairStyle > MaxHairId)
                hairStyle = 0;
            
            PlayerSprite.PrepareDisplayPlayerCharacter(0, hairStyle, hairColor, 0, 0, 0, isGenderMale);
        }

        public void ChangeGender(bool isMale)
        {
            isGenderMale = isMale;
            GenderButtons[0].interactable = !isMale;
            GenderButtons[1].interactable = isMale;
            PlayerSprite.PrepareDisplayPlayerCharacter(0, hairStyle, hairColor, 0, 0, 0, isGenderMale);
        }
        
        public void TurnCharacter(bool isLeft)
        {
            var change = isLeft ? 1 : -1;
            if ((int)Direction + change > (int)Direction.SouthEast)
                Direction = Direction.South;
            else if ((int)Direction + change < 0)
                Direction = Direction.SouthEast;
            else
                Direction = (Direction)((int)Direction + change);
            
            PlayerSprite.ChangeDirection(Direction);
        }

        public void CancelCreate()
        {
            Parent.ReturnToCharacterSelect();
        }

        public void SubmitCreate()
        {
            if (PlayerNameText.text.Length <= 0 || statsRemaining != 0)
                return;
            if (PlayerNameText.text.Length > 30)
            {
                Parent.ErrorMessage("Character name must be 30 or fewer characters in length.");
                return;
            }
            NetworkManager.Instance.SendEnterServerNewCharacterMessage(PlayerNameText.text, Parent.SelectedSlot, hairStyle, hairColor, statValues, isGenderMale);
            Parent.LastTitleState = TitleScreen.TitleScreenState.CharacterCreation;
            Parent.TitleState = TitleScreen.TitleScreenState.Waiting;
            gameObject.SetActive(false);
        }

        public void Update()
        {
            SaveButton.interactable = PlayerNameText.text.Length > 0 && statsRemaining <= 0;
        }
    }
}