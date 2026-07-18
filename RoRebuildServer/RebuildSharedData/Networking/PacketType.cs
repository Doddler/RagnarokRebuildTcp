namespace RebuildSharedData.Networking;

public class ServerOnlyPacketAttribute : Attribute
{
}

public enum PacketType : byte
{
    //this set of packets all run on the main thread
    PlayerReady,
    EnterServer,
    Ping,
    DeleteCharacter,

    CreateParty,
    InvitePartyMember,
    AcceptPartyInvite,
    UpdateParty,
    NotifyPlayerPartyChange,

    AdminCharacterAction,
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

    //packets after this point are handled by the instance the player is on
    InstancePacketHandlerStart,

    [ServerOnlyPacket] ConnectionApproved,
    [ServerOnlyPacket] ConnectionDenied,
    [ServerOnlyPacket] CreateEntity,
    [ServerOnlyPacket] CreateEntity2,
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
    UpdateMapImportantEntityTracking,
    ApplyStatusEffect,
    RemoveStatusEffect,
    SocketEquipment,

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

    ChangePlayerSpecialActionState,
    [ServerOnlyPacket] RefreshGrantedSkills,


    SkillWithMaskedArea,

    VendingStart,
    VendingStop,
    VendingViewStore,
    VendingNotifyOfSale,
    VendingPurchaseFromStore,

    StartWalkInDirection,
    ResetMotion,

    ToggleActivatedState,
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