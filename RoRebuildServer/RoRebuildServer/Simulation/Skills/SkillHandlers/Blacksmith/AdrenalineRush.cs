using Antlr4.Runtime.Tree;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Blacksmith;

[SkillHandler(CharacterSkill.AdrenalineRush, SkillClass.Physical, SkillTarget.Self)]
public class AdrenalineRush : SkillHandlerBase
{
    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect, bool isItemSource)
    {
        //useable only with 1h axe, 2h axe, 1h mace, 2h mace
        if (source.Character.Type == CharacterType.Player && (source.Player.MainWeaponClass < (int)WeaponClass.Axe || source.Player.MainWeaponClass > (int)WeaponClass.TwoHandMace))
            return SkillValidationResult.IncorrectWeapon;

        return base.ValidateTarget(source, target, position, lvl, false, false);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if(!isIndirect)
            source.ApplyCooldownForSupportSkillAction();

        var duration = lvl * 30;
        if (source.Character.Type == CharacterType.Player && source.Player.MaxAvailableLevelOfSkill(CharacterSkill.HiltBinding) > 0)
            duration = lvl * 33;

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.AdrenalineRush, duration, 30);
        source.AddStatusEffect(status);
        var effectId = DataManager.EffectIdForName["AdrenalineRush"];
        CommandBuilder.SendEffectOnCharacterMulti(source.Character, effectId);

        if (source.Character.Type == CharacterType.Player && source.Player.Party != null)
        {
            foreach (var p in source.Player.Party.OnlineMembers)
            {
                if (!p.TryGet<WorldObject>(out var partyMember) || partyMember.Map != source.Character.Map)
                    continue;
                if (!partyMember.CombatEntity.IsValidAlly(source, true))
                    continue;
                if (!source.Character.Position.InRange(partyMember.Position, 14))
                    continue;
                if (partyMember.Player.MainWeaponClass < (int)WeaponClass.Axe || partyMember.Player.MainWeaponClass > (int)WeaponClass.TwoHandMace)
                    continue;

                status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.AdrenalineRush, duration, 25);
                partyMember.CombatEntity.AddStatusEffect(status);
                //CommandBuilder.SendEffectOnCharacterMulti(partyMember, effectId);
                CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(partyMember, CharacterSkill.AdrenalineRush, lvl, true);
            }
        }

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(source.Character, CharacterSkill.AdrenalineRush, lvl, isIndirect);
    }
}