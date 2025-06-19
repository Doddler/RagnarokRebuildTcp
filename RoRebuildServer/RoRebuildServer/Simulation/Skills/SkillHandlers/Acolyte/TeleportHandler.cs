using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte
{
    [SkillHandler(CharacterSkill.Teleport, SkillClass.Magic, SkillTarget.Self)]
    public class TeleportHandler : SkillHandlerBase
    {
        public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position, int lvl)
        {
            if (!source.CanTeleport())
                return SkillValidationResult.CannotTeleportHere;
            return base.ValidateTarget(source, target, position, lvl);
        }

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
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
