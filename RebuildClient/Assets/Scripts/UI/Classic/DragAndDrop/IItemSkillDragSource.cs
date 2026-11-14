using RO_Flex_UI.Components.DragAndDrop;

namespace Assets.Scripts.UI.Classic.DragAndDrop
{
    public interface IItemSkillDragSource : IDragSource
    {
        int ItemId { get; }
        int SkillId { get; }
    }
}
