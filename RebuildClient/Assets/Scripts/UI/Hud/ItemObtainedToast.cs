using Assets.Scripts.PlayerControl;
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

        public void SetText(InventoryItem inventoryItem, int itemCount)
        {
            //var itemName = item.Slots == 0 ? item.Name : $"{item.Name} [{item.Slots}]";
            var obtainedText = $"{inventoryItem.ProperName()} - {itemCount} obtained.";
            
            if(itemCount == 1)
                CameraFollower.Instance.AppendChatText($"<color=#00fbfb>You got {inventoryItem.ProperName()}.</color>");
            else
                CameraFollower.Instance.AppendChatText($"<color=#00fbfb>You got {itemCount}x {inventoryItem.ProperName()}.</color>");
            
            Icon.sprite = ClientDataLoader.Instance.ItemIconAtlas.GetSprite(inventoryItem.ItemData.Sprite);
            Icon.rectTransform.sizeDelta = Icon.sprite.rect.size * 2;
            Text.text = obtainedText;
            
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