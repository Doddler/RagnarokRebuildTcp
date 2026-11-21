using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;

//a rented falcon is only kept while this skill is learned or granted (Player.UsableFollowers)
[SkillHandler(CharacterSkill.FalconMastery, SkillClass.None, SkillTarget.Passive)]
public class FalconMasteryHandler : SkillHandlerBase
{
    //learning the skill comes with a falcon
    public override void OnLearned(CombatEntity owner)
    {
        if (owner.Character.Type == CharacterType.Player)
            owner.Player.GiveFalcon();
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        throw new NotImplementedException();
    }
}
