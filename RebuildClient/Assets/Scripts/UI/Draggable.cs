using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI
{
    
    class Draggable : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Transform Target;
        
        private bool isMouseDown = false;
        private Vector3 startMousePosition;
        private Vector3 startPosition;
        public bool ShouldReturn;

        public UnityEvent OnDragEvent;

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

        public void Update()
        {
            if (isMouseDown)
            {
                var curPos = Input.mousePosition;
                var diff = curPos - startMousePosition;

                var pos = startPosition + diff;

                var rect = GetComponent<RectTransform>();

                var halfx = rect.sizeDelta.x * rect.transform.lossyScale.x / 2f;

                pos = new Vector3(Mathf.Clamp(pos.x, -halfx, Screen.width - halfx), Mathf.Clamp(pos.y, rect.sizeDelta.y * rect.transform.lossyScale.y / 2f, Screen.height), pos.z);

                //Debug.Log($"Screen.width:{Screen.width} Screen.height:{Screen.height} pos:{pos} rect:{rect.sizeDelta} scale:{rect.transform.lossyScale}");

                Target.position = pos;
            }
        }
    }
}
