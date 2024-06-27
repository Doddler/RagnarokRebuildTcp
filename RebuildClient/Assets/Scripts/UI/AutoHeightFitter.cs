using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    [ExecuteInEditMode]
    public class AutoHeightFitter : UIBehaviour
    {
        public RectTransform[] MatchElements;
        private Vector2 lastSize;
        
        protected override void OnRectTransformDimensionsChange()
        {
            if (MatchElements == null)
                return;
            
            var rect = (RectTransform)transform;
            if (rect.sizeDelta == lastSize)
                return;
            
            var preferredHeight = 0f;
            for (var i = 0; i < MatchElements.Length; i++)
            {
                var child = MatchElements[i];
                LayoutRebuilder.ForceRebuildLayoutImmediate(child);
                //assume we're anchored top left so all elements go down
                var height = child.rect.min.y * -1; 
                if (height > preferredHeight)
                    preferredHeight = height;
            }

            if (preferredHeight == 0)
                return; //why

            lastSize = new Vector2(rect.sizeDelta.x, preferredHeight);
            
            rect.sizeDelta = lastSize;
            LayoutRebuilder.MarkLayoutForRebuild(rect);
        }
    }
}



