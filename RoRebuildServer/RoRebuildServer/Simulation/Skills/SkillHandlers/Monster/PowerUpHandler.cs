using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.PowerUp, SkillClass.Magic, SkillTarget.Self)]
public class PowerUpHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        var ch = source.Character;

        source.ApplyCooldownForSupportSkillAction();

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.PowerUp, int.Clamp(5 * lvl, 5, 25), lvl);
        source.AddStatusEffect(status);

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(ch, CharacterSkill.PowerUp, lvl);
    }
}