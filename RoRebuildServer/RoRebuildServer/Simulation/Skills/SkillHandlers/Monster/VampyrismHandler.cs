using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.Vampyrism, SkillClass.Magic, SkillTarget.Self)]
public class VampyrismHandler :SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        source.ApplyCooldownForSupportSkillAction();

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Vampyrism, 30f * lvl);
        source.AddStatusEffect(status);

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(source.Character, CharacterSkill.Vampyrism, lvl, isIndirect);
    }
}