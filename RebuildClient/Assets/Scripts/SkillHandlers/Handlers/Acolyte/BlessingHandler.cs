using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.Blessing, true)]
    public class BlessingHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src?.PerformSkillMotion();
            if (attack.Target != null)
            {
                BlessingEffect.Create(attack.Target);
                AudioManager.Instance.AttachSoundToEntity(src.Id, "ef_blessing.ogg", attack.Target.gameObject);
            }
        }
    }
}