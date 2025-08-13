using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Thief
{
    [SkillHandler(CharacterSkill.Envenom, SkillClass.Physical, SkillTarget.Enemy)]
    public class EnvenomHandler : SkillHandlerBase
    {
        public override int GetSkillRange(CombatEntity source, int lvl) => 2;
        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl,
            bool isIndirect, bool isItemSource)
        {
            if (target == null)
                return;

            var flags = AttackFlags.Physical;
            if (source.Character.Type == CharacterType.Player)
                flags |= AttackFlags.CanCrit;

            var res = source.CalculateCombatResult(target, 1f + lvl * 0.05f, 1, flags, CharacterSkill.Envenom, AttackElement.Poison);
            var poisonBarrier = res.IsDamageResult && target.RemoveStatusOfTypeIfExists(CharacterStatusEffect.Detoxify);
            if (poisonBarrier)
                res.Damage /= 2;
            source.ApplyCooldownForAttackAction(target);
            source.ExecuteCombatResult(res, false);

            var ch = source.Character;

            CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.Envenom, lvl, res);

            if (!res.IsDamageResult || poisonBarrier)
                return;
            
            var chance = 500 + lvl * 50; //50%-100%

            var duration = 4 + lvl * 2;
            if (duration < 10)
                duration = 10;

            //envenom unlike normal poison doesn't have it's duration reduced
            source.TryPoisonOnTarget(target, chance, false, res.Damage / 2, duration, res.AttackMotionTime);
        }
    }
}
