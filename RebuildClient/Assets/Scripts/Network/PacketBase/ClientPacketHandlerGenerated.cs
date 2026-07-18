using Assets.Scripts.Network;
using Assets.Scripts.Network.PacketBase;
using Assets.Scripts.Network.IncomingPacketHandlers;
using Assets.Scripts.Network.IncomingPacketHandlers.Character;
using Assets.Scripts.Network.IncomingPacketHandlers.Party;
using Assets.Scripts.Network.IncomingPacketHandlers.Combat;
using Assets.Scripts.Network.IncomingPacketHandlers.Environment;
using Assets.Scripts.Network.IncomingPacketHandlers.Network;
using Assets.Scripts.Network.IncomingPacketHandlers.System;
using Assets.Scripts.Network.HandlerBase;

namespace Assets.Scripts.Network.PacketBase
{
	public static partial class ClientPacketHandler
	{
		static ClientPacketHandler()
		{
			handlers = new ClientPacketHandlerBase[114];
			handlers[0] = new InvalidPacket(); //PlayerReady
			handlers[1] = new PacketOnEnterServer(); //EnterServer
			handlers[2] = new InvalidPacket(); //Ping
			handlers[3] = new InvalidPacket(); //DeleteCharacter
			handlers[4] = new InvalidPacket(); //CreateParty
			handlers[5] = new PacketInvitePartyMember(); //InvitePartyMember
			handlers[6] = new PacketAcceptPartyInvite(); //AcceptPartyInvite
			handlers[7] = new PacketUpdateParty(); //UpdateParty
			handlers[8] = new PacketNotifyPlayerPartyChange(); //NotifyPlayerPartyChange
			handlers[9] = new InvalidPacket(); //AdminCharacterAction
			handlers[10] = new InvalidPacket(); //AdminRequestMove
			handlers[11] = new InvalidPacket(); //AdminServerAction
			handlers[12] = new InvalidPacket(); //AdminLevelUp
			handlers[13] = new InvalidPacket(); //AdminEnterServerSpecificMap
			handlers[14] = new InvalidPacket(); //AdminChangeAppearance
			handlers[15] = new InvalidPacket(); //AdminSummonMonster
			handlers[16] = new PacketAdminHideCharacter(); //AdminHideCharacter
			handlers[17] = new InvalidPacket(); //AdminChangeSpeed
			handlers[18] = new InvalidPacket(); //AdminFindTarget
			handlers[19] = new InvalidPacket(); //AdminResetSkills
			handlers[20] = new InvalidPacket(); //AdminResetStats
			handlers[21] = new InvalidPacket(); //AdminCreateItem
			handlers[22] = new InvalidPacket(); //InstancePacketHandlerStart
			handlers[23] = new PacketOnConnectionApproved(); //ConnectionApproved
			handlers[24] = new InvalidPacket(); //ConnectionDenied
			handlers[25] = new PacketCreateEntity(); //CreateEntity
			handlers[26] = new PacketCreateEntity2(); //CreateEntity2
			handlers[27] = new InvalidPacket(); //StartWalk
			handlers[28] = new InvalidPacket(); //PauseMove
			handlers[29] = new InvalidPacket(); //ResumeMove
			handlers[30] = new InvalidPacket(); //Move
			handlers[31] = new PacketAttack(); //Attack
			handlers[32] = new PacketTakeDamage(); //TakeDamage
			handlers[33] = new PacketLookTowards(); //LookTowards
			handlers[34] = new InvalidPacket(); //SitStand
			handlers[35] = new PacketRemoveEntity(); //RemoveEntity
			handlers[36] = new PacketRemoveAllEntities(); //RemoveAllEntities
			handlers[37] = new InvalidPacket(); //Disconnect
			handlers[38] = new PacketOnChangeMaps(); //ChangeMaps
			handlers[39] = new InvalidPacket(); //StopAction
			handlers[40] = new InvalidPacket(); //StopImmediate
			handlers[41] = new InvalidPacket(); //RandomTeleport
			handlers[42] = new InvalidPacket(); //UnhandledPacket
			handlers[43] = new InvalidPacket(); //HitTarget
			handlers[44] = new PacketStartCasting(); //StartCast
			handlers[45] = new InvalidPacket(); //StartAreaCast
			handlers[46] = new PacketUpdateExistingCast(); //UpdateExistingCast
			handlers[47] = new PacketStopCasting(); //StopCast
			handlers[48] = new InvalidPacket(); //CreateCastCircle
			handlers[49] = new PacketOnSkill(); //Skill
			handlers[50] = new PacketSkillIndirect(); //SkillIndirect
			handlers[51] = new PacketSkillFailure(); //SkillError
			handlers[52] = new PacketErrorMessage(); //ErrorMessage
			handlers[53] = new PacketChangeTarget(); //ChangeTarget
			handlers[54] = new InvalidPacket(); //GainExp
			handlers[55] = new InvalidPacket(); //LevelUp
			handlers[56] = new InvalidPacket(); //Death
			handlers[57] = new PacketHpRecovery(); //HpRecovery
			handlers[58] = new PacketImprovedRecoveryTick(); //ImprovedRecoveryTick
			handlers[59] = new PacketChangeSpValue(); //ChangeSpValue
			handlers[60] = new PacketUpdateZeny(); //UpdateZeny
			handlers[61] = new InvalidPacket(); //Respawn
			handlers[62] = new PacketRequestFailed(); //RequestFailed
			handlers[63] = new PacketTargeted(); //Targeted
			handlers[64] = new PacketSay(); //Say
			handlers[65] = new InvalidPacket(); //ChangeName
			handlers[66] = new PacketResurrection(); //Resurrection
			handlers[67] = new InvalidPacket(); //UseInventoryItem
			handlers[68] = new PacketEquipUnequipGear(); //EquipUnequipGear
			handlers[69] = new PacketUpdateCharacterDisplayState(); //UpdateCharacterDisplayState
			handlers[70] = new PacketAddOrRemoveInventoryItem(); //AddOrRemoveInventoryItem
			handlers[71] = new PacketEffectOnCharacter(); //EffectOnCharacter
			handlers[72] = new PacketEffectAtLocation(); //EffectAtLocation
			handlers[73] = new PacketPlayOneShotSound(); //PlayOneShotSound
			handlers[74] = new PacketEmote(); //Emote
			handlers[75] = new InvalidPacket(); //ClientTextCommand
			handlers[76] = new PacketUpdatePlayerData(); //UpdatePlayerData
			handlers[77] = new PacketApplySkillPoint(); //ApplySkillPoint
			handlers[78] = new InvalidPacket(); //ApplyStatPoints
			handlers[79] = new PacketChangeTargetableState(); //ChangeTargetableState
			handlers[80] = new PacketUpdateMapImportantEntityTracking(); //UpdateMapImportantEntityTracking
			handlers[81] = new PacketApplyStatusEffect(); //ApplyStatusEffect
			handlers[82] = new PacketRemoveStatusEffect(); //RemoveStatusEffect
			handlers[83] = new PacketSocketEquipment(); //SocketEquipment
			handlers[84] = new InvalidPacket(); //NpcClick
			handlers[85] = new PacketNpcInteraction(); //NpcInteraction
			handlers[86] = new InvalidPacket(); //NpcAdvance
			handlers[87] = new InvalidPacket(); //NpcSelectOption
			handlers[88] = new InvalidPacket(); //NpcRefineSubmit
			handlers[89] = new PacketDropItem(); //DropItem
			handlers[90] = new PacketPickUpItem(); //PickUpItem
			handlers[91] = new PacketOpenShop(); //OpenShop
			handlers[92] = new PacketOpenStorage(); //OpenStorage
			handlers[93] = new PacketStartNpcTrade(); //StartNpcTrade
			handlers[94] = new PacketStorageInteraction(); //StorageInteraction
			handlers[95] = new InvalidPacket(); //ShopBuySell
			handlers[96] = new InvalidPacket(); //NpcTradeItem
			handlers[97] = new PacketCartInventoryInteraction(); //CartInventoryInteraction
			handlers[98] = new PacketChangeFollower(); //ChangeFollower
			handlers[99] = new PacketServerEvent(); //ServerEvent
			handlers[100] = new PacketServerResult(); //ServerResult
			handlers[101] = new InvalidPacket(); //DebugEntry
			handlers[102] = new PacketMemoMapLocation(); //MemoMapLocation
			handlers[103] = new PacketChangePlayerSpecialActionState(); //ChangePlayerSpecialActionState
			handlers[104] = new PacketRefreshGrantedSkills(); //RefreshGrantedSkills
			handlers[105] = new PacketSkillWithMaskedArea(); //SkillWithMaskedArea
			handlers[106] = new PacketStartVending(); //VendingStart
			handlers[107] = new PacketVendingStop(); //VendingStop
			handlers[108] = new PacketVendingStoreView(); //VendingViewStore
			handlers[109] = new PacketVendingNotifyOfSale(); //VendingNotifyOfSale
			handlers[110] = new InvalidPacket(); //VendingPurchaseFromStore
			handlers[111] = new InvalidPacket(); //StartWalkInDirection
			handlers[112] = new PacketResetMotion(); //ResetMotion
			handlers[113] = new PacketToggleActivatedState(); //ToggleActivatedState
		}
	}
}
