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

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            if (target == null || !target.IsTargetable)
                return;

            var map = source.Character.Map;
            Debug.Assert(map != null);

            lvl = int.Clamp(lvl, 1, 10);

            source.Character.Map.AddVisiblePlayersAsPacketRecipients(source.Character, target.Character);

            using var targetList = EntityListPool.Get();
            map.GatherEnemiesInArea(source.Character, target.Character.Position, 1, targetList, true, true);

            var weight = 0;
            if (source.Character.Type == CharacterType.Player)
            {
                if (source.Player.Inventory != null)
                    weight = source.Player.Inventory.BagWeight / 10;
            }
            else
                weight = (source.GetStat(CharacterStat.Level) + source.GetEffectiveStat(CharacterStat.Str)) * 50;

            var skillMod = 0.5f + 0.1f * lvl + weight / 6000f; //this will need to change when carts are implemented

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
