using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Knight;

[SkillHandler(CharacterSkill.TwoHandQuicken, SkillClass.Unique, SkillTarget.Self)]
public class TwoHandQuickenHandler : SkillHandlerBase
{
    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect)
    {
        if (source.Character.Type == CharacterType.Player && source.Player.WeaponClass != 3)
            return SkillValidationResult.IncorrectWeapon;
        return base.ValidateTarget(source, target, position, lvl, false);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        source.ApplyCooldownForSupportSkillAction();

        var timing = 30; //30% delay reduction (about 42% faster with no buffs, about when combo'd with berserk pot about +100%)
        if (source.Character.Type == CharacterType.Monster && lvl >= 10)
            timing = 70; //monsters with lvl 10 get 70% delay reduction (about +330% faster with no other modifiers)

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.TwoHandQuicken, 180f, timing);
        source.AddStatusEffect(status);

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(source.Character, CharacterSkill.TwoHandQuicken, lvl, isIndirect);
    }
}