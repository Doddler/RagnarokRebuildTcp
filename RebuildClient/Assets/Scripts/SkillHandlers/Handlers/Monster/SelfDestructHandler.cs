using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.SelfDestruct, true)]
    public class SelfDestructHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            target.Messages.SendHitEffect(attack.Src, attack.DamageTiming, 2);
        }

    }
}