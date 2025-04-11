using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Utility
{
    public class ScrollRectWithoutDrag : ScrollRect
    {
        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
        }

        public override void OnDrag(PointerEventData eventData)
        {
        }


#if UNITY_EDITOR
        [UnityEditor.MenuItem("CONTEXT/ScrollRect/Disable Drag To Scroll")]
        static void ConvertToNoDrag(UnityEditor.MenuCommand command)
        {
            var og = (ScrollRect)command.context;
            if (og is ScrollRectWithoutDrag)
            {
                Debug.LogWarning($"Drag is already disabled on {og}");
                return;
            }

            // Cache references
            var gameObject = og.gameObject;
            var content = og.content;
            var viewport = og.viewport;
            var horizontalScrollbar = og.horizontalScrollbar;
            var verticalScrollbar = og.verticalScrollbar;
            var onValueChanged = og.onValueChanged;

            // Cache other settings
            var horizontal = og.horizontal;
            var vertical = og.vertical;
            var movementType = og.movementType;
            var elasticity = og.elasticity;
            var inertia = og.inertia;
            var decelerationRate = og.decelerationRate;
            var scrollSensitivity = og.scrollSensitivity;
            var horizontalScrollbarSpacing = og.horizontalScrollbarSpacing;
            var verticalScrollbarSpacing = og.verticalScrollbarSpacing;

            // Remove the existing component
            UnityEditor.Undo.DestroyObjectImmediate(og);

            // Add the new component and transfer the old values to it
            var newScrollRect = UnityEditor.Undo.AddComponent<ScrollRectWithoutDrag>(gameObject);

            newScrollRect.content = content;
            newScrollRect.viewport = viewport;
            newScrollRect.horizontalScrollbar = horizontalScrollbar;
            newScrollRect.verticalScrollbar = verticalScrollbar;
            newScrollRect.horizontal = horizontal;
            newScrollRect.vertical = vertical;
            newScrollRect.movementType = movementType;
            newScrollRect.elasticity = elasticity;
            newScrollRect.inertia = inertia;
            newScrollRect.decelerationRate = decelerationRate;
            newScrollRect.scrollSensitivity = scrollSensitivity;
            newScrollRect.horizontalScrollbarSpacing = horizontalScrollbarSpacing;
            newScrollRect.verticalScrollbarSpacing = verticalScrollbarSpacing;
            newScrollRect.onValueChanged = onValueChanged;

            UnityEditor.EditorUtility.SetDirty(gameObject);
        }
#endif
    }
}