using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Blacksmith;

[SkillHandler(CharacterSkill.WeaponPerfection, SkillClass.Physical, SkillTarget.Self)]
public class WeaponPerfectionHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        if (!isIndirect)
            source.ApplyCooldownForSupportSkillAction();

        var len = lvl * 20;
        if (source.Character.Type == CharacterType.Player && source.Player.MaxAvailableLevelOfSkill(CharacterSkill.HiltBinding) > 0)
            len = lvl * 22;

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.WeaponPerfection, len, lvl * 4);
        source.AddStatusEffect(status);

        //var effectId = DataManager.EffectIdForName["PowerThrust"];
        //CommandBuilder.SendEffectOnCharacterMulti(source.Character, effectId);

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
                //if (partyMember.Player.WeaponClass < (int)WeaponClass.Axe || partyMember.Player.WeaponClass > (int)WeaponClass.TwoHandMace)
                //    continue;

                status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.WeaponPerfection, len, lvl);
                partyMember.CombatEntity.AddStatusEffect(status);
                //CommandBuilder.SendEffectOnCharacterMulti(partyMember, effectId);
                CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(partyMember, CharacterSkill.WeaponPerfection, lvl * 2, true);
            }
        }

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(source.Character, CharacterSkill.WeaponPerfection, lvl, isIndirect);
    }
}