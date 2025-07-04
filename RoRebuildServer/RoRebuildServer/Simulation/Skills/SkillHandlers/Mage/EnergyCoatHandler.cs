using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;

[SkillHandler(CharacterSkill.EnergyCoat)]
public class EnergyCoatHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 5f;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.EnergyCoat, 300f);
        source.AddStatusEffect(status);

        if (!isIndirect)
            source.ApplyCooldownForSupportSkillAction();

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(source.Character, CharacterSkill.EnergyCoat, lvl, isIndirect);
    }
}