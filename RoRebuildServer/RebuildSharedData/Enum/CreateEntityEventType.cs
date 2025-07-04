using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.Enum
{
    public enum CreateEntityEventType : byte
    {
        Normal,
        EnterServer,
        Warp,
        Toss,
        Descend
    };
}
