using Assets.Scripts.Sprites;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RebuildSharedData.Data;

namespace Assets.Scripts.UI.TitleScreen
{
    public class CharacterSelectPlayerButton : UIBehaviour, IPointerClickHandler
    {
        public CharacterSelectWindow Parent;
        public Button Button;
        public TextMeshProUGUI UnavailableText;
        public UiPlayerSprite PlayerSprite;

        public Sprite SelectedSprite;
        public Sprite UnselectedSprite;

        private ClientCharacterSummary characterSummary;

        public void SetAsUnavailable()
        {
            UnavailableText.gameObject.SetActive(true);
            var spriteState = Button.spriteState;
            spriteState.disabledSprite = UnselectedSprite;
            Button.spriteState = spriteState;
            Button.interactable = false;
        }

        public void PrepareEmptySlot(bool isSelected)
        {
            UnavailableText.gameObject.SetActive(false);
            var spriteState = Button.spriteState;
            spriteState.disabledSprite = SelectedSprite;
            Button.spriteState = spriteState;
            Button.interactable = !isSelected;
            PlayerSprite.gameObject.SetActive(false);
        }

        public void PrepareCharacterEntry(ClientCharacterSummary summary, bool isSelected)
        {
            if (summary == null)
            {
                PrepareEmptySlot(isSelected);
                return;
            }

            characterSummary = summary;
            var spriteState = Button.spriteState;
            spriteState.disabledSprite = SelectedSprite;
            Button.spriteState = spriteState;
            Button.interactable = !isSelected;

            if (summary.SummaryData == null)
            {
                characterSummary = summary;
                UnavailableText.gameObject.SetActive(true);
                UnavailableText.text = "Appearance Data Unavailable";
                return;
            }

            UnavailableText.gameObject.SetActive(false);
            PlayerSprite.gameObject.SetActive(true);
            PlayerSprite.PrepareDisplayPlayerCharacter(PlayerAppearance.FromSummary(summary.SummaryData));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.clickCount >= 2)
                Parent.ClickOk();
        }
    }
}