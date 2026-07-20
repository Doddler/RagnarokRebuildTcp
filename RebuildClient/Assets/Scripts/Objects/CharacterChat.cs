using System;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.Objects
{
    public class CharacterChat : MonoBehaviour
    {
        public TextMeshProUGUI TextObject;

        [NonSerialized] public RectTransform RectTransform;

        void Awake()
        {
            RectTransform = (RectTransform)transform;
        }

        public void SetText(string text)
        {
            TextObject.text = text;
            TextObject.ForceMeshUpdate();

            var rect = (RectTransform)TextObject.transform;
            rect.anchoredPosition = new Vector3(0, 5f, 0f);

            borderDirty = true;
            RefreshBorderIfNeeded();
        }

        private bool borderDirty;

        // Sizes the bubble to its text; retries until TMP returns real bounds (it can't measure while
        // inactive). Returns true on the frame it resized so the owner can restack.
        public bool RefreshBorderIfNeeded()
        {
            if (!borderDirty)
                return false;

            var size = TextObject.textBounds.size;
            if (size.x <= 0f)
                return false;

            var rect = RectTransform;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x + 18);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y + 13);
            borderDirty = false;
            return true;
        }
    }
}
