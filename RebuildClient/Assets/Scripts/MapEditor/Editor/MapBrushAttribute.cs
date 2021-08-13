using System;

namespace Assets.Scripts.MapEditor.Editor
{
    class MapBrushAttribute : Attribute
    {
        public string Name;
        public int Order;

        public MapBrushAttribute(string name, int order)
        {
            Name = name;
            Order = order;
        }
    }
}
