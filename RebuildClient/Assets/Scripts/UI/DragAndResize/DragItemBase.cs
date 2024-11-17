using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public class DragItemBase : MonoBehaviour
    {
        public Image Image;
        public CanvasGroup CanvasGroup;
        public TextMeshProUGUI CountText;
        public DragItemType Type;
        public Sprite Sprite;
        public int ItemId;
        public int ItemCount;

        public void Assign(DragItemBase orig) => Assign(orig.Type, orig.Sprite, orig.ItemId, orig.ItemCount);
        
        public void Assign(DragItemType type, Sprite sprite, int itemId, int count)
        {
            Type = type;
            Sprite = sprite;
            ItemId = itemId;
            UpdateCount(count);

            if (Image != null)
            {
                Image.sprite = sprite;
                Image.rectTransform.sizeDelta = Sprite.rect.size * 2;
            }
        }

        public void HideCount()
        {
            CountText.gameObject.SetActive(false);
        }

        public void SetEquipped()
        {
            CountText.gameObject.SetActive(true);
            CountText.text = "E";
        }

        public void UpdateCount(int count)
        {
            // Debug.Log($"Update item count for {ItemId} to {count}");
            ItemCount = count;
            if (CountText != null)
            {
                if ((count <= 0 && Type == DragItemType.Skill) || Type == DragItemType.Equipment)
                    CountText.gameObject.SetActive(false);
                else
                {
                    CountText.gameObject.SetActive(true);
                    CountText.text = count.ToString();
                }
            }
        }

        public void Clear()
        {
            Type = DragItemType.None;
            gameObject.SetActive(false);
        }
    }
}