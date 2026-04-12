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
			handlers = new ClientPacketHandlerBase[113];
			handlers[0] = new PacketOnConnectionApproved(); //ConnectionApproved
			handlers[1] = new InvalidPacket(); //ConnectionDenied
			handlers[2] = new InvalidPacket(); //PlayerReady
			handlers[3] = new PacketOnEnterServer(); //EnterServer
			handlers[4] = new InvalidPacket(); //Ping
			handlers[5] = new PacketCreateEntity(); //CreateEntity
			handlers[6] = new PacketCreateEntity2(); //CreateEntity2
			handlers[7] = new InvalidPacket(); //StartWalk
			handlers[8] = new InvalidPacket(); //PauseMove
			handlers[9] = new InvalidPacket(); //ResumeMove
			handlers[10] = new InvalidPacket(); //Move
			handlers[11] = new PacketAttack(); //Attack
			handlers[12] = new PacketTakeDamage(); //TakeDamage
			handlers[13] = new PacketLookTowards(); //LookTowards
			handlers[14] = new InvalidPacket(); //SitStand
			handlers[15] = new PacketRemoveEntity(); //RemoveEntity
			handlers[16] = new PacketRemoveAllEntities(); //RemoveAllEntities
			handlers[17] = new InvalidPacket(); //Disconnect
			handlers[18] = new PacketOnChangeMaps(); //ChangeMaps
			handlers[19] = new InvalidPacket(); //StopAction
			handlers[20] = new InvalidPacket(); //StopImmediate
			handlers[21] = new InvalidPacket(); //RandomTeleport
			handlers[22] = new InvalidPacket(); //UnhandledPacket
			handlers[23] = new InvalidPacket(); //HitTarget
			handlers[24] = new PacketStartCasting(); //StartCast
			handlers[25] = new InvalidPacket(); //StartAreaCast
			handlers[26] = new PacketUpdateExistingCast(); //UpdateExistingCast
			handlers[27] = new PacketStopCasting(); //StopCast
			handlers[28] = new InvalidPacket(); //CreateCastCircle
			handlers[29] = new PacketOnSkill(); //Skill
			handlers[30] = new PacketSkillIndirect(); //SkillIndirect
			handlers[31] = new PacketSkillFailure(); //SkillError
			handlers[32] = new PacketErrorMessage(); //ErrorMessage
			handlers[33] = new PacketChangeTarget(); //ChangeTarget
			handlers[34] = new InvalidPacket(); //GainExp
			handlers[35] = new InvalidPacket(); //LevelUp
			handlers[36] = new InvalidPacket(); //Death
			handlers[37] = new PacketHpRecovery(); //HpRecovery
			handlers[38] = new PacketImprovedRecoveryTick(); //ImprovedRecoveryTick
			handlers[39] = new PacketChangeSpValue(); //ChangeSpValue
			handlers[40] = new PacketUpdateZeny(); //UpdateZeny
			handlers[41] = new InvalidPacket(); //Respawn
			handlers[42] = new PacketRequestFailed(); //RequestFailed
			handlers[43] = new PacketTargeted(); //Targeted
			handlers[44] = new PacketSay(); //Say
			handlers[45] = new InvalidPacket(); //ChangeName
			handlers[46] = new PacketResurrection(); //Resurrection
			handlers[47] = new InvalidPacket(); //UseInventoryItem
			handlers[48] = new PacketEquipUnequipGear(); //EquipUnequipGear
			handlers[49] = new PacketUpdateCharacterDisplayState(); //UpdateCharacterDisplayState
			handlers[50] = new PacketAddOrRemoveInventoryItem(); //AddOrRemoveInventoryItem
			handlers[51] = new PacketEffectOnCharacter(); //EffectOnCharacter
			handlers[52] = new PacketEffectAtLocation(); //EffectAtLocation
			handlers[53] = new PacketPlayOneShotSound(); //PlayOneShotSound
			handlers[54] = new PacketEmote(); //Emote
			handlers[55] = new InvalidPacket(); //ClientTextCommand
			handlers[56] = new PacketUpdatePlayerData(); //UpdatePlayerData
			handlers[57] = new PacketApplySkillPoint(); //ApplySkillPoint
			handlers[58] = new InvalidPacket(); //ApplyStatPoints
			handlers[59] = new PacketChangeTargetableState(); //ChangeTargetableState
			handlers[60] = new PacketUpdateMapImportantEntityTracking(); //UpdateMapImportantEntityTracking
			handlers[61] = new PacketApplyStatusEffect(); //ApplyStatusEffect
			handlers[62] = new PacketRemoveStatusEffect(); //RemoveStatusEffect
			handlers[63] = new PacketSocketEquipment(); //SocketEquipment
			handlers[64] = new InvalidPacket(); //AdminRequestMove
			handlers[65] = new InvalidPacket(); //AdminServerAction
			handlers[66] = new InvalidPacket(); //AdminLevelUp
			handlers[67] = new InvalidPacket(); //AdminEnterServerSpecificMap
			handlers[68] = new InvalidPacket(); //AdminChangeAppearance
			handlers[69] = new InvalidPacket(); //AdminSummonMonster
			handlers[70] = new PacketAdminHideCharacter(); //AdminHideCharacter
			handlers[71] = new InvalidPacket(); //AdminChangeSpeed
			handlers[72] = new InvalidPacket(); //AdminFindTarget
			handlers[73] = new InvalidPacket(); //AdminResetSkills
			handlers[74] = new InvalidPacket(); //AdminResetStats
			handlers[75] = new InvalidPacket(); //AdminCreateItem
			handlers[76] = new InvalidPacket(); //NpcClick
			handlers[77] = new PacketNpcInteraction(); //NpcInteraction
			handlers[78] = new InvalidPacket(); //NpcAdvance
			handlers[79] = new InvalidPacket(); //NpcSelectOption
			handlers[80] = new InvalidPacket(); //NpcRefineSubmit
			handlers[81] = new PacketDropItem(); //DropItem
			handlers[82] = new PacketPickUpItem(); //PickUpItem
			handlers[83] = new PacketOpenShop(); //OpenShop
			handlers[84] = new PacketOpenStorage(); //OpenStorage
			handlers[85] = new PacketStartNpcTrade(); //StartNpcTrade
			handlers[86] = new PacketStorageInteraction(); //StorageInteraction
			handlers[87] = new InvalidPacket(); //ShopBuySell
			handlers[88] = new InvalidPacket(); //NpcTradeItem
			handlers[89] = new PacketCartInventoryInteraction(); //CartInventoryInteraction
			handlers[90] = new PacketChangeFollower(); //ChangeFollower
			handlers[91] = new PacketServerEvent(); //ServerEvent
			handlers[92] = new PacketServerResult(); //ServerResult
			handlers[93] = new InvalidPacket(); //DebugEntry
			handlers[94] = new PacketMemoMapLocation(); //MemoMapLocation
			handlers[95] = new InvalidPacket(); //DeleteCharacter
			handlers[96] = new InvalidPacket(); //AdminCharacterAction
			handlers[97] = new PacketChangePlayerSpecialActionState(); //ChangePlayerSpecialActionState
			handlers[98] = new PacketRefreshGrantedSkills(); //RefreshGrantedSkills
			handlers[99] = new InvalidPacket(); //CreateParty
			handlers[100] = new PacketInvitePartyMember(); //InvitePartyMember
			handlers[101] = new PacketAcceptPartyInvite(); //AcceptPartyInvite
			handlers[102] = new PacketUpdateParty(); //UpdateParty
			handlers[103] = new PacketNotifyPlayerPartyChange(); //NotifyPlayerPartyChange
			handlers[104] = new PacketSkillWithMaskedArea(); //SkillWithMaskedArea
			handlers[105] = new PacketStartVending(); //VendingStart
			handlers[106] = new PacketVendingStop(); //VendingStop
			handlers[107] = new PacketVendingStoreView(); //VendingViewStore
			handlers[108] = new PacketVendingNotifyOfSale(); //VendingNotifyOfSale
			handlers[109] = new InvalidPacket(); //VendingPurchaseFromStore
			handlers[110] = new InvalidPacket(); //StartWalkInDirection
			handlers[111] = new PacketResetMotion(); //ResetMotion
			handlers[112] = new PacketToggleActivatedState(); //ToggleActivatedState
		}
	}
}
