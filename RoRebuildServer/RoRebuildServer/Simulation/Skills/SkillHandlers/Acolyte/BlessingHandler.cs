using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte
{
    [SkillHandler(CharacterSkill.Blessing, SkillClass.Magic, SkillTarget.Ally)]
    public class BlessingHandler : SkillHandlerBase
    {
        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            if (target == null)
            {
                ServerLogger.LogWarning($"Entity {source.Character.Name} is attempting to cast Increase Agi without a target.");
                return;
            }

            var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Blessing, 40f + 20 * lvl, lvl);
            target.StatusContainer.AddNewStatusEffect(status);

            var res = DamageInfo.SupportSkillResult(source.Entity, target.Entity, CharacterSkill.Blessing);
            GenericCastAndInformSupportSkill(source, target, CharacterSkill.Blessing, lvl, ref res);
        }
    }
}
