using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.Smoking, SkillClass.Physical, SkillTarget.Self)]
public class SmokingHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        var ch = source.Character;

        source.ApplyCooldownForSupportSkillAction();

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Smoking, 10f);
        source.AddStatusEffect(status);

        //CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(ch, CharacterSkill.Smoking, lvl);
    }
}