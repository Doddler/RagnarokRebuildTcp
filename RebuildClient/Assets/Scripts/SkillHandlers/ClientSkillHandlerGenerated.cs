using Assets.Scripts.SkillHandlers.Handlers;

namespace Assets.Scripts.SkillHandlers
{
	public static partial class ClientSkillHandler
	{
		static ClientSkillHandler()
		{
			handlers = new SkillHandlerBase[34];
			handlers[0] = new DefaultSkillHandler();
			handlers[1] = new DefaultSkillHandler();
			handlers[2] = new DefaultSkillHandler();
			handlers[3] = new BashHandler();
			handlers[4] = new DefaultSkillHandler();
			handlers[5] = new DefaultSkillHandler();
			handlers[6] = new DefaultSkillHandler();
			handlers[7] = new DefaultSkillHandler();
			handlers[8] = new DefaultSkillHandler();
			handlers[9] = new DefaultSkillHandler();
			handlers[10] = new DefaultSkillHandler();
			handlers[11] = new FireBoltHandler();
			handlers[11].ExecuteWithoutSource = true;
			handlers[12] = new ColdBoltHandler();
			handlers[12].ExecuteWithoutSource = true;
			handlers[13] = new DefaultSkillHandler();
			handlers[14] = new DefaultSkillHandler();
			handlers[15] = new DefaultSkillHandler();
			handlers[16] = new LightningBoltHandler();
			handlers[16].ExecuteWithoutSource = true;
			handlers[17] = new DefaultSkillHandler();
			handlers[18] = new ThunderStormHandler();
			handlers[19] = new DefaultSkillHandler();
			handlers[20] = new DefaultSkillHandler();
			handlers[21] = new DefaultSkillHandler();
			handlers[22] = new DefaultSkillHandler();
			handlers[23] = new DefaultSkillHandler();
			handlers[24] = new DefaultSkillHandler();
			handlers[25] = new DefaultSkillHandler();
			handlers[26] = new DefaultSkillHandler();
			handlers[27] = new DefaultSkillHandler();
			handlers[28] = new MammoniteHandler();
			handlers[29] = new TwoHandQuickenHandler();
			handlers[30] = new DefaultSkillHandler();
			handlers[31] = new DefaultSkillHandler();
			handlers[32] = new GrandThunderstormHandler();
			handlers[33] = new DefaultSkillHandler();
		}
	}
}
