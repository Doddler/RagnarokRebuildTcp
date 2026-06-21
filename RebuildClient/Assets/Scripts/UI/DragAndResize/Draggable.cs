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
        public bool LockFullyInBounds;
        public bool SnapToStep;
        public int StepSize;
        
        private bool isMouseDown = false;
        private Vector3 startMousePosition;
        private Vector3 startPosition;
        private bool adjustCanvasAlpha;
        private Vector2 initialSize;

        public UnityEvent OnDragEvent;

        public void Awake()
        {
            if (CanvasGroup != null)
                adjustCanvasAlpha = true;
            initialSize = Target.sizeDelta * Target.lossyScale;
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

            var pos = Target.position;

            pos = ClampFullyInScreen(pos);
            
            Target.position = pos;
        }

        private Vector3 ClampFullyInScreen(Vector3 pos)
        {
            var width = Target.rect.width * Target.transform.lossyScale.x;
            var height = Target.rect.height * Target.transform.lossyScale.y;

            var minX = width * Target.pivot.x;
            var maxX = Screen.width - width * (1f - Target.pivot.x);
            var minY = height * Target.pivot.y;
            var maxY = Screen.height - height * (1f - Target.pivot.y);

            return new Vector3(
                Mathf.Clamp(pos.x, minX, maxX),
                Mathf.Clamp(pos.y, minY, maxY),
                pos.z);
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

                var halfx = Target.sizeDelta.x * Target.transform.lossyScale.x / 2f;



                if (LockFullyInBounds)
                    pos = ClampFullyInScreen(pos);
                else
                    pos = new Vector3(Mathf.Clamp(pos.x, -halfx, Screen.width - halfx),
                        Mathf.Clamp(pos.y, Target.sizeDelta.y * Target.transform.lossyScale.y / 2f, Screen.height), pos.z);

                //Debug.Log($"Screen.width:{Screen.width} Screen.height:{Screen.height} pos:{pos} rect:{rect.sizeDelta} scale:{rect.transform.lossyScale}");
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
