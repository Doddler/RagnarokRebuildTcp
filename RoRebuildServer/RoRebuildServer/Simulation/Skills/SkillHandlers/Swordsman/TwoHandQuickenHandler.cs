using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Swordsman
{
    [SkillHandler(CharacterSkill.TwoHandQuicken, SkillClass.Unique, SkillTarget.Self)]
    public class TwoHandQuickenHandler : SkillHandlerBase
    {
        public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position, int lvl)
        {
            if (source.Character.Type == CharacterType.Player && source.Player.WeaponClass != 3)
                return SkillValidationResult.IncorrectWeapon;
            return base.ValidateTarget(source, target, position, lvl);
        }

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            source.ApplyCooldownForSupportSkillAction();
            
            var timing = 6 * lvl; //+6% aspd per level, to a max of +60% attack speed
            if (source.Character.Type == CharacterType.Monster && lvl >= 10)
                timing = 200; //monsters with lvl 10 get 3x attack speed instead (+200%)

            var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.TwoHandQuicken, 180f, timing);
            source.AddStatusEffect(status);
            
            CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(source.Character, CharacterSkill.TwoHandQuicken, lvl);
        }
    }
}
