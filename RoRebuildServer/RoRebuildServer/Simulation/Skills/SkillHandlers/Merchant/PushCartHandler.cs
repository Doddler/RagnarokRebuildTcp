using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Merchant;

//a rented cart is only kept while this skill is learned or granted (Player.UsableFollowers)
[SkillHandler(CharacterSkill.PushCart, SkillClass.None, SkillTarget.Passive)]
public class PushCartHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl,
        bool isIndirect, bool isItemSource)
    {
        throw new NotImplementedException();
    }
}