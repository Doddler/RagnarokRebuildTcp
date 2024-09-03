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
        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            var ch = source.Character;

            source.ApplyCooldownForSupportSkillAction();

            var timing = 10 * lvl;
            if (source.Character.Type == CharacterType.Monster && lvl >= 10)
                timing = 200;

            var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.TwoHandQuicken, 180f, timing);
            source.AddStatusEffect(status);
            
            //source.SetStat(CharacterStat.AspdBonus, timing);
            //source.UpdateStats();

            ch.Map?.AddVisiblePlayersAsPacketRecipients(ch);
            CommandBuilder.SkillExecuteSelfTargetedSkill(ch, CharacterSkill.TwoHandQuicken, lvl);
            //CommandBuilder.SendEffectOnCharacterMulti(ch, DataManager.EffectIdForName["TwoHandQuicken"]); //Two Hand Quicken
            CommandBuilder.ClearRecipients();
        }
    }
}
