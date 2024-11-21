namespace Assets.Scripts.UI
{
    public interface IItemDropTarget
    {
        public void DropItem();
        public void DisableDropArea() {}
    }
}