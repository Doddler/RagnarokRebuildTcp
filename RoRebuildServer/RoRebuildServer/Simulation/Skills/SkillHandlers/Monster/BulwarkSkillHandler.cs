using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.Bulwark, SkillClass.Physical, SkillTarget.Self)]
public class BulwarkSkillHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        source.ApplyCooldownForSupportSkillAction();
        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Bulwark, 30f);
        source.AddStatusEffect(status);

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(source.Character, CharacterSkill.Bulwark, lvl, isIndirect);
    }
}