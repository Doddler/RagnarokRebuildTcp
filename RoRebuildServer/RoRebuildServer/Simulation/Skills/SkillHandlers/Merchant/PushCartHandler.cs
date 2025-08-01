using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Merchant
{
    [SkillHandler(CharacterSkill.PushCart, SkillClass.None, SkillTarget.Passive)]
    public class PushCartHandler : SkillHandlerBase
    {
        public override void ApplyPassiveEffects(CombatEntity owner, int lvl)
        {
            if (owner.Character.Type != CharacterType.Player)
                return;

            var activeCart = owner.Player.GetData(PlayerStat.PushCart);
            if (owner.Player.GetData(PlayerStat.PushCart) > 0)
            {
                owner.Player.PlayerFollower |= activeCart switch
                {
                    1 => PlayerFollower.Cart1,
                    2 => PlayerFollower.Cart2,
                    3 => PlayerFollower.Cart3,
                    4 => PlayerFollower.Cart4,
                    _ => PlayerFollower.Cart0
                };
            }
        }

        public override void RemovePassiveEffects(CombatEntity owner, int lvl)
        {
            if (owner.Character.Type != CharacterType.Player)
                return;

            var activeCart = owner.Player.PlayerFollower & PlayerFollower.AnyCart;
            if (activeCart == PlayerFollower.None)
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
