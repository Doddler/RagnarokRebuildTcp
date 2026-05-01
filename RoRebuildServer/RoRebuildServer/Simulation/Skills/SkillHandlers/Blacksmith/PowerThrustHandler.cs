using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Blacksmith;

[SkillHandler(CharacterSkill.PowerThrust, SkillClass.Physical, SkillTarget.Self)]
public class PowerThrustHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        if (!isIndirect)
            source.ApplyCooldownForSupportSkillAction();

        source.RemoveStatusOfTypeIfExists(CharacterStatusEffect.PowerThrustParty);

        var duration = lvl * 20;
        if (source.Character.Type == CharacterType.Player && source.Player.MaxAvailableLevelOfSkill(CharacterSkill.HiltBinding) > 0)
            duration = lvl * 22;

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.PowerThrustSelf, duration, lvl * 5);
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
                //if (partyMember.Player.WeaponClass < 6 || partyMember.Player.WeaponClass > 9)
                //    continue;
                if (partyMember.CombatEntity.HasStatusEffectOfType(CharacterStatusEffect.PowerThrustSelf))
                    continue;

                status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.PowerThrustParty, duration, 5);
                partyMember.CombatEntity.AddStatusEffect(status);
                //CommandBuilder.SendEffectOnCharacterMulti(partyMember, effectId);
                CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(partyMember, CharacterSkill.PowerThrust, lvl, true);
            }
        }

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(source.Character, CharacterSkill.PowerThrust, lvl, isIndirect);
    }
}