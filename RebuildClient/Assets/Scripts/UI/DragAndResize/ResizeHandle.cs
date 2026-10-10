using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class ResizeHandle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public RectTransform Target;
    public Vector2 MinSize;
    public Vector2 MaxSize = new Vector2(100000, 100000);
    public bool SnapToStep;
    public int StepSize;
    public int StepSizeY;
    public Vector2 BaseSize;
    public bool FlipYAxis;
    [NonSerialized] public Vector2Int CurrentStepSize = Vector2Int.one;

    private bool isMouseDown = false;
    private Vector3 startMousePosition;
    private Vector3 lastMousePosition;
    private Vector3 startPosition;
    private Vector2Int maxStepSize = Vector2Int.one;

    private Vector2 startSize;
    //public bool ShouldReturn;

    public UnityEvent OnDragEvent;

    public void Awake()
    {
        
        if (StepSizeY == 0 && StepSize != 0)
            StepSizeY = StepSize;
        
        if (SnapToStep)
            maxStepSize = new Vector2Int(Mathf.RoundToInt(MaxSize.x), Mathf.RoundToInt(MaxSize.y));
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isMouseDown = true;

        startPosition = Target.position;
        startSize = Target.sizeDelta;
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
            if (curPos == lastMousePosition)
                return;
            var mPos = new Vector2(Mathf.Clamp(curPos.x, 0, Screen.width), Mathf.Clamp(curPos.y, 0, Screen.height));

            var diff = mPos - (Vector2)startMousePosition;
            if (FlipYAxis)
                diff.y = -diff.y;

            var newSize = startSize + new Vector2(diff.x / Target.lossyScale.x, -diff.y / Target.lossyScale.y);

            if (SnapToStep)
            {
                var stepCount = new Vector2((newSize.x - BaseSize.x) / StepSize, (newSize.y - BaseSize.y) / StepSizeY);
                CurrentStepSize = new Vector2Int(Mathf.RoundToInt(stepCount.x), Mathf.RoundToInt(stepCount.y));
                newSize = BaseSize + new Vector2(CurrentStepSize.x * StepSize, CurrentStepSize.y * StepSizeY);
            }

            newSize = new Vector2(Mathf.Max(newSize.x, MinSize.x), Mathf.Max(newSize.y, MinSize.y));
            newSize = new Vector2(Mathf.Min(newSize.x, MaxSize.x), Mathf.Min(newSize.y, MaxSize.y));

            Target.sizeDelta = newSize;

            //update snapsize to match the max size
            if (SnapToStep)
                CurrentStepSize = new Vector2Int(
                    Mathf.RoundToInt((newSize.x - BaseSize.x) / StepSize), 
                    Mathf.RoundToInt((newSize.y - BaseSize.y) / StepSizeY));

            OnDragEvent.Invoke();
            lastMousePosition = curPos;
        }
    }
}
