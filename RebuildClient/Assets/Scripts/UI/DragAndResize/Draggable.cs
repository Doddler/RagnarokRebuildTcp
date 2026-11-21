using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.Utility;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI
{
    public class Draggable : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public RectTransform Target;
        public CanvasGroup CanvasGroup;
        public bool ShouldReturn;
        public bool SnapToStep;
        public int StepSize;
        
        private bool isMouseDown = false;
        private Vector3 startMousePosition;
        private Vector3 startPosition;
        private bool adjustCanvasAlpha;

        public UnityEvent OnDragEvent;

        public void Awake()
        {
            if (CanvasGroup != null)
                adjustCanvasAlpha = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isMouseDown = true;

            startPosition = Target.position;
            startMousePosition = Input.mousePosition;

            //transform.SetAsLastSibling(); //move to top

            OnDragEvent.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isMouseDown = false;

            if (ShouldReturn)
                Target.position = startPosition;
        }

        public void FitWindowIntoPlayArea()
        {
            Target.position = Target.ClampFullyOnScreen(Target.position);
        }

        public void Update()
        {
            if (isMouseDown)
            {
                if (CanvasGroup != null)
                {
                    CanvasGroup.alpha = Mathf.Lerp(CanvasGroup.alpha, 0.8f, Time.deltaTime * 10f);
                    adjustCanvasAlpha = true;
                }

                var curPos = Input.mousePosition;
                var diff = curPos - startMousePosition;

                var pos = startPosition + diff;

                pos = GameConfig.Data.KeepWindowsOnScreen
                    ? Target.ClampFullyOnScreen(pos)
                    : Target.ClampDragToScreen(pos);

                if (pos != Target.position)
                    Target.position = pos;
            }
            else
            {
                if (adjustCanvasAlpha)
                {
                    CanvasGroup.alpha = Mathf.Lerp(CanvasGroup.alpha, 1f, Time.deltaTime * 10f);
                    if (CanvasGroup.alpha > 0.99f)
                    {
                        CanvasGroup.alpha = 1f;
                        adjustCanvasAlpha = false;
                    }
                }
            }
        }
    }
}
