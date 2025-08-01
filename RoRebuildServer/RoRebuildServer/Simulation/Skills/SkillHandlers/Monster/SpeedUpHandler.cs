using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.SpeedUp, SkillClass.Magic, SkillTarget.Self)]
public class SpeedUpHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 1f;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        var ch = source.Character;

        //source.ApplyCooldownForSupportSkillAction();

        var duration = 10 * lvl;

        if (lvl > 10)
            duration = 60;

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.SpeedUp, duration, lvl);
        source.AddStatusEffect(status);

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(ch, CharacterSkill.SpeedUp, lvl, isIndirect);
    }
}