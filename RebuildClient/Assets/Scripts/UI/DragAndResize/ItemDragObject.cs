using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public enum ItemDragOrigin
    {
        None,
        HotBar,
        ItemWindow,
        EquipmentWindow,
        ShopWindow
    }
    
    public class ItemDragObject : DragItemBase
    {
        public ItemDragOrigin Origin { get; set; }
        public int OriginId;
        
        public void Update()
        {
            transform.position = Input.mousePosition;
        }
        
    }
}