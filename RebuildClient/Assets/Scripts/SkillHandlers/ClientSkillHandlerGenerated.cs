using Assets.Scripts.SkillHandlers.Handlers;

namespace Assets.Scripts.SkillHandlers
{
	public static partial class ClientSkillHandler
	{
		static ClientSkillHandler()
		{
			handlers = new SkillHandlerBase[17];
			handlers[0] = new DefaultSkillHandler();
			handlers[1] = new Assets.Scripts.SkillHandlers.Handlers.BashHandler();
			handlers[2] = new DefaultSkillHandler();
			handlers[3] = new DefaultSkillHandler();
			handlers[4] = new DefaultSkillHandler();
			handlers[5] = new Assets.Scripts.SkillHandlers.Handlers.FireBoltHandler();
			handlers[6] = new DefaultSkillHandler();
			handlers[7] = new DefaultSkillHandler();
			handlers[8] = new DefaultSkillHandler();
			handlers[9] = new DefaultSkillHandler();
			handlers[10] = new DefaultSkillHandler();
			handlers[11] = new DefaultSkillHandler();
			handlers[12] = new DefaultSkillHandler();
			handlers[13] = new DefaultSkillHandler();
			handlers[14] = new DefaultSkillHandler();
			handlers[15] = new DefaultSkillHandler();
			handlers[16] = new DefaultSkillHandler();
		}
	}
}
