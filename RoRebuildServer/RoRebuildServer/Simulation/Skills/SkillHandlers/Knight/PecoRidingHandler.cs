using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Knight;

[SkillHandler(CharacterSkill.PecoPecoRiding, SkillClass.None, SkillTarget.Passive)]
public class PecoRidingHandler : SkillHandlerBase
{
    public override void ApplyPassiveEffects(CombatEntity owner, int lvl)
    {
        if (owner.Character.Type != CharacterType.Player)
            return;

        var riding = owner.Player.GetData(PlayerStat.FollowerType) & (int)PlayerFollower.Mounted;
        owner.Player.PlayerFollower |= (PlayerFollower)riding;
        if (riding > 0)
            owner.AddStatusEffect(CharacterStatusEffect.PecoRiding, int.MaxValue);
    }

    public override void RemovePassiveEffects(CombatEntity owner, int lvl)
    {
        if (owner.Character.Type != CharacterType.Player)
            return;

        owner.Player.PlayerFollower &= ~PlayerFollower.Mounted;
        owner.RemoveStatusOfTypeIfExists(CharacterStatusEffect.PecoRiding);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl,
        bool isIndirect, bool isItemSource)
    {
        throw new NotImplementedException();
    }
}