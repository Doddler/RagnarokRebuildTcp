using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

            transform.SetAsLastSibling(); //move to top

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
            // var halfx = rect.sizeDelta.x * rect.transform.lossyScale.x / 2f;

            pos = new Vector3(Mathf.Clamp(pos.x, 0, Screen.width - Target.sizeDelta.x * Target.transform.lossyScale.x),
                Mathf.Clamp(pos.y, Target.sizeDelta.y * Target.transform.lossyScale.y, Screen.height), pos.z);
            
            Target.position = pos;
        }

        public void Update()
        {
            if (isMouseDown)
            {
                if (CanvasGroup != null)
                {
                    CanvasGroup.alpha = Mathf.Lerp(CanvasGroup.alpha, 0.6f, Time.deltaTime * 10f);
                    adjustCanvasAlpha = true;
                }

                var curPos = Input.mousePosition;
                var diff = curPos - startMousePosition;

                var pos = startPosition + diff;

                var halfx = Target.sizeDelta.x * Target.transform.lossyScale.x / 2f;



                
                if (LockFullyInBounds)
                pos = new Vector3(Mathf.Clamp(pos.x, 0, Screen.width - Target.sizeDelta.x * Target.transform.lossyScale.x),
                    Mathf.Clamp(pos.y, Target.sizeDelta.y * Target.transform.lossyScale.y, Screen.height), pos.z);
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