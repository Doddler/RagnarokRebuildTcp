using System;
using System.Collections.Generic;
using System.Text;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace RebuildSharedData.ClientTypes
{
    [Serializable]
    public class EmoteData
    {
        public int Id;
        public int Sprite;
        public int Frame;
        public int X;
        public int Y;
        public float Size;
        public string Commands;
    }

}
