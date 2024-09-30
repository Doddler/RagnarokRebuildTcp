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
    [ServerOnlyPacket] CreateEntity,
    StartWalk,
    PauseMove,
    ResumeMove,
    [ServerOnlyPacket] Move,
    Attack,
    [ServerOnlyPacket] TakeDamage,
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
    [ServerOnlyPacket] HitTarget,
    [ServerOnlyPacket] StartCast,
    [ServerOnlyPacket] StartAreaCast,
    [ServerOnlyPacket] StopCast,
    [ServerOnlyPacket] CreateCastCircle,
    Skill,
    [ServerOnlyPacket] SkillIndirect,
    [ServerOnlyPacket] SkillError,
    [ServerOnlyPacket] ErrorMessage,
    ChangeTarget,
    [ServerOnlyPacket] GainExp,
    [ServerOnlyPacket] LevelUp,
    [ServerOnlyPacket] Death,
    [ServerOnlyPacket] HpRecovery,
    [ServerOnlyPacket] ChangeSpValue,
    Respawn,
    [ServerOnlyPacket] RequestFailed,
    [ServerOnlyPacket] Targeted,
    Say,
    ChangeName,
    [ServerOnlyPacket] Resurrection,
    UseInventoryItem,
    [ServerOnlyPacket] AddOrRemoveInventoryItem,
    [ServerOnlyPacket] EffectOnCharacter,
    [ServerOnlyPacket] EffectAtLocation,
    [ServerOnlyPacket] PlayOneShotSound,
    Emote,
    ClientTextCommand,
    UpdatePlayerData,
    ApplySkillPoint,
    ChangeTargetableState,
    UpdateMinimapMarker,
    ApplyStatusEffect,
    RemoveStatusEffect,
    
    AdminRequestMove,
    AdminServerAction,
    AdminLevelUp,
    AdminEnterServerSpecificMap,
    AdminChangeAppearance,
    AdminSummonMonster,
    AdminHideCharacter,
    AdminChangeSpeed,
    AdminFindTarget,
    AdminResetSkills,
    AdminCreateItem,

    NpcClick,
    [ServerOnlyPacket] NpcInteraction,
    NpcAdvance,
    NpcSelectOption,

    DropItem,
    PickUpItem,
    [ServerOnlyPacket] OpenShop,
    ShopBuySell,
    [ServerOnlyPacket] ItemUpdate,
}

public enum MessageType : byte
{
    Local,
    MapWide,
    WorldWide,
    Server,
    Party,
    DirectMessage
}

public enum AdminAction : byte
{
    ForceGC,
    ReloadScripts,
    KillMobs
}
public enum ClientTextCommand : byte
{
    Where,
    Info,
    Adminify
}

public enum NpcInteractionType
{
    NpcFocusNpc,
    NpcDialog,
    NpcOption,
    NpcEndInteraction,
    NpcShowSprite,
}