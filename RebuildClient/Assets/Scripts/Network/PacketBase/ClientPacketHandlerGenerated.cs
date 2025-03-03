using Assets.Scripts.Network;
using Assets.Scripts.Network.PacketBase;
using Assets.Scripts.Network.IncomingPacketHandlers;
using Assets.Scripts.Network.IncomingPacketHandlers.Character;
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
			handlers = new ClientPacketHandlerBase[92];
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
			handlers[37] = new PacketChangeSpValue(); //ChangeSpValue
			handlers[38] = new PacketUpdateZeny(); //UpdateZeny
			handlers[39] = new InvalidPacket(); //Respawn
			handlers[40] = new PacketRequestFailed(); //RequestFailed
			handlers[41] = new PacketTargeted(); //Targeted
			handlers[42] = new PacketSay(); //Say
			handlers[43] = new InvalidPacket(); //ChangeName
			handlers[44] = new PacketResurrection(); //Resurrection
			handlers[45] = new InvalidPacket(); //UseInventoryItem
			handlers[46] = new PacketEquipUnequipGear(); //EquipUnequipGear
			handlers[47] = new PacketUpdateCharacterDisplayState(); //UpdateCharacterDisplayState
			handlers[48] = new PacketAddOrRemoveInventoryItem(); //AddOrRemoveInventoryItem
			handlers[49] = new PacketEffectOnCharacter(); //EffectOnCharacter
			handlers[50] = new PacketEffectAtLocation(); //EffectAtLocation
			handlers[51] = new PacketPlayOneShotSound(); //PlayOneShotSound
			handlers[52] = new PacketEmote(); //Emote
			handlers[53] = new InvalidPacket(); //ClientTextCommand
			handlers[54] = new PacketUpdatePlayerData(); //UpdatePlayerData
			handlers[55] = new PacketApplySkillPoint(); //ApplySkillPoint
			handlers[56] = new InvalidPacket(); //ApplyStatPoints
			handlers[57] = new PacketChangeTargetableState(); //ChangeTargetableState
			handlers[58] = new PacketUpdateMinimapMarker(); //UpdateMinimapMarker
			handlers[59] = new PacketApplyStatusEffect(); //ApplyStatusEffect
			handlers[60] = new PacketRemoveStatusEffect(); //RemoveStatusEffect
			handlers[61] = new PacketSocketEquipment(); //SocketEquipment
			handlers[62] = new InvalidPacket(); //AdminRequestMove
			handlers[63] = new InvalidPacket(); //AdminServerAction
			handlers[64] = new InvalidPacket(); //AdminLevelUp
			handlers[65] = new InvalidPacket(); //AdminEnterServerSpecificMap
			handlers[66] = new InvalidPacket(); //AdminChangeAppearance
			handlers[67] = new InvalidPacket(); //AdminSummonMonster
			handlers[68] = new PacketAdminHideCharacter(); //AdminHideCharacter
			handlers[69] = new InvalidPacket(); //AdminChangeSpeed
			handlers[70] = new InvalidPacket(); //AdminFindTarget
			handlers[71] = new InvalidPacket(); //AdminResetSkills
			handlers[72] = new InvalidPacket(); //AdminResetStats
			handlers[73] = new InvalidPacket(); //AdminCreateItem
			handlers[74] = new InvalidPacket(); //NpcClick
			handlers[75] = new InvalidPacket(); //NpcInteraction
			handlers[76] = new InvalidPacket(); //NpcAdvance
			handlers[77] = new InvalidPacket(); //NpcSelectOption
			handlers[78] = new InvalidPacket(); //NpcRefineSubmit
			handlers[79] = new PacketDropItem(); //DropItem
			handlers[80] = new PacketPickUpItem(); //PickUpItem
			handlers[81] = new PacketOpenShop(); //OpenShop
			handlers[82] = new PacketOpenStorage(); //OpenStorage
			handlers[83] = new PacketStorageInteraction(); //StorageInteraction
			handlers[84] = new InvalidPacket(); //ShopBuySell
			handlers[85] = new InvalidPacket(); //ItemUpdate
			handlers[86] = new PacketServerEvent(); //ServerEvent
			handlers[87] = new InvalidPacket(); //DebugEntry
			handlers[88] = new PacketMemoMapLocation(); //MemoMapLocation
			handlers[89] = new InvalidPacket(); //DeleteCharacter
			handlers[90] = new InvalidPacket(); //AdminCharacterAction
			handlers[91] = new PacketChangePlayerSpecialActionState(); //ChangePlayerSpecialActionState
		}
	}
}
