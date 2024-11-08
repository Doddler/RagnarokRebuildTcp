using Assets.Scripts.Sprites;
using RebuildSharedData.ClientTypes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Hud
{
    public class ItemObtainedToast : MonoBehaviour
    {
        public GameObject Container;
        public RectTransform Rect;
        public TextMeshProUGUI Text;
        public Image Icon;

        private float endTime;

        public void Awake()
        {
            Container.SetActive(false);
        }

        public void SetText(int itemId, int itemCount)
        {
            if (!ClientDataLoader.Instance.TryGetItemById(itemId, out var item))
                return;

            var itemName = item.Slots == 0 ? item.Name : $"{item.Name} [{item.Slots}]";
            
            Icon.sprite = ClientDataLoader.Instance.ItemIconAtlas.GetSprite(item.Sprite);
            Text.text = $"{itemName} - {itemCount} obtained.";
            
            Container.SetActive(true);
            endTime = Time.timeSinceLevelLoad + 3f;
            
            LayoutRebuilder.ForceRebuildLayoutImmediate(Rect);
            Text.ForceMeshUpdate();
        }

        public void Update()
        {
            if (endTime <= Time.timeSinceLevelLoad)
            {
                Container.SetActive(false);
                return;
            }
        }
    }
}