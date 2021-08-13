using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildData.Shared.Enum
{
    public enum ClientErrorType : byte
    {
        UnknownMap,
        InvalidCoordinates,
        TooManyRequests,
    }
}
