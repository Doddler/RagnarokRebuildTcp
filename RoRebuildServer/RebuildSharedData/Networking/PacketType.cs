namespace RebuildSharedData.Networking;

public class ServerOnlyPacketAttribute : Attribute
{

}

public enum PacketType : byte
{
    [ServerOnlyPacket] ConnectionApproved,
    [ServerOnlyPacket] ConnectionDenied,
    PlayerReady,
    EnterServer,
    Ping,
    CreateEntity,
    StartMove,
    [ServerOnlyPacket] Move,
    Attack,
    LookTowards,
    SitStand,
    [ServerOnlyPacket] RemoveEntity,
    [ServerOnlyPacket] RemoveAllEntities,
    Disconnect,
    [ServerOnlyPacket] ChangeMaps,
    StopAction,
    StopImmediate,
    RandomTeleport,
    UnhandledPacket,
    HitTarget,
    Skill,
    ChangeTarget,
    [ServerOnlyPacket] GainExp,
    [ServerOnlyPacket] LevelUp,
    [ServerOnlyPacket] Death,
    [ServerOnlyPacket] HpRecovery,
    Respawn,
    [ServerOnlyPacket] RequestFailed,
    [ServerOnlyPacket] Targeted,
    Say,
    ChangeName,
    [ServerOnlyPacket] Resurrection,

    AdminRequestMove,
    AdminServerAction,
    AdminLevelUp,
    AdminEnterServerSpecificMap,

    NpcClick,
    NpcShowSprite,
    NpcDialog,
    NpcOption,
    NpcAdvance,
    NpcSelectOption,
    
}

public enum AdminAction : byte
{
    ForceGC
}