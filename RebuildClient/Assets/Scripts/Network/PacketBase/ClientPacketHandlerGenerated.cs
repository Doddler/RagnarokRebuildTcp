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
			handlers = new ClientPacketHandlerBase[99];
			handlers[0] = new PacketOnConnectionApproved(); //ConnectionApproved
			handlers[1] = new InvalidPacket(); //ConnectionDenied
			handlers[2] = new InvalidPacket(); //PlayerReady
			handlers[3] = new PacketOnEnterServer(); //EnterServer
			handlers[4] = new InvalidPacket(); //Ping
			handlers[5] = new PacketCreateEntity(); //CreateEntity
			handlers[6] = new InvalidPacket(); //StartWalk
			handlers[7] = new InvalidPacket(); //PauseMove
			handlers[8] = new InvalidPacket(); //ResumeMove
			handlers[9] = new InvalidPacket(); //Move
			handlers[10] = new PacketAttack(); //Attack
			handlers[11] = new PacketTakeDamage(); //TakeDamage
			handlers[12] = new PacketLookTowards(); //LookTowards
			handlers[13] = new InvalidPacket(); //SitStand
			handlers[14] = new PacketRemoveEntity(); //RemoveEntity
			handlers[15] = new PacketRemoveAllEntities(); //RemoveAllEntities
			handlers[16] = new InvalidPacket(); //Disconnect
			handlers[17] = new PacketOnChangeMaps(); //ChangeMaps
			handlers[18] = new InvalidPacket(); //StopAction
			handlers[19] = new InvalidPacket(); //StopImmediate
			handlers[20] = new InvalidPacket(); //RandomTeleport
			handlers[21] = new InvalidPacket(); //UnhandledPacket
			handlers[22] = new InvalidPacket(); //HitTarget
			handlers[23] = new PacketStartCasting(); //StartCast
			handlers[24] = new InvalidPacket(); //StartAreaCast
			handlers[25] = new PacketUpdateExistingCast(); //UpdateExistingCast
			handlers[26] = new PacketStopCasting(); //StopCast
			handlers[27] = new InvalidPacket(); //CreateCastCircle
			handlers[28] = new PacketOnSkill(); //Skill
			handlers[29] = new PacketSkillIndirect(); //SkillIndirect
			handlers[30] = new PacketSkillFailure(); //SkillError
			handlers[31] = new PacketErrorMessage(); //ErrorMessage
			handlers[32] = new PacketChangeTarget(); //ChangeTarget
			handlers[33] = new InvalidPacket(); //GainExp
			handlers[34] = new InvalidPacket(); //LevelUp
			handlers[35] = new InvalidPacket(); //Death
			handlers[36] = new InvalidPacket(); //HpRecovery
			handlers[37] = new PacketImprovedRecoveryTick(); //ImprovedRecoveryTick
			handlers[38] = new PacketChangeSpValue(); //ChangeSpValue
			handlers[39] = new PacketUpdateZeny(); //UpdateZeny
			handlers[40] = new InvalidPacket(); //Respawn
			handlers[41] = new PacketRequestFailed(); //RequestFailed
			handlers[42] = new PacketTargeted(); //Targeted
			handlers[43] = new PacketSay(); //Say
			handlers[44] = new InvalidPacket(); //ChangeName
			handlers[45] = new PacketResurrection(); //Resurrection
			handlers[46] = new InvalidPacket(); //UseInventoryItem
			handlers[47] = new PacketEquipUnequipGear(); //EquipUnequipGear
			handlers[48] = new PacketUpdateCharacterDisplayState(); //UpdateCharacterDisplayState
			handlers[49] = new PacketAddOrRemoveInventoryItem(); //AddOrRemoveInventoryItem
			handlers[50] = new PacketEffectOnCharacter(); //EffectOnCharacter
			handlers[51] = new PacketEffectAtLocation(); //EffectAtLocation
			handlers[52] = new PacketPlayOneShotSound(); //PlayOneShotSound
			handlers[53] = new PacketEmote(); //Emote
			handlers[54] = new InvalidPacket(); //ClientTextCommand
			handlers[55] = new PacketUpdatePlayerData(); //UpdatePlayerData
			handlers[56] = new PacketApplySkillPoint(); //ApplySkillPoint
			handlers[57] = new InvalidPacket(); //ApplyStatPoints
			handlers[58] = new PacketChangeTargetableState(); //ChangeTargetableState
			handlers[59] = new PacketUpdateMinimapMarker(); //UpdateMinimapMarker
			handlers[60] = new PacketApplyStatusEffect(); //ApplyStatusEffect
			handlers[61] = new PacketRemoveStatusEffect(); //RemoveStatusEffect
			handlers[62] = new PacketSocketEquipment(); //SocketEquipment
			handlers[63] = new InvalidPacket(); //AdminRequestMove
			handlers[64] = new InvalidPacket(); //AdminServerAction
			handlers[65] = new InvalidPacket(); //AdminLevelUp
			handlers[66] = new InvalidPacket(); //AdminEnterServerSpecificMap
			handlers[67] = new InvalidPacket(); //AdminChangeAppearance
			handlers[68] = new InvalidPacket(); //AdminSummonMonster
			handlers[69] = new PacketAdminHideCharacter(); //AdminHideCharacter
			handlers[70] = new InvalidPacket(); //AdminChangeSpeed
			handlers[71] = new InvalidPacket(); //AdminFindTarget
			handlers[72] = new InvalidPacket(); //AdminResetSkills
			handlers[73] = new InvalidPacket(); //AdminResetStats
			handlers[74] = new InvalidPacket(); //AdminCreateItem
			handlers[75] = new InvalidPacket(); //NpcClick
			handlers[76] = new InvalidPacket(); //NpcInteraction
			handlers[77] = new InvalidPacket(); //NpcAdvance
			handlers[78] = new InvalidPacket(); //NpcSelectOption
			handlers[79] = new InvalidPacket(); //NpcRefineSubmit
			handlers[80] = new PacketDropItem(); //DropItem
			handlers[81] = new PacketPickUpItem(); //PickUpItem
			handlers[82] = new PacketOpenShop(); //OpenShop
			handlers[83] = new PacketOpenStorage(); //OpenStorage
			handlers[84] = new PacketStorageInteraction(); //StorageInteraction
			handlers[85] = new InvalidPacket(); //ShopBuySell
			handlers[86] = new InvalidPacket(); //ItemUpdate
			handlers[87] = new PacketServerEvent(); //ServerEvent
			handlers[88] = new InvalidPacket(); //DebugEntry
			handlers[89] = new PacketMemoMapLocation(); //MemoMapLocation
			handlers[90] = new InvalidPacket(); //DeleteCharacter
			handlers[91] = new InvalidPacket(); //AdminCharacterAction
			handlers[92] = new PacketChangePlayerSpecialActionState(); //ChangePlayerSpecialActionState
			handlers[93] = new PacketRefreshGrantedSkills(); //RefreshGrantedSkills
			handlers[94] = new InvalidPacket(); //CreateParty
			handlers[95] = new PacketInvitePartyMember(); //InvitePartyMember
			handlers[96] = new PacketAcceptPartyInvite(); //AcceptPartyInvite
			handlers[97] = new PacketUpdateParty(); //UpdateParty
			handlers[98] = new PacketNotifyPlayerPartyChange(); //NotifyPlayerPartyChange
		}
	}
}
