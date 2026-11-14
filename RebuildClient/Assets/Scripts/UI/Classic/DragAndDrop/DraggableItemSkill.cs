using Assets.Scripts.UI.Classic.DragAndDrop;
using RO_Flex_UI.Components;
using RO_Flex_UI.Components.DragAndDrop;
using UnityEngine;

public class DraggableItemSkill : IconAmount, IItemSkillDragSource, IDragTarget
{
    public int ItemId => throw new System.NotImplementedException();

    public int SkillId => throw new System.NotImplementedException();


    public bool CanDrag()
    {
        var dragManager = DraggableManager.Instance;

        return !Empty
        && dragManager != null
        && !dragManager.Dragging;
    }

    public bool CanDrop(DragPayload payload)
    {
        return true;
    }

    public DragPayload CreatePayload()
    {
        return new DragPayload(this, Sprite, Amount);
    }

    public void OnDragComplete()
    {
        Clear();
    }

    public void OnDropComplete(DragPayload payload)
    {
        Assign(payload.sprite, payload.text);
        SetActive(true);
    }
}