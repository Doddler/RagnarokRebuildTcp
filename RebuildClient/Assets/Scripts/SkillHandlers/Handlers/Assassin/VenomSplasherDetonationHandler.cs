using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.SkillHandlers.Handlers.Assassin
{
    [SkillHandler(CharacterSkill.VenomSplasherDetonation)]
    public class VenomSplasherDetonationHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            attack.Target?.Messages.SendElementalHitEffect(attack.Src, attack.MotionTime, AttackElement.Poison);
        }

        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            if (attack.Target == null) return;
            
            AudioManager.Instance.OneShotSoundEffect(src.Id, "assasin_venomsplasher.ogg", attack.Target.transform.position, 1f, 0.1667f);
                
            var cam = CameraFollower.Instance;
            var id = cam.EffectIdLookup["VenomSplasher"];
            cam.AttachEffectToEntity(id, attack.Target.gameObject, attack.Target.Id);

        }
    }
}