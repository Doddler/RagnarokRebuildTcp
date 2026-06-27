using Assets.Scripts.Sprites;
using System;
using Assets.Scripts.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Utility
{
    public class TabButtonVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image tabImage;
        [SerializeField] private Image childImage;

        private TabGroupVisual group;
        private Sprite fallbackIcon;
        private bool fallbackIconCaptured;
        private int iconRequestVersion;
        private bool isHovered;
        private bool isSelected;

        public Button Button => button;

        private void Reset()
        {
            button = GetComponent<Button>();
            tabImage = GetComponent<Image>();

            var images = GetComponentsInChildren<Image>(true);
            foreach (var image in images)
            {
                if (image.gameObject == gameObject)
                    continue;

                childImage = image;
                break;
            }
        }

        public void Initialize(TabGroupVisual owner)
        {
            group = owner;

            CaptureFallbackIcon();

            button.onClick.RemoveListener(SelectThisTab);
            button.onClick.AddListener(SelectThisTab);

            UpdateVisual();
        }

        public void SetIcon(Sprite icon)
        {
            CaptureFallbackIcon();
            iconRequestVersion++;
            ApplyIcon(icon);
        }

        public void SetIconFromItemAtlas(string spriteName)
        {
            CaptureFallbackIcon();
            iconRequestVersion++;
            ApplyIcon(null);

            if (string.IsNullOrWhiteSpace(spriteName))
                return;

            var icon = ClientDataLoader.Instance?.GetIconAtlasSprite(spriteName);
            if (icon == null)
            {
                Debug.LogWarning($"Unable to set tab icon: Item atlas sprite {spriteName} was not found.");
                return;
            }

            ApplyIcon(icon);
        }

        public void SetIconFromRoSprite(string spriteAddress, string embeddedSpriteName = null)
        {
            CaptureFallbackIcon();
            var requestVersion = ++iconRequestVersion;
            ApplyIcon(null);

            if (string.IsNullOrWhiteSpace(spriteAddress))
                return;

            AddressableUtility.LoadRoSpriteData(gameObject, spriteAddress, spriteData =>
            {
                if (requestVersion != iconRequestVersion)
                    return;

                if (spriteData?.Sprites == null || spriteData.Sprites.Length == 0)
                {
                    Debug.LogWarning($"Unable to set tab icon: {spriteAddress} contains no sprites.");
                    return;
                }

                var icon = spriteData.Sprites[0];
                if (!string.IsNullOrWhiteSpace(embeddedSpriteName))
                {
                    icon = Array.Find(spriteData.Sprites, sprite => sprite != null && sprite.name == embeddedSpriteName);
                    if (icon == null)
                    {
                        Debug.LogWarning($"Unable to set tab icon: {embeddedSpriteName} was not found in {spriteAddress}.");
                        return;
                    }
                }

                ApplyIcon(icon);
            });
        }

        private void CaptureFallbackIcon()
        {
            if (fallbackIconCaptured)
                return;

            fallbackIcon = childImage.sprite;
            fallbackIconCaptured = true;
        }

        private void ApplyIcon(Sprite icon)
        {
            childImage.sprite = icon != null ? icon : fallbackIcon;
        }

        public void SetSelected(bool selected, TabGroupVisual owner)
        {
            group = owner;
            isSelected = selected;

            button.interactable = !selected;

            UpdateVisual();
        }

        public void SelectThisTab()
        {
            group?.SelectTab(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            UpdateVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            var iconMaterial = group.GetIconMaterial(isSelected, isHovered);

            tabImage.sprite = group.GetTabSprite(isSelected);
            childImage.material = iconMaterial;

            if (childImage.TryGetComponent<UiPlayerSprite>(out var playerSprite))
                playerSprite.SetMaterial(iconMaterial);
        }
    }
}
