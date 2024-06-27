using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public enum ItemDragOrigin
    {
        None,
        HotBar,
        ItemWindow
    }
    
    public class ItemDragObject : DragItemBase
    {
        public ItemDragOrigin Origin;
        public int OriginId;
        
        public void Update()
        {
            transform.position = Input.mousePosition;
        }
        
    }
}