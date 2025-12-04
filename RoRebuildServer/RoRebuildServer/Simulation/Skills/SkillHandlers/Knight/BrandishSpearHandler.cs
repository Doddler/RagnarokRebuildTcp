using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Knight
{
    [SkillHandler(CharacterSkill.BrandishSpear)]
    public class BrandishSpearHandler : SkillHandlerBase
    {
        public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 0.7f;
        public override int GetSkillRange(CombatEntity source, int lvl) => int.Min(1 + (lvl - 1) / 3, 3);

        public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position, int lvl,
            bool isIndirect, bool isItemSource)
        {
            if (!isIndirect && source.Character.Type == CharacterType.Player && (source.Player.WeaponClass < 4 || source.Player.WeaponClass > 5))
                return SkillValidationResult.IncorrectWeapon; //spear only

            return base.ValidateTarget(source, target, position, lvl, isIndirect, isItemSource);
        }

        private float GetDamageForDistanceAndLevel(int distance, int lvl)
        {
            var baseAtk = 1f + 0.2f * lvl;
            var distRank = (lvl - 1) / 3;
            var distScore = distRank - int.Max(0, distance - 1);

            return baseAtk + distScore switch
            {
                3 => 1f + 0.162f * lvl,
                2 => 0.75f + 0.15f * lvl,
                1 => 0.5f + 0.1f * lvl,
                _ => 0f
            };
        }

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
        {
            lvl = lvl.Clamp(1, 10);
            var map = source.Character.Map;

            if (target == null || !target.IsValidTarget(source) || map == null)
                return;

            var srcPoint = source.Character.Position;
            var endPoint = target.Character.Position;
            var dist = (int)srcPoint.FloatDistance(endPoint);
            var aoeLength = 2 + (lvl - 1) / 3;

            using var potentialTargets = EntityListPool.Get();
            var area = Area.CreateAroundTwoPoints(srcPoint, endPoint, 7 - dist);
            map.GatherEnemiesInArea(source.Character, area, potentialTargets, true, true);
            map.AddVisiblePlayersAsPacketRecipients(source.Character, target.Character);

            source.Character.FaceTargetWithoutClientUpdate(target.Character); //if one of our targets later is stacked this will make us knock them in the right direction
            source.ApplyCooldownForSupportSkillAction();

            var castInfo = DamageInfo.EmptyResult(source.Entity, target.Entity);
            CommandBuilder.SkillExecuteTargetedSkill(source.Character, target.Character, CharacterSkill.BrandishSpear, lvl, castInfo);

            foreach (var potentialTarget in potentialTargets)
            {
                if (!potentialTarget.TryGet<CombatEntity>(out var splashTarget))
                    continue;

                var (isHit, distance) = MathHelper.IsPointInLinePathWithProjectedDistance(srcPoint, endPoint, splashTarget.Character.Position, aoeLength, 2.6f);

                if (!isHit)
                    continue;

                var attackMultiplier = GetDamageForDistanceAndLevel((int)distance, lvl);

                var req = new AttackRequest(CharacterSkill.BrandishSpear, attackMultiplier, 1, AttackFlags.Physical, AttackElement.None);
                var res = source.CalculateCombatResult(splashTarget, req);
                res.KnockBack = 2;
                res.IsIndirect = true;
                if (splashTarget.Character.Position == source.Character.Position)
                    res.AttackPosition = source.Character.Position - Directions.GetVectorForDirection(source.Character.FacingDirection); //if we're stacked knock back in view direction
                source.ExecuteCombatResult(res, false);

                CommandBuilder.AttackMulti(source.Character, splashTarget.Character, res, false);
            }

            CommandBuilder.ClearRecipients();
        }
    }
}