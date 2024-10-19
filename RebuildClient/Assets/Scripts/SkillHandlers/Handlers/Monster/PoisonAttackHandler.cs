using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.PoisonAttack)]
    public class PoisonAttackHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            //temporary, I think there should be a unique visual for poison attack?
            src.PerformBasicAttackMotion(CharacterSkill.PoisonAttack);
            AudioManager.Instance.AttachSoundToEntity(src.Id, "ef_bash.ogg", src.gameObject);
            if(attack.Damage > 0)
                attack.Target?.Messages.SendHitEffect(src, attack.MotionTime, 2);
        }
    }
}