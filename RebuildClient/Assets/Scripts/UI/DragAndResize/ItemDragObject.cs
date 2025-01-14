using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    [Flags]
    public enum ItemDragOrigin
    {
        None,
        HotBar = 1,
        ItemWindow = 2,
        EquipmentWindow = 4,
        ShopWindow = 8,
        StorageWindow = 16
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