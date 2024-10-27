using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.UI;
using UnityEngine;
using UnityEngine.EventSystems;

public class SkillTooltipArea : UIBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public SkillWindowEntry Entry;
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        Entry.HoverTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Entry.LeaveHoverTooltip();
    }
}
