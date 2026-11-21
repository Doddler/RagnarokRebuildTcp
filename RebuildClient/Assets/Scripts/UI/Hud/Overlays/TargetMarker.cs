using Assets.Scripts.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Hud
{
    //Its rect covers the whole bob, so nothing stacked above overlaps the arrow.
    public class TargetMarker : MonoBehaviour
    {
        private const string SpritePath = "Assets/Sprites/Misc/cursors.spr";
        private const int TargetAction = 3;
        //UI pixels per sprite pixel, the scale the world cursor was drawn at
        private const float Scale = 1.5f;

        private RectTransform arrow;
        private Image image;
        private float frameSeconds;
        private float[] frameHeights;
        private int shownFrame = -1;

        public static TargetMarker Create(Transform parent)
        {
            var go = new GameObject("TargetMarker", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.SetActive(false);
            var rect = (RectTransform)go.transform;
            rect.pivot = new Vector2(0.5f, 0f);

            var arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            arrowGo.transform.SetParent(go.transform, false);

            var marker = go.AddComponent<TargetMarker>();
            marker.arrow = (RectTransform)arrowGo.transform;
            marker.arrow.anchorMin = marker.arrow.anchorMax = marker.arrow.pivot = new Vector2(0.5f, 0f);
            marker.image = arrowGo.GetComponent<Image>();
            marker.image.raycastTarget = false;
            marker.image.enabled = false;

            AddressableUtility.LoadRoSpriteData(go, SpritePath, marker.OnSpriteLoaded);
            return marker;
        }

        private void OnSpriteLoaded(RoSpriteData data)
        {
            if (data == null || data.Actions.Length <= TargetAction)
                return;

            var action = data.Actions[TargetAction];
            var frames = action.Frames;
            if (frames.Length == 0 || frames[0].Layers.Length == 0)
                return;

            //sprite y grows downward, so the highest frame has the most negative y
            var lowest = float.MinValue;
            var highest = float.MaxValue;
            for (var i = 0; i < frames.Length; i++)
            {
                var y = frames[i].Layers[0].Position.y;
                lowest = Mathf.Max(lowest, y);
                highest = Mathf.Min(highest, y);
            }

            frameHeights = new float[frames.Length];
            for (var i = 0; i < frames.Length; i++)
                frameHeights[i] = (lowest - frames[i].Layers[0].Position.y) * Scale;

            var spriteIndex = frames[0].Layers[0].Index;
            var size = (Vector2)data.SpriteSizes[spriteIndex] * Scale;
            image.sprite = data.Sprites[spriteIndex];
            image.enabled = true;
            arrow.sizeDelta = size;
            ((RectTransform)transform).sizeDelta = new Vector2(size.x, size.y + (lowest - highest) * Scale);
            //a sprite without a frame delay would otherwise divide by zero
            frameSeconds = Mathf.Max(action.Delay / 1000f, 0.01f);
        }

        //only moves on a frame change, so the canvas isn't rebuilt every update
        private void Update()
        {
            if (frameHeights == null)
                return;

            var frame = (int)(Time.time / frameSeconds) % frameHeights.Length;
            if (frame == shownFrame)
                return;

            shownFrame = frame;
            arrow.anchoredPosition = new Vector2(0f, frameHeights[frame]);
        }
    }
}
