using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte;

[SkillHandler(CharacterSkill.Return, SkillClass.Magic, SkillTarget.Self)]
public class ReturnHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (source.Character.Type == CharacterType.Player)
            source.Player.ReturnToSavePoint();
    }

}