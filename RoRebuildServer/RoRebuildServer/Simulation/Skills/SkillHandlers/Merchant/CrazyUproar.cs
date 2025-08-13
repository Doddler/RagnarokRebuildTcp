using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Merchant;

[SkillHandler(CharacterSkill.CrazyUproar, SkillClass.Physical, SkillTarget.Self)]
public class CrazyUproar : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        var ch = source.Character;

        source.ApplyCooldownForSupportSkillAction();

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.CrazyUproar, 300f);
        source.AddStatusEffect(status);

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(ch, CharacterSkill.CrazyUproar, lvl, isIndirect);
    }
}