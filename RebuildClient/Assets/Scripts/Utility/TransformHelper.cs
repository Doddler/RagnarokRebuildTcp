using UnityEngine;

namespace Assets.Scripts.Utility
{
    public static class TransformHelper
    {
        public static RectTransform RectTransform(this Transform t) => (RectTransform)t;
        public static RectTransform RectTransform(this GameObject t) => (RectTransform)t.transform;
        public static RectTransform RectTransform(this MonoBehaviour m) => (RectTransform)m.transform;

        //rect.rect resolves stretched anchors; sizeDelta is only the real size when the anchors match.
        private static Vector2 ScreenSize(RectTransform rect) => Vector2.Scale(rect.rect.size, rect.lossyScale);

        public static Vector3 ClampFullyOnScreen(this RectTransform rect, Vector3 pos)
        {
            var size = ScreenSize(rect);

            var minX = size.x * rect.pivot.x;
            var maxX = Screen.width - size.x * (1f - rect.pivot.x);
            var minY = size.y * rect.pivot.y;
            var maxY = Screen.height - size.y * (1f - rect.pivot.y);

            return new Vector3(
                minX > maxX ? Screen.width / 2f : Mathf.Clamp(pos.x, minX, maxX),
                minY > maxY ? Screen.height / 2f : Mathf.Clamp(pos.y, minY, maxY),
                pos.z);
        }

        //How much of a dragged rect stays on screen, in UI units.
        private const float DragMargin = 40f;

        //Lets the rect hang off the left, right and bottom, but never past the top - windows are dragged
        //by their title bar, so a rect above the top edge could not be dragged back.
        public static Vector3 ClampDragToScreen(this RectTransform rect, Vector3 pos)
        {
            var size = ScreenSize(rect);

            var toLeft = size.x * rect.pivot.x;
            var toRight = size.x * (1f - rect.pivot.x);
            var toTop = size.y * (1f - rect.pivot.y);

            var marginX = DragMargin * rect.lossyScale.x;
            var marginY = DragMargin * rect.lossyScale.y;

            return new Vector3(
                Mathf.Clamp(pos.x, marginX - toRight, Screen.width - marginX + toLeft),
                Mathf.Clamp(pos.y, marginY - toTop, Screen.height - toTop),
                pos.z);
        }
    }
}