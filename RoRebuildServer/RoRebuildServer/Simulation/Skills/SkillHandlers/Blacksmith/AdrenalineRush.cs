using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Blacksmith;

[SkillHandler(CharacterSkill.AdrenalineRush, SkillClass.Physical, SkillTarget.Self)]
public class AdrenalineRush : SkillHandlerBase
{
    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position, int lvl)
    {
        //useable only with 1h axe, 2h axe, 1h mace, 2h mace
        if (source.Character.Type == CharacterType.Player && (source.Player.WeaponClass < 6 || source.Player.WeaponClass > 9))
            return SkillValidationResult.IncorrectWeapon;

        return base.ValidateTarget(source, target, position, lvl);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        source.ApplyCooldownForSupportSkillAction();

        //was 30% cut in delay, but 60% faster is the closest with new aspd formula
        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.AdrenalineRush, lvl * 30, 60);
        source.AddStatusEffect(status);

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(source.Character, CharacterSkill.AdrenalineRush, lvl);
    }
}