using Assets.Scripts.SkillHandlers.Handlers;

namespace Assets.Scripts.SkillHandlers
{
	public static partial class ClientSkillHandler
	{
		static ClientSkillHandler()
		{
			handlers = new SkillHandlerBase[21];
			handlers[0] = new DefaultSkillHandler();
			handlers[1] = new BashHandler();
			handlers[2] = new DefaultSkillHandler();
			handlers[3] = new DefaultSkillHandler();
			handlers[4] = new DefaultSkillHandler();
			handlers[5] = new FireBoltHandler();
			handlers[5].ExecuteWithoutSource = true;
			handlers[6] = new ColdBoltHandler();
			handlers[6].ExecuteWithoutSource = true;
			handlers[7] = new DefaultSkillHandler();
			handlers[8] = new DefaultSkillHandler();
			handlers[9] = new DefaultSkillHandler();
			handlers[10] = new LightningBoltHandler();
			handlers[10].ExecuteWithoutSource = true;
			handlers[11] = new DefaultSkillHandler();
			handlers[12] = new ThunderStormHandler();
			handlers[13] = new DefaultSkillHandler();
			handlers[14] = new DefaultSkillHandler();
			handlers[15] = new DefaultSkillHandler();
			handlers[16] = new DefaultSkillHandler();
			handlers[17] = new DefaultSkillHandler();
			handlers[18] = new MammoniteHandler();
			handlers[19] = new DefaultSkillHandler();
			handlers[20] = new DefaultSkillHandler();
		}
	}
}
