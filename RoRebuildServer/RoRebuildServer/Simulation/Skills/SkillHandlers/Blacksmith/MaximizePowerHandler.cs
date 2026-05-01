using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Blacksmith;

[SkillHandler(CharacterSkill.MaximizePower, SkillClass.Physical, SkillTarget.Self)]
public class MaximizePowerHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        var ch = source.Character;

        source.ApplyCooldownForSupportSkillAction();

        var len = source.Character.Type == CharacterType.Player ? -1 : 10;

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.PowerMaximize, len, lvl);
        source.AddStatusEffect(status);

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(ch, CharacterSkill.MaximizePower, lvl, isIndirect);
    }
}