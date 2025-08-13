using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.MagicalAttack, SkillClass.Magic, SkillTarget.Enemy)]
public class MagicalAttackHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (target == null)
            return;

        var ratio = 1f + lvl * 0.5f;
        
        var res = source.CalculateCombatResult(target, ratio, 1, AttackFlags.Magical, CharacterSkill.MagicalAttack);
        
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.MagicalAttack, lvl, res);

        if (!res.IsDamageResult)
            return;

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.MagicalAttack, 15f);
        source.AddStatusEffect(status);
    }
}