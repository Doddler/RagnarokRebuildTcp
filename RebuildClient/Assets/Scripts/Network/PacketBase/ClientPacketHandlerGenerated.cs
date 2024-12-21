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
			handlers = new ClientPacketHandlerBase[85];
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
			handlers[25] = new PacketStopCasting(); //StopCast
			handlers[26] = new InvalidPacket(); //CreateCastCircle
			handlers[27] = new PacketOnSkill(); //Skill
			handlers[28] = new PacketSkillIndirect(); //SkillIndirect
			handlers[29] = new PacketSkillFailure(); //SkillError
			handlers[30] = new PacketErrorMessage(); //ErrorMessage
			handlers[31] = new PacketChangeTarget(); //ChangeTarget
			handlers[32] = new InvalidPacket(); //GainExp
			handlers[33] = new InvalidPacket(); //LevelUp
			handlers[34] = new InvalidPacket(); //Death
			handlers[35] = new InvalidPacket(); //HpRecovery
			handlers[36] = new PacketChangeSpValue(); //ChangeSpValue
			handlers[37] = new PacketUpdateZeny(); //UpdateZeny
			handlers[38] = new InvalidPacket(); //Respawn
			handlers[39] = new PacketRequestFailed(); //RequestFailed
			handlers[40] = new PacketTargeted(); //Targeted
			handlers[41] = new PacketSay(); //Say
			handlers[42] = new InvalidPacket(); //ChangeName
			handlers[43] = new PacketResurrection(); //Resurrection
			handlers[44] = new InvalidPacket(); //UseInventoryItem
			handlers[45] = new PacketEquipUnequipGear(); //EquipUnequipGear
			handlers[46] = new PacketUpdateCharacterDisplayState(); //UpdateCharacterDisplayState
			handlers[47] = new PacketAddOrRemoveInventoryItem(); //AddOrRemoveInventoryItem
			handlers[48] = new PacketEffectOnCharacter(); //EffectOnCharacter
			handlers[49] = new PacketEffectAtLocation(); //EffectAtLocation
			handlers[50] = new PacketPlayOneShotSound(); //PlayOneShotSound
			handlers[51] = new PacketEmote(); //Emote
			handlers[52] = new InvalidPacket(); //ClientTextCommand
			handlers[53] = new PacketUpdatePlayerData(); //UpdatePlayerData
			handlers[54] = new PacketApplySkillPoint(); //ApplySkillPoint
			handlers[55] = new InvalidPacket(); //ApplyStatPoints
			handlers[56] = new PacketChangeTargetableState(); //ChangeTargetableState
			handlers[57] = new PacketUpdateMinimapMarker(); //UpdateMinimapMarker
			handlers[58] = new PacketApplyStatusEffect(); //ApplyStatusEffect
			handlers[59] = new PacketRemoveStatusEffect(); //RemoveStatusEffect
			handlers[60] = new PacketSocketEquipment(); //SocketEquipment
			handlers[61] = new InvalidPacket(); //AdminRequestMove
			handlers[62] = new InvalidPacket(); //AdminServerAction
			handlers[63] = new InvalidPacket(); //AdminLevelUp
			handlers[64] = new InvalidPacket(); //AdminEnterServerSpecificMap
			handlers[65] = new InvalidPacket(); //AdminChangeAppearance
			handlers[66] = new InvalidPacket(); //AdminSummonMonster
			handlers[67] = new PacketAdminHideCharacter(); //AdminHideCharacter
			handlers[68] = new InvalidPacket(); //AdminChangeSpeed
			handlers[69] = new InvalidPacket(); //AdminFindTarget
			handlers[70] = new InvalidPacket(); //AdminResetSkills
			handlers[71] = new InvalidPacket(); //AdminResetStats
			handlers[72] = new InvalidPacket(); //AdminCreateItem
			handlers[73] = new InvalidPacket(); //NpcClick
			handlers[74] = new InvalidPacket(); //NpcInteraction
			handlers[75] = new InvalidPacket(); //NpcAdvance
			handlers[76] = new InvalidPacket(); //NpcSelectOption
			handlers[77] = new InvalidPacket(); //NpcRefineSubmit
			handlers[78] = new PacketDropItem(); //DropItem
			handlers[79] = new PacketPickUpItem(); //PickUpItem
			handlers[80] = new PacketOpenShop(); //OpenShop
			handlers[81] = new InvalidPacket(); //ShopBuySell
			handlers[82] = new InvalidPacket(); //ItemUpdate
			handlers[83] = new PacketServerEvent(); //ServerEvent
			handlers[84] = new InvalidPacket(); //DebugEntry
		}
	}
}
