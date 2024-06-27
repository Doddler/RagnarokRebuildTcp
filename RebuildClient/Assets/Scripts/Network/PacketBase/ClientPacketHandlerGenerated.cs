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
			handlers = new ClientPacketHandlerBase[60];
			handlers[0] = new InvalidPacket(); //ConnectionApproved
			handlers[1] = new InvalidPacket(); //ConnectionDenied
			handlers[2] = new InvalidPacket(); //PlayerReady
			handlers[3] = new InvalidPacket(); //EnterServer
			handlers[4] = new InvalidPacket(); //Ping
			handlers[5] = new InvalidPacket(); //CreateEntity
			handlers[6] = new InvalidPacket(); //StartMove
			handlers[7] = new InvalidPacket(); //FixedMove
			handlers[8] = new InvalidPacket(); //Move
			handlers[9] = new InvalidPacket(); //Attack
			handlers[10] = new InvalidPacket(); //TakeDamage
			handlers[11] = new InvalidPacket(); //LookTowards
			handlers[12] = new InvalidPacket(); //SitStand
			handlers[13] = new InvalidPacket(); //RemoveEntity
			handlers[14] = new InvalidPacket(); //RemoveAllEntities
			handlers[15] = new InvalidPacket(); //Disconnect
			handlers[16] = new InvalidPacket(); //ChangeMaps
			handlers[17] = new InvalidPacket(); //StopAction
			handlers[18] = new InvalidPacket(); //StopImmediate
			handlers[19] = new InvalidPacket(); //RandomTeleport
			handlers[20] = new InvalidPacket(); //UnhandledPacket
			handlers[21] = new InvalidPacket(); //HitTarget
			handlers[22] = new PacketStartCasting(); //StartCast
			handlers[23] = new InvalidPacket(); //StartAreaCast
			handlers[24] = new InvalidPacket(); //CreateCastCircle
			handlers[25] = new InvalidPacket(); //Skill
			handlers[26] = new InvalidPacket(); //SkillError
			handlers[27] = new PacketErrorMessage(); //ErrorMessage
			handlers[28] = new InvalidPacket(); //ChangeTarget
			handlers[29] = new InvalidPacket(); //GainExp
			handlers[30] = new InvalidPacket(); //LevelUp
			handlers[31] = new InvalidPacket(); //Death
			handlers[32] = new InvalidPacket(); //HpRecovery
			handlers[33] = new InvalidPacket(); //Respawn
			handlers[34] = new InvalidPacket(); //RequestFailed
			handlers[35] = new InvalidPacket(); //Targeted
			handlers[36] = new InvalidPacket(); //Say
			handlers[37] = new InvalidPacket(); //ChangeName
			handlers[38] = new InvalidPacket(); //Resurrection
			handlers[39] = new InvalidPacket(); //UseInventoryItem
			handlers[40] = new InvalidPacket(); //EffectOnCharacter
			handlers[41] = new InvalidPacket(); //EffectAtLocation
			handlers[42] = new InvalidPacket(); //Emote
			handlers[43] = new InvalidPacket(); //ClientTextCommand
			handlers[44] = new PacketUpdatePlayerData(); //UpdatePlayerData
			handlers[45] = new PacketApplySkillPoint(); //ApplySkillPoint
			handlers[46] = new InvalidPacket(); //AdminRequestMove
			handlers[47] = new InvalidPacket(); //AdminServerAction
			handlers[48] = new InvalidPacket(); //AdminLevelUp
			handlers[49] = new InvalidPacket(); //AdminEnterServerSpecificMap
			handlers[50] = new InvalidPacket(); //AdminChangeAppearance
			handlers[51] = new InvalidPacket(); //AdminSummonMonster
			handlers[52] = new InvalidPacket(); //AdminHideCharacter
			handlers[53] = new InvalidPacket(); //AdminChangeSpeed
			handlers[54] = new InvalidPacket(); //AdminFindTarget
			handlers[55] = new InvalidPacket(); //AdminResetSkills
			handlers[56] = new InvalidPacket(); //NpcClick
			handlers[57] = new InvalidPacket(); //NpcInteraction
			handlers[58] = new InvalidPacket(); //NpcAdvance
			handlers[59] = new InvalidPacket(); //NpcSelectOption
		}
	}
}
