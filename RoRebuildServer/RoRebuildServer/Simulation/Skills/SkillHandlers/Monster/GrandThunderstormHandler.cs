using RebuildSharedData.Data;
using RoRebuildServer.EntityComponents;
using System.Diagnostics;
using RebuildSharedData.Enum;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.GrandThunderstorm, SkillClass.Magic, SkillTarget.Self)]
public class GrandThunderstormHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (source.Character.Type != CharacterType.Player)
            return; //monster version will trigger via script

        Debug.Assert(source.Character.Map != null);

        var ch = source.Character;
        var map = ch.Map;

        var e = World.Instance.CreateEvent(source.Entity, map, "GrandThunderstorm", source.Character.Position, 1, 1, 0, 0, null);
        ch.AttachEvent(e);
    }
}