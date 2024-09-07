using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Merchant
{
    [SkillHandler(CharacterSkill.PushCart, SkillClass.None, SkillTarget.Passive)]
    public class PushCartHandler : SkillHandlerBase
    {
        public override void ApplyPassiveEffects(CombatEntity owner, int lvl)
        {
            var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.PushCart, float.MaxValue);
            owner.AddStatusEffect(status);
        }

        public override void RemovePassiveEffects(CombatEntity owner, int lvl)
        {
            owner.RemoveStatusOfTypeIfExists(CharacterStatusEffect.PushCart);
        }

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            throw new NotImplementedException();
        }
    }
}
