using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Merchant
{
    [SkillHandler(CharacterSkill.CartRevolution, SkillClass.Physical)]
    public class CartRevolutionHandler : SkillHandlerBase
    {
        public override int GetSkillRange(CombatEntity source, int lvl) => 1;

        public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target,
            Position position, int lvl, bool isIndirect, bool isItemSource)
        {
            if (source.Character.Type == CharacterType.Player && !source.Player.HasCart)
                return SkillValidationResult.CartRequired;

            return base.ValidateTarget(source, target, position, lvl, false, false);
        }

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl,
            bool isIndirect, bool isItemSource)
        {
            if (target == null || !target.IsTargetable || source.Character.Map == null)
                return;

            var map = source.Character.Map;
            Debug.Assert(map != null);

            lvl = 1; // int.Clamp(lvl, 1, 10);

            source.Character.Map.AddVisiblePlayersAsPacketRecipients(source.Character, target.Character);

            using var targetList = EntityListPool.Get();
            map.GatherEnemiesInArea(source.Character, target.Character.Position, 1, targetList, true, true);

            var weight = 0;
            if (source.Character.Type == CharacterType.Player)
            {
                if (source.Player.CartInventory != null)
                    weight = source.Player.CartInventory.BagWeight / 10;
            }
            else
                weight = (source.GetStat(CharacterStat.Level) + source.GetEffectiveStat(CharacterStat.Str)) * 50;

            var skillMod = 1.5f + weight / 8000f;

            var attack = new AttackRequest(CharacterSkill.CartRevolution, skillMod, 1, AttackFlags.Physical, AttackElement.None);

            var res = source.CalculateCombatResult(target, attack);
            res.KnockBack = 2;
            res.AttackMotionTime = 0.6f;
            res.AttackPosition = source.Character.Position;

            source.ApplyCooldownForAttackAction(position);
            CommandBuilder.SkillExecuteTargetedSkill(source.Character, target.Character, CharacterSkill.CartRevolution, lvl, res);
            source.ExecuteCombatResult(res, false);

            foreach (var e in targetList)
            {
                if (e == target.Entity)
                    continue;
                res = source.CalculateCombatResult(e.Get<CombatEntity>(), attack);
                res.KnockBack = 2;
                res.AttackMotionTime = 0.6f;
                res.AttackPosition = source.Character.Position;

                source.ExecuteCombatResult(res, false);

                if (e.TryGet<WorldObject>(out var blastTarget))
                    CommandBuilder.AttackMulti(source.Character, blastTarget, res, false);
            }

            CommandBuilder.ClearRecipients();
        }
    }
}
