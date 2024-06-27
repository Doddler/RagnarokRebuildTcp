using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.ClientTypes
{
    [Serializable]
    class ItemData
    {
        public int Id;
        public string Code;
        public int Weight;
        public int Price;
        public bool IsUseable;
        public string Effect;
        public string Sprite;
    }
}
