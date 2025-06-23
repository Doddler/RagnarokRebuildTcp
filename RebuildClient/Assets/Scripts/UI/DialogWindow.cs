using Assets.Scripts.Network;
using Assets.Scripts.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    internal class DialogWindow : WindowBase, IPointerDownHandler
    {
        public TextMeshProUGUI NameBox;
        public TextMeshProUGUI TextBox;
        public Image NpcImage;

        public void SetDialog(string name, string text)
        {
            if(string.IsNullOrWhiteSpace(name))
                NameBox.text = name;
            else
                NameBox.text = $"[{name}]";

            TextBox.text = text;
            
            gameObject.SetActive(true);
            MoveToTop();
            CanCloseWithEscape = false;
        }

        public void ShowImage(string sprite)
        {
            AddressableUtility.LoadSprite(gameObject, "Assets/Sprites/Cutins/" + sprite + ".png", sprite =>
            {
                NpcImage.sprite = sprite;
                NpcImage.SetNativeSize();
                NpcImage.gameObject.SetActive(true);
            });
        }

        public void MakeBig()
        {
            transform.RectTransform().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 1100);
            // transform.RectTransform().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 550);
        }

        public void MakeNormalSize()
        {
            transform.RectTransform().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 800);
            // transform.RectTransform().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 230);
        }

        public void HideUI()
        {
            NpcImage.gameObject.SetActive(false);
            gameObject.SetActive(false);
            NameBox.text = "";
            TextBox.text = "";
            UiManager.Instance.WindowStack.Remove(this);
        }
        public new void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            NetworkManager.Instance.SendNpcAdvance();
        }
    }
}
