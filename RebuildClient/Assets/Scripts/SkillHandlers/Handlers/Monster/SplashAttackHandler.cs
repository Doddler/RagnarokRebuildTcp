using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.SplashAttack)]
    public class SplashAttackHandler : SkillHandlerBase
    {
        public override int GetSkillAoESize(ServerControllable src, int lvl) => 3;

        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            if(attack.Src.CharacterType != CharacterType.Player)
                attack.Target.Messages.SendHitEffect(attack.Src, attack.DamageTiming, 2, 1);
        }

        public override void ExecuteSkillGroundTargeted(ServerControllable src, ref AttackResultData attack)
        {
            DefaultSkillCastEffect.Create(src);
            src.PerformBasicAttackMotion();
            AudioManager.Instance.AttachSoundToEntity(src.Id, "ef_bash.ogg", src.gameObject);
        }
    }
}