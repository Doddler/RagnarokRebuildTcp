using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ClickScrollBlock : UIBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnDrag(PointerEventData eventData)
    {
        eventData.Use();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        eventData.Use();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        eventData.Use();
    }
}
