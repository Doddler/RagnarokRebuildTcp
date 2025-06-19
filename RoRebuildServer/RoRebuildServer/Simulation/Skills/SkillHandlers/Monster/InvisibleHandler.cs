using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.Invisible, SkillClass.Magic, SkillTarget.Self)]
public class InvisibleHandler : SkillHandlerBase
{
    public override bool UsableWhileHidden => true;

    public override bool ShouldSkillCostSp(CombatEntity source)
    {
        return !source.HasStatusEffectOfType(CharacterStatusEffect.Invisible);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        var ch = source.Character;

        source.ApplyCooldownForSupportSkillAction();

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(ch, CharacterSkill.Invisible, lvl, isIndirect);

        var time = float.MaxValue;
        if (source.Character.Type == CharacterType.Monster)
            time = 30f;

        if (source.Character.Type == CharacterType.Player && source.Character.State == CharacterState.Sitting)
            source.Character.SitStand(false); //safety

        if (!source.StatusContainer?.ExtendStatusEffectOfType(CharacterStatusEffect.Invisible,
                Time.ElapsedTimeFloat + 30f) ?? true)
        {
            var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Invisible, time, lvl);
            source.AddStatusEffect(status);
        }
    }
}