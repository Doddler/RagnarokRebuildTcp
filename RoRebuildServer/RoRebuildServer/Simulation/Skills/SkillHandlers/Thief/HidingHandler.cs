using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Thief;

[SkillHandler(CharacterSkill.Hiding, SkillClass.None, SkillTarget.Self)]
public class HidingHandler : SkillHandlerBase
{
    public override bool ShouldSkillCostSp(CombatEntity source)
    {
        return !source.HasStatusEffectOfType(CharacterStatusEffect.Hiding);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        var ch = source.Character;

        source.ApplyCooldownForSupportSkillAction();
        
        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Hiding, 30f + 30f * lvl, lvl);
        source.AddStatusEffect(status);

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(ch, CharacterSkill.Hiding, lvl);
    }
}