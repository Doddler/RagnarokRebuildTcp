using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class ResizeHandle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{

    public Transform Target;
    public Vector2 MinSize;

    private bool isMouseDown = false;
    private Vector3 startMousePosition;
    private Vector3 startPosition;
    
    private Vector2 startSize;
    //public bool ShouldReturn;

    public UnityEvent OnDragEvent;

    public void OnPointerDown(PointerEventData eventData)
    {
        isMouseDown = true;

        var rect = Target.GetComponent<RectTransform>();

        startPosition = Target.position;
        startSize = rect.sizeDelta;
        startMousePosition = Input.mousePosition;

        Target.SetAsLastSibling(); //move to top

        OnDragEvent.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isMouseDown = false;

        //if (ShouldReturn)
        //    Target.position = startPosition;
    }

    public void Update()
    {
        if (isMouseDown)
        {
            var curPos = Input.mousePosition;
            var mPos = new Vector2(Mathf.Clamp(curPos.x, 0, Screen.width), Mathf.Clamp(curPos.y, 0, Screen.height));
            
            var diff = mPos - (Vector2)startMousePosition;
            
            var rect = Target.GetComponent<RectTransform>();
            
            var newSize = startSize + new Vector2(diff.x / rect.transform.lossyScale.x, -diff.y / rect.transform.lossyScale.y);

            newSize = new Vector2(Mathf.Max(newSize.x, MinSize.x), Mathf.Max(newSize.y, MinSize.y));

            rect.sizeDelta = newSize;


        }
    }
}
