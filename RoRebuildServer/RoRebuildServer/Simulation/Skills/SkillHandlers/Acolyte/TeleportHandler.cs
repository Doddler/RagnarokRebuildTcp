using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte
{
    [SkillHandler(CharacterSkill.Teleport, SkillClass.Magic, SkillTarget.Self)]
    public class TeleportHandler : SkillHandlerBase
    {
        public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target,
            Position position, int lvl, bool isIndirect, bool isItemSource)
        {
            if (!source.CanTeleport())
                return SkillValidationResult.CannotTeleportHere;
            if (source.HasBodyState(BodyStateFlags.Snared))
                return SkillValidationResult.TeleportBlocked;
            return base.ValidateTarget(source, target, position, lvl, false, false);
        }

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl,
            bool isIndirect, bool isItemSource)
        {
            //if (source.Character.Type == CharacterType.Player && lvl == 2)
            //{
            //    source.Player.ReturnToSavePoint();
            //    return;
            //}

            source.RandomTeleport();
        }
    }
}