using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Thief
{
    [SkillHandler(CharacterSkill.BackSlide, SkillClass.None, SkillTarget.Self)]
    public class BackSlideHandler : SkillHandlerBase
    {
        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            var ch = source.Character;
            if (ch.Map == null)
                return;

            if (ch.Type != CharacterType.Player)
                source.ApplyCooldownForSupportSkillAction();
            else
                ch.AttackCooldown = Time.ElapsedTimeFloat + float.Max(source.GetTiming(TimingStat.AttackDelayTime), source.GetTiming(TimingStat.AttackMotionTime)) * 0.67f;

            var pos = ch.Map.WalkData.CalcKnockbackFromPosition(ch.Position, ch.Position.AddDirectionToPosition(ch.FacingDirection), 5);
            if (ch.Position != pos)
                ch.Map.ChangeEntityPosition3(ch, ch.WorldPosition, pos, false);

            ch.StopMovingImmediately();

            ch.Map?.AddVisiblePlayersAsPacketRecipients(ch);
            CommandBuilder.SendMoveEntityMulti(ch);
            CommandBuilder.SkillExecuteSelfTargetedSkill(ch, CharacterSkill.BackSlide, 1);
            CommandBuilder.ClearRecipients();
        }
    }
}
