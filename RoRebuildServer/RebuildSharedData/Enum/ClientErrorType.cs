namespace RebuildSharedData.Enum;

public enum ClientErrorType : byte
{
    None,
    UnknownMap,
    InvalidCoordinates,
    TooManyRequests,
    MalformedRequest,
    RequestTooLong,
}