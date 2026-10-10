using System;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.Objects
{
    public class CharacterChat : MonoBehaviour
    {
        public TextMeshProUGUI TextObject;

        [NonSerialized] public RectTransform RectTransform;

        private float maxTextWidth;

        void Awake()
        {
            RectTransform = (RectTransform)transform;
            //widest the bubble may wrap to; TextObject's own rect gets resized per-message afterward
            maxTextWidth = ((RectTransform)TextObject.transform).rect.width;
        }

        public void SetText(string text)
        {
            TextObject.text = text;
            borderDirty = true;
            RefreshBorderIfNeeded();
        }

        private bool borderDirty;

        // Sizes the bubble to its text using the actual rendered ink bounds. TextObject is resized to
        // match the border on both axes so its margin/alignment measure against the same box. Retries
        // until TMP can produce a valid size; returns true on the frame it resized.
        public bool RefreshBorderIfNeeded()
        {
            if (!borderDirty)
                return false;

            var margin = TextObject.margin; //x=left, y=top, z=right, w=bottom
            var horizontalPadding = margin.x + margin.z;
            var verticalPadding = margin.y + margin.w;

            var textRect = (RectTransform)TextObject.transform;
            //reset to the true max wrap width before measuring - a previous message may have shrunk it
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, maxTextWidth);

            TextObject.ForceMeshUpdate();
            var size = TextObject.textBounds.size;
            if (size.x <= 0f)
                return false;

            var totalWidth = size.x + horizontalPadding;
            var totalHeight = size.y + verticalPadding;

            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, totalWidth);
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);

            var rect = RectTransform;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, totalWidth);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);
            borderDirty = false;
            return true;
        }
    }
}
