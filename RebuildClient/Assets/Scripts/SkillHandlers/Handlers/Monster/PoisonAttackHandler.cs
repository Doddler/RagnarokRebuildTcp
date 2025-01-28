using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.PoisonAttack)]
    public class PoisonAttackHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            //temporary, I think there should be a unique visual for poison attack?
            src.PerformBasicAttackMotion(CharacterSkill.PoisonAttack);
            //AudioManager.Instance.AttachSoundToEntity(src.Id, "ef_bash.ogg", src.gameObject);
            if(attack.Damage > 0)
                attack.Target?.Messages.SendElementalHitEffect(src, attack.MotionTime, AttackElement.Poison);
        }
    }
}