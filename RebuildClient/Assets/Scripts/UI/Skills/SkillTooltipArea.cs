using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.UI;
using UnityEngine;
using UnityEngine.EventSystems;

public class SkillTooltipArea : UIBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public SkillWindowEntry Entry;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Entry.HoverTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Entry.LeaveHoverTooltip();
    }
}
