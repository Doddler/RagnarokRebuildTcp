using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;

[SkillHandler(CharacterSkill.Sight, SkillClass.Magic, SkillTarget.Self)]
public class SightHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        source.ApplyCooldownForSupportSkillAction();

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Sight, 10);
        source.AddStatusEffect(status);

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(source.Character, CharacterSkill.Sight, lvl);
    }
}