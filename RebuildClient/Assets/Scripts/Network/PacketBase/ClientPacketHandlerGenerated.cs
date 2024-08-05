using Assets.Scripts.Network;
using Assets.Scripts.Network.PacketBase;
using Assets.Scripts.Network.IncomingPacketHandlers;
using Assets.Scripts.Network.HandlerBase;

namespace Assets.Scripts.Network.PacketBase
{
	public static partial class ClientPacketHandler
	{
		static ClientPacketHandler()
		{
			handlers = new ClientPacketHandlerBase[62];
			handlers[0] = new InvalidPacket(); //ConnectionApproved
			handlers[1] = new InvalidPacket(); //ConnectionDenied
			handlers[2] = new InvalidPacket(); //PlayerReady
			handlers[3] = new InvalidPacket(); //EnterServer
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
			handlers[17] = new InvalidPacket(); //ChangeMaps
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
			handlers[43] = new InvalidPacket(); //Emote
			handlers[44] = new InvalidPacket(); //ClientTextCommand
			handlers[45] = new PacketUpdatePlayerData(); //UpdatePlayerData
			handlers[46] = new PacketApplySkillPoint(); //ApplySkillPoint
			handlers[47] = new PacketChangeTargetableState(); //ChangeTargetableState
			handlers[48] = new InvalidPacket(); //AdminRequestMove
			handlers[49] = new InvalidPacket(); //AdminServerAction
			handlers[50] = new InvalidPacket(); //AdminLevelUp
			handlers[51] = new InvalidPacket(); //AdminEnterServerSpecificMap
			handlers[52] = new InvalidPacket(); //AdminChangeAppearance
			handlers[53] = new InvalidPacket(); //AdminSummonMonster
			handlers[54] = new InvalidPacket(); //AdminHideCharacter
			handlers[55] = new InvalidPacket(); //AdminChangeSpeed
			handlers[56] = new InvalidPacket(); //AdminFindTarget
			handlers[57] = new InvalidPacket(); //AdminResetSkills
			handlers[58] = new InvalidPacket(); //NpcClick
			handlers[59] = new InvalidPacket(); //NpcInteraction
			handlers[60] = new InvalidPacket(); //NpcAdvance
			handlers[61] = new InvalidPacket(); //NpcSelectOption
		}
	}
}
