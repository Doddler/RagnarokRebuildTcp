using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Assassin;

[SkillHandler(CharacterSkill.Cloaking, SkillClass.Physical, SkillTarget.Self)]
public class CloakingHandler : SkillHandlerBase
{
    public override bool UsableWhileHidden => true;

    public override bool ShouldSkillCostSp(CombatEntity source)
    {
        return !source.HasStatusEffectOfType(CharacterStatusEffect.Cloaking);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        var ch = source.Character;

        source.ApplyCooldownForSupportSkillAction();

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(ch, CharacterSkill.Cloaking, lvl, isIndirect);

        if (source.HasStatusEffectOfType(CharacterStatusEffect.Cloaking))
        {
            source.RemoveStatusOfTypeIfExists(CharacterStatusEffect.Cloaking);
            return;
        }

        if (source.HasStatusEffectOfType(CharacterStatusEffect.Hiding))
            source.RemoveStatusOfTypeIfExists(CharacterStatusEffect.Hiding);

        var time = float.MaxValue;
        if (source.Character.Type == CharacterType.Monster)
            time = 10f;

        if (source.Character.Type == CharacterType.Player && source.Character.State == CharacterState.Sitting)
            source.Character.SitStand(false); //safety

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Cloaking, time, lvl);
        source.AddStatusEffect(status);
    }
}