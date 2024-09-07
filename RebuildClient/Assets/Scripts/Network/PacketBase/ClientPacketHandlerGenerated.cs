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
			handlers = new ClientPacketHandlerBase[69];
			handlers[0] = new InvalidPacket(); //ConnectionApproved
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
			handlers[29] = new InvalidPacket(); //SkillError
			handlers[30] = new PacketErrorMessage(); //ErrorMessage
			handlers[31] = new PacketChangeTarget(); //ChangeTarget
			handlers[32] = new InvalidPacket(); //GainExp
			handlers[33] = new InvalidPacket(); //LevelUp
			handlers[34] = new InvalidPacket(); //Death
			handlers[35] = new InvalidPacket(); //HpRecovery
			handlers[36] = new PacketChangeSpValue(); //ChangeSpValue
			handlers[37] = new InvalidPacket(); //Respawn
			handlers[38] = new PacketRequestFailed(); //RequestFailed
			handlers[39] = new PacketTargeted(); //Targeted
			handlers[40] = new PacketSay(); //Say
			handlers[41] = new InvalidPacket(); //ChangeName
			handlers[42] = new PacketResurrection(); //Resurrection
			handlers[43] = new InvalidPacket(); //UseInventoryItem
			handlers[44] = new InvalidPacket(); //EffectOnCharacter
			handlers[45] = new PacketEffectAtLocation(); //EffectAtLocation
			handlers[46] = new PacketPlayOneShotSound(); //PlayOneShotSound
			handlers[47] = new PacketEmote(); //Emote
			handlers[48] = new InvalidPacket(); //ClientTextCommand
			handlers[49] = new PacketUpdatePlayerData(); //UpdatePlayerData
			handlers[50] = new PacketApplySkillPoint(); //ApplySkillPoint
			handlers[51] = new PacketChangeTargetableState(); //ChangeTargetableState
			handlers[52] = new PacketUpdateMinimapMarker(); //UpdateMinimapMarker
			handlers[53] = new PacketApplyStatusEffect(); //ApplyStatusEffect
			handlers[54] = new PacketRemoveStatusEffect(); //RemoveStatusEffect
			handlers[55] = new InvalidPacket(); //AdminRequestMove
			handlers[56] = new InvalidPacket(); //AdminServerAction
			handlers[57] = new InvalidPacket(); //AdminLevelUp
			handlers[58] = new InvalidPacket(); //AdminEnterServerSpecificMap
			handlers[59] = new InvalidPacket(); //AdminChangeAppearance
			handlers[60] = new InvalidPacket(); //AdminSummonMonster
			handlers[61] = new PacketAdminHideCharacter(); //AdminHideCharacter
			handlers[62] = new InvalidPacket(); //AdminChangeSpeed
			handlers[63] = new InvalidPacket(); //AdminFindTarget
			handlers[64] = new InvalidPacket(); //AdminResetSkills
			handlers[65] = new InvalidPacket(); //NpcClick
			handlers[66] = new InvalidPacket(); //NpcInteraction
			handlers[67] = new InvalidPacket(); //NpcAdvance
			handlers[68] = new InvalidPacket(); //NpcSelectOption
		}
	}
}
