using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Assassin;

[SkillHandler(CharacterSkill.PoisonReact, SkillClass.Physical, SkillTarget.Self)]
public class PoisonReactHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.PoisonReact, 30 + lvl * 15, lvl, (1 + lvl) / 2);
        source.AddStatusEffect(status);

        if (!isIndirect)
            source.ApplyCooldownForSupportSkillAction();

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(source.Character, CharacterSkill.PoisonReact, lvl, isIndirect);
    }
}