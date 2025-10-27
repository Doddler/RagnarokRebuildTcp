using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Merchant
{
    [SkillHandler(CharacterSkill.PushCart, SkillClass.None, SkillTarget.Passive)]
    public class PushCartHandler : SkillHandlerBase
    {
        public override void ApplyPassiveEffects(CombatEntity owner, int lvl)
        {
            if (owner.Character.Type != CharacterType.Player)
                return;

            var activeCart = owner.Player.GetData(PlayerStat.FollowerType) & (int)PlayerFollower.AnyCart;
            owner.Player.PlayerFollower |= (PlayerFollower)activeCart;
        }

        public override void RemovePassiveEffects(CombatEntity owner, int lvl)
        {
            if (owner.Character.Type != CharacterType.Player)
                return;
            
            owner.Player.PlayerFollower &= ~PlayerFollower.AnyCart;
        }

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl,
            bool isIndirect, bool isItemSource)
        {
            throw new NotImplementedException();
        }
    }
}
