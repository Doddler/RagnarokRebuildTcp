using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Swordsman;

[SkillHandler(CharacterSkill.Endure, SkillClass.None, SkillTarget.Self)]
public class EndureHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (!source.TryGetStatusContainer(out var s)
            || !s.TryGetExistingStatus(CharacterStatusEffect.Endure, out var existing)
            || existing.Value1 <= 2 + lvl)
        {
            //val1 is hit count, val2 is mdef increase
            var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Endure, 7f + lvl * 3f, 2 + lvl, lvl);
            source.AddStatusEffect(status);
        }

        if(!isIndirect)
            source.ApplyCooldownForSupportSkillAction();

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(source.Character, CharacterSkill.Endure, lvl, isIndirect);
    }
}