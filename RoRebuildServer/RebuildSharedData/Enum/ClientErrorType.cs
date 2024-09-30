namespace RebuildSharedData.Enum;

public enum ClientErrorType : byte
{
    None,
    UnknownMap,
    InvalidCoordinates,
    InvalidInput,
    TooManyRequests,
    MalformedRequest,
    RequestTooLong,
    DisallowedCharacters,
    CommandUnavailable
}