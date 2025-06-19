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
    [ServerOnlyPacket] UpdateExistingCast,
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
    [ServerOnlyPacket] ImprovedRecoveryTick,
    [ServerOnlyPacket] ChangeSpValue,
    [ServerOnlyPacket] UpdateZeny,
    Respawn,
    [ServerOnlyPacket] RequestFailed,
    [ServerOnlyPacket] Targeted,
    Say,
    ChangeName,
    [ServerOnlyPacket] Resurrection,
    UseInventoryItem,
    EquipUnequipGear,
    [ServerOnlyPacket] UpdateCharacterDisplayState,
    [ServerOnlyPacket] AddOrRemoveInventoryItem,
    [ServerOnlyPacket] EffectOnCharacter,
    [ServerOnlyPacket] EffectAtLocation,
    [ServerOnlyPacket] PlayOneShotSound,
    Emote,
    ClientTextCommand,
    UpdatePlayerData,
    ApplySkillPoint,
    ApplyStatPoints,
    ChangeTargetableState,
    UpdateMinimapMarker,
    ApplyStatusEffect,
    RemoveStatusEffect,
    SocketEquipment,
    
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
    AdminResetStats,
    AdminCreateItem,
    
    NpcClick,
    [ServerOnlyPacket] NpcInteraction,
    NpcAdvance,
    NpcSelectOption,
    NpcRefineSubmit,

    DropItem,
    PickUpItem,
    [ServerOnlyPacket] OpenShop,
    [ServerOnlyPacket] OpenStorage,
    [ServerOnlyPacket] StartNpcTrade,
    StorageInteraction,
    ShopBuySell,
    NpcTradeItem,
    CartInventoryInteraction,
    ChangeFollower,
    [ServerOnlyPacket] ServerEvent,
    [ServerOnlyPacket] ServerResult,
    DebugEntry,
    
    MemoMapLocation,
    DeleteCharacter,
    
    AdminCharacterAction,
    ChangePlayerSpecialActionState,
    [ServerOnlyPacket] RefreshGrantedSkills,

    CreateParty,
    InvitePartyMember,
    AcceptPartyInvite,
    UpdateParty,
    NotifyPlayerPartyChange,

    SkillWithMaskedArea,
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
    KillMobs,
    EnableMonsterDebugLogging,
    SignalNpc,
    ShutdownServer,
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
    NpcOpenRefineWindow,
    NpcBeginItemTrade,
    NpcPromptForCount,
}