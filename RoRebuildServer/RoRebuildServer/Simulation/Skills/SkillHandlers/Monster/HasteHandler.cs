using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster
{
    [SkillHandler(CharacterSkill.Haste, SkillClass.Unique, SkillTarget.Self)]
    public class HasteHandler : SkillHandlerBase
    {
        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            if (source.Character.Type != CharacterType.Monster)
                return;

            var ch = source.Character;
            var mon = ch.Monster;

            source.ApplyCooldownForAttackAction();

            var recharge = mon.MonsterBase.RechargeTime / 3f;
            var motionTime = mon.MonsterBase.AttackLockTime;
            var spriteTime = mon.MonsterBase.AttackDamageTiming;
            if (recharge < motionTime)
            {
                var ratio = recharge / motionTime;
                motionTime *= ratio;
                spriteTime *= ratio;
            }


            source.SetTiming(TimingStat.AttackDelayTime, recharge);
            source.SetTiming(TimingStat.AttackMotionTime, motionTime);
            source.SetTiming(TimingStat.SpriteAttackTiming, spriteTime);

            ch.Map?.GatherPlayersForMultiCast(ch);
            CommandBuilder.SendEffectOnCharacterMulti(ch, DataManager.EffectIdForName["TwoHandQuicken"]); //Two Hand Quicken
            CommandBuilder.ClearRecipients();
        }
    }
}
