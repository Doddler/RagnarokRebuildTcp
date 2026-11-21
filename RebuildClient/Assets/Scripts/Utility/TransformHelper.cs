using UnityEngine;

namespace Assets.Scripts.Utility
{
    public static class TransformHelper
    {
        //How much of a dragged rect stays on screen, in UI units.
        private const float DragMargin = 40f;

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

        //Beside the cursor, flipping to its left without room; never under it, as its graphic hangs down and right.
        public static void PlaceNextToCursor(this RectTransform rect, Vector2 gap, bool above)
        {
            var size = ScreenSize(rect);
            var cursor = (Vector2)Input.mousePosition;
            var gapX = gap.x * rect.lossyScale.x;
            var gapY = gap.y * rect.lossyScale.y;

            var bottom = above ? cursor.y + gapY : cursor.y - gapY - size.y;
            var fitsVertically = bottom >= 0f && bottom + size.y <= Screen.height;

            var left = cursor.x + gapX;
            if (!fitsVertically || left + size.x > Screen.width)
                left = cursor.x - gapX - size.x;

            left = Mathf.Clamp(left, 0f, Mathf.Max(0f, Screen.width - size.x));
            bottom = Mathf.Clamp(bottom, 0f, Mathf.Max(0f, Screen.height - size.y));

            //position places the pivot, so offset from the corner by the pivot's share of the size
            rect.position = new Vector3(left + size.x * rect.pivot.x, bottom + size.y * rect.pivot.y, rect.position.z);
        }

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