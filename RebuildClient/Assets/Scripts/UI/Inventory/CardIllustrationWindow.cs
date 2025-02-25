using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using RebuildSharedData.ClientTypes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Inventory
{
    public class CardIllustrationWindow : WindowBase
    {
        public Image CardImage;
        public TextMeshProUGUI WindowName;

        public void DisplayCard(ItemData data)
        {
            WindowName.text = data.Name;
            var illustration = $"Assets/Sprites/Imported/Collections/cardart_{data.Code}.png";
            AddressableUtility.LoadSprite(gameObject, illustration, DisplayDescription, () =>
            {
                AddressableUtility.LoadSprite(gameObject, "Assets/Sprites/Imported/Collections/cardart_default.png", DisplayDescription);
            });        
        }
        
        private void DisplayDescription(Sprite collection)
        {
            CardImage.sprite = collection;
                
            ShowWindow();
            MoveToTop();
        }
    }
}