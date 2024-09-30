using Assets.Scripts.SkillHandlers.Handlers;

namespace Assets.Scripts.SkillHandlers
{
	public static partial class ClientSkillHandler
	{
		static ClientSkillHandler()
		{
			handlers = new SkillHandlerBase[71];
			handlers[0] = new DefaultSkillHandler();
			handlers[1] = new DefaultSkillHandler();
			handlers[2] = new FirstAidHandler();
			handlers[3] = new BashHandler();
			handlers[4] = new EndureSkillHandler();
			handlers[5] = new DefaultSkillHandler();
			handlers[6] = new DefaultSkillHandler();
			handlers[7] = new ProvokeHandler();
			handlers[8] = new DefaultSkillHandler();
			handlers[9] = new DefaultSkillHandler();
			handlers[10] = new DefaultSkillHandler();
			handlers[11] = new FireBoltHandler();
			handlers[11].ExecuteWithoutSource = true;
			handlers[12] = new ColdBoltHandler();
			handlers[12].ExecuteWithoutSource = true;
			handlers[13] = new DefaultSkillHandler();
			handlers[14] = new FireWallHandler();
			handlers[14].ExecuteWithoutSource = true;
			handlers[15] = new DefaultSkillHandler();
			handlers[16] = new LightningBoltHandler();
			handlers[16].ExecuteWithoutSource = true;
			handlers[17] = new DefaultSkillHandler();
			handlers[18] = new DefaultSkillHandler();
			handlers[19] = new ThunderStormHandler();
			handlers[20] = new DefaultSkillHandler();
			handlers[21] = new DoubleStrafeHandler();
			handlers[22] = new DefaultSkillHandler();
			handlers[23] = new DefaultSkillHandler();
			handlers[24] = new ImproveConcentrationHandler();
			handlers[25] = new DefaultSkillHandler();
			handlers[26] = new DefaultSkillHandler();
			handlers[27] = new HealHandler();
			handlers[27].ExecuteWithoutSource = true;
			handlers[28] = new IncreaseAgiHandler();
			handlers[29] = new DefaultSkillHandler();
			handlers[30] = new BlessingHandler();
			handlers[30].ExecuteWithoutSource = true;
			handlers[31] = new DefaultSkillHandler();
			handlers[32] = new DefaultSkillHandler();
			handlers[33] = new DefaultSkillHandler();
			handlers[34] = new DefaultSkillHandler();
			handlers[35] = new DefaultSkillHandler();
			handlers[36] = new DefaultSkillHandler();
			handlers[37] = new DefaultSkillHandler();
			handlers[38] = new DefaultSkillHandler();
			handlers[39] = new DefaultSkillHandler();
			handlers[40] = new DefaultSkillHandler();
			handlers[41] = new DefaultSkillHandler();
			handlers[42] = new DefaultSkillHandler();
			handlers[43] = new DefaultSkillHandler();
			handlers[44] = new DefaultSkillHandler();
			handlers[45] = new DefaultSkillHandler();
			handlers[46] = new DefaultSkillHandler();
			handlers[47] = new DefaultSkillHandler();
			handlers[48] = new DefaultSkillHandler();
			handlers[49] = new DefaultSkillHandler();
			handlers[50] = new DefaultSkillHandler();
			handlers[51] = new DefaultSkillHandler();
			handlers[52] = new DefaultSkillHandler();
			handlers[53] = new DefaultSkillHandler();
			handlers[54] = new DefaultSkillHandler();
			handlers[55] = new DefaultSkillHandler();
			handlers[56] = new MammoniteHandler();
			handlers[57] = new DefaultSkillHandler();
			handlers[58] = new DefaultSkillHandler();
			handlers[59] = new DefaultSkillHandler();
			handlers[60] = new TwoHandQuickenHandler();
			handlers[61] = new HammerFallHandler();
			handlers[62] = new DefaultSkillHandler();
			handlers[63] = new GrandThunderstormHandler();
			handlers[64] = new SelfDestructHandler();
			handlers[64].ExecuteWithoutSource = true;
			handlers[65] = new DefaultSkillHandler();
			handlers[66] = new DefaultSkillHandler();
			handlers[67] = new SonicBlowHandler();
			handlers[68] = new DefaultSkillHandler();
			handlers[69] = new DefaultSkillHandler();
			handlers[70] = new DefaultSkillHandler();
		}
	}
}
