using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.DarkBlessing)]
public class DarkBlessing : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 9;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (target == null)
            return;

        source.ApplyCooldownForSupportSkillAction();

        var di = DamageInfo.EmptyResult(source.Entity, target.Entity);
        di.AttackSkill = CharacterSkill.DarkBlessing;

        if (source.TestHitVsEvasionWithAttackerPenalty(target))
        {            
            var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.DarkBlessing, 1f * lvl, source.Character.Id);
            target.AddStatusEffect(status);
        }

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.DarkBlessing, 1, di);
    }
}