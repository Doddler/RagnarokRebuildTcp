using RO_Flex_UI.Components.DragAndDrop;

namespace Assets.Scripts.UI.Classic.DragAndDrop
{
    public interface IItemDragSource : IDragSource
    {
        int ItemId { get; }
    }
}
