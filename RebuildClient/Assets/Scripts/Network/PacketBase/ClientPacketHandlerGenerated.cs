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
			handlers = new ClientPacketHandlerBase[63];
			handlers[0] = new InvalidPacket(); //ConnectionApproved
			handlers[1] = new InvalidPacket(); //ConnectionDenied
			handlers[2] = new InvalidPacket(); //PlayerReady
			handlers[3] = new PacketOnEnterServer(); //EnterServer
			handlers[4] = new InvalidPacket(); //Ping
			handlers[5] = new InvalidPacket(); //CreateEntity
			handlers[6] = new InvalidPacket(); //StartWalk
			handlers[7] = new InvalidPacket(); //PauseMove
			handlers[8] = new InvalidPacket(); //ResumeMove
			handlers[9] = new InvalidPacket(); //Move
			handlers[10] = new InvalidPacket(); //Attack
			handlers[11] = new InvalidPacket(); //TakeDamage
			handlers[12] = new InvalidPacket(); //LookTowards
			handlers[13] = new InvalidPacket(); //SitStand
			handlers[14] = new InvalidPacket(); //RemoveEntity
			handlers[15] = new InvalidPacket(); //RemoveAllEntities
			handlers[16] = new InvalidPacket(); //Disconnect
			handlers[17] = new PacketOnChangeMaps(); //ChangeMaps
			handlers[18] = new InvalidPacket(); //StopAction
			handlers[19] = new InvalidPacket(); //StopImmediate
			handlers[20] = new InvalidPacket(); //RandomTeleport
			handlers[21] = new InvalidPacket(); //UnhandledPacket
			handlers[22] = new InvalidPacket(); //HitTarget
			handlers[23] = new PacketStartCasting(); //StartCast
			handlers[24] = new InvalidPacket(); //StartAreaCast
			handlers[25] = new InvalidPacket(); //CreateCastCircle
			handlers[26] = new InvalidPacket(); //Skill
			handlers[27] = new InvalidPacket(); //SkillError
			handlers[28] = new PacketErrorMessage(); //ErrorMessage
			handlers[29] = new InvalidPacket(); //ChangeTarget
			handlers[30] = new InvalidPacket(); //GainExp
			handlers[31] = new InvalidPacket(); //LevelUp
			handlers[32] = new InvalidPacket(); //Death
			handlers[33] = new InvalidPacket(); //HpRecovery
			handlers[34] = new InvalidPacket(); //Respawn
			handlers[35] = new InvalidPacket(); //RequestFailed
			handlers[36] = new InvalidPacket(); //Targeted
			handlers[37] = new InvalidPacket(); //Say
			handlers[38] = new InvalidPacket(); //ChangeName
			handlers[39] = new InvalidPacket(); //Resurrection
			handlers[40] = new InvalidPacket(); //UseInventoryItem
			handlers[41] = new InvalidPacket(); //EffectOnCharacter
			handlers[42] = new InvalidPacket(); //EffectAtLocation
			handlers[43] = new PacketPlayOneShotSound(); //PlayOneShotSound
			handlers[44] = new InvalidPacket(); //Emote
			handlers[45] = new InvalidPacket(); //ClientTextCommand
			handlers[46] = new PacketUpdatePlayerData(); //UpdatePlayerData
			handlers[47] = new PacketApplySkillPoint(); //ApplySkillPoint
			handlers[48] = new PacketChangeTargetableState(); //ChangeTargetableState
			handlers[49] = new InvalidPacket(); //AdminRequestMove
			handlers[50] = new InvalidPacket(); //AdminServerAction
			handlers[51] = new InvalidPacket(); //AdminLevelUp
			handlers[52] = new InvalidPacket(); //AdminEnterServerSpecificMap
			handlers[53] = new InvalidPacket(); //AdminChangeAppearance
			handlers[54] = new InvalidPacket(); //AdminSummonMonster
			handlers[55] = new InvalidPacket(); //AdminHideCharacter
			handlers[56] = new InvalidPacket(); //AdminChangeSpeed
			handlers[57] = new InvalidPacket(); //AdminFindTarget
			handlers[58] = new InvalidPacket(); //AdminResetSkills
			handlers[59] = new InvalidPacket(); //NpcClick
			handlers[60] = new InvalidPacket(); //NpcInteraction
			handlers[61] = new InvalidPacket(); //NpcAdvance
			handlers[62] = new InvalidPacket(); //NpcSelectOption
		}
	}
}
