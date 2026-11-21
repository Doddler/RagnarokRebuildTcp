using System;
using System.Collections.Generic;

namespace Assets.Scripts.UI.ConfigWindow
{
    //Each token needs a binding in BuildLists.
    public enum ListOption
    {
    }

    public readonly struct ListBinding
    {
        public readonly Func<List<string>> Labels;
        public readonly Func<int> Get;
        public readonly Action<int> Set;
        public ListBinding(Func<List<string>> labels, Func<int> get, Action<int> set) { Labels = labels; Get = get; Set = set; }
    }

    public static partial class GameConfig
    {
        private static readonly Dictionary<ListOption, ListBinding> Lists = BuildLists();

        public static ListBinding ListFor(ListOption o) => Lists[o];

        private static Dictionary<ListOption, ListBinding> BuildLists()
        {
            var map = new Dictionary<ListOption, ListBinding>();
            Validate(map, "has no list binding — a list row bound to it would throw.");
            return map;
        }
    }
}
